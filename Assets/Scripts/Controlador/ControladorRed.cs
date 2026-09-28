using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Comunicación 1 a 1 entre las dos instancias del juego, por TCP.
    /// Un jugador actúa de host (Escuchar) y el otro de cliente (Conectar).
    /// Los mensajes viajan como una línea de JSON por vez (terminada en '\n').
    ///
    /// Las acciones que llegan del oponente NO se aplican directamente sobre el
    /// Modelo desde el hilo de red: se encolan en ColaPrincipal para que
    /// GameController las procese (y revalide) en su Update(), manteniendo un
    /// único punto de entrada y orden determinístico.
    /// </summary>
    public class ControladorRed : IDisposable
    {
        public event Action<AccionJuego> AccionRecibida;

        /// <summary>Se dispara UNA sola vez, desde un hilo de fondo, si la conexión se cae o el otro
        /// jugador se desconecta. No se dispara al cerrar la partida localmente (Dispose).</summary>
        public event Action ConexionPerdida;

        public bool EstaConectado { get; private set; }

        private int _perdidaNotificada; // 0/1, Interlocked
        private int _liberado;          // 0/1, Interlocked

        private TcpListener _listener;
        private TcpClient _cliente;
        private NetworkStream _stream;
        private readonly ColaPrincipal _colaPrincipal;
        private CancellationTokenSource _cts;

        public ControladorRed(ColaPrincipal colaPrincipal)
        {
            _colaPrincipal = colaPrincipal;
        }

        /// <summary>Actúa como host: espera a que el otro jugador se conecte.</summary>
        public async Task EscucharAsync(int puerto)
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, puerto);
            _listener.Start();

            _cliente = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            _stream = _cliente.GetStream();
            EstaConectado = true;

            _ = EscucharMensajesAsync(_cts.Token);
        }

        /// <summary>Actúa como cliente: se conecta a la IP del host.</summary>
        public async Task ConectarAsync(string ip, int puerto)
        {
            _cts = new CancellationTokenSource();
            _cliente = new TcpClient();
            await _cliente.ConnectAsync(ip, puerto).ConfigureAwait(false);
            _stream = _cliente.GetStream();
            EstaConectado = true;

            _ = EscucharMensajesAsync(_cts.Token);
        }

        /// <summary>Serializa y envía una acción al oponente.</summary>
        public async Task EnviarAccionAsync(AccionJuego accion)
        {
            if (!EstaConectado || _stream == null) return;

            string json = JsonSerializer.Serialize(accion);
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");

            try
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch (Exception)
            {
                EstaConectado = false;
                NotificarConexionPerdida();
            }
        }

        private async Task EscucharMensajesAsync(CancellationToken token)
        {
            var buffer = new byte[4096];
            var acumulado = new StringBuilder();

            try
            {
                while (!token.IsCancellationRequested && _cliente.Connected)
                {
                    int leidos = await _stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (leidos == 0) break; // el otro lado cerró la conexión

                    acumulado.Append(Encoding.UTF8.GetString(buffer, 0, leidos));

                    string contenido = acumulado.ToString();
                    int indiceSalto;
                    while ((indiceSalto = contenido.IndexOf('\n')) >= 0)
                    {
                        string linea = contenido.Substring(0, indiceSalto);
                        contenido = contenido.Substring(indiceSalto + 1);
                        ProcesarLinea(linea);
                    }
                    acumulado.Clear();
                    acumulado.Append(contenido);
                }
            }
            catch (Exception)
            {
                // conexión cortada / cancelada: se refleja en EstaConectado
            }
            finally
            {
                EstaConectado = false;
                NotificarConexionPerdida();
            }
        }

        private void NotificarConexionPerdida()
        {
            if (Interlocked.CompareExchange(ref _liberado, 0, 0) == 1) return;               // cierre local: no es una pérdida
            if (Interlocked.Exchange(ref _perdidaNotificada, 1) == 1) return;                // ya se avisó

            try { ConexionPerdida?.Invoke(); }
            catch (Exception) { }
        }

        private void ProcesarLinea(string linea)
        {
            if (string.IsNullOrWhiteSpace(linea)) return;

            try
            {
                AccionJuego accion = JsonSerializer.Deserialize<AccionJuego>(linea);
                if (accion != null)
                    _colaPrincipal.Encolar(() => AccionRecibida?.Invoke(accion));
            }
            catch (JsonException)
            {
                // mensaje corrupto/incompleto: se descarta en vez de tirar abajo la conexión
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _liberado, 1);
            _cts?.Cancel();
            _stream?.Dispose();
            _cliente?.Dispose();
            _listener?.Stop();
            EstaConectado = false;
        }
    }
}
