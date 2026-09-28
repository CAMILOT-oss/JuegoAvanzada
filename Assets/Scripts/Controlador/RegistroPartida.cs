using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Bitácora de la partida (log_partida.txt). Es segura entre hilos: cualquier Task
    /// (combate, construcción, entrenamiento, red) o el hilo principal puede llamar a
    /// Registrar(...) sin bloquearse, porque el texto solo se ENCOLA (productor) y un
    /// único hilo de fondo dedicado (consumidor) es el que escribe en disco.
    ///
    /// Hilo creado: "RegistroPartida-Escritor" (IsBackground = true).
    /// Sincronización: BlockingCollection (cola concurrente con bloqueo del consumidor).
    /// Finalización: Dispose() marca la cola como completa, el hilo vacía lo pendiente,
    /// cierra el archivo y termina (Join con tiempo máximo de espera).
    ///
    /// Formato de cada registro (los tres primeros renglones son el formato pedido en la guía):
    ///   Turno: Jugador1
    ///   Acción: Ataque
    ///   Resultado: Impacto - Unidad enemiga destruida
    ///   Hora: 14:03:22 (t+00:01:23)
    ///
    /// Clase sin dependencias de UnityEngine. Nunca lanza excepciones hacia quien la llama:
    /// un problema de disco no debe tumbar la partida.
    /// </summary>
    public sealed class RegistroPartida : IDisposable
    {
        private readonly BlockingCollection<string> _cola = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private readonly Thread _hiloEscritor;
        private readonly StreamWriter _escritor;
        private readonly Stopwatch _cronometro = Stopwatch.StartNew();

        // Ids de entidades cuya destrucción ya se registró (evita duplicados si dos
        // atacantes rematan al mismo objetivo en el mismo instante).
        private readonly HashSet<int> _destruccionesRegistradas = new HashSet<int>();
        private readonly object _bloqueoDestrucciones = new object();

        private int _cerrado; // 0 = abierto, 1 = cerrado (Interlocked)

        /// <summary>True si el archivo pudo abrirse y el registro está funcionando.</summary>
        public bool EstaActivo { get; }

        /// <summary>Mensaje del error ocurrido al abrir el archivo (null si todo salió bien).</summary>
        public string ErrorInicial { get; }

        public string Ruta { get; }

        public RegistroPartida(string rutaArchivo)
        {
            Ruta = rutaArchivo;

            try
            {
                string carpeta = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

                // FileMode.Create: cada partida nueva empieza con un log limpio.
                var flujo = new FileStream(rutaArchivo, FileMode.Create, FileAccess.Write, FileShare.Read);
                _escritor = new StreamWriter(flujo, new UTF8Encoding(false)) { AutoFlush = true };

                _escritor.WriteLine("==============================================");
                _escritor.WriteLine(" REGISTRO DE PARTIDA - IMPERIOS EN GUERRA");
                _escritor.WriteLine($" Inicio: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _escritor.WriteLine("==============================================");
                _escritor.WriteLine();

                _hiloEscritor = new Thread(BucleEscritura)
                {
                    Name = "RegistroPartida-Escritor",
                    IsBackground = true
                };
                _hiloEscritor.Start();
                EstaActivo = true;
            }
            catch (Exception e)
            {
                ErrorInicial = e.Message;
                EstaActivo = false;
            }
        }

        /// <summary>
        /// Agrega un evento a la bitácora. "turno" es el jugador que realiza la acción
        /// (o "Sistema" para eventos generales). Como el juego es en tiempo real y no por
        /// turnos, "Turno" identifica QUIÉN actúa, no el orden de jugada.
        /// </summary>
        public void Registrar(string turno, string accion, string resultado)
        {
            if (!EstaActivo || Volatile.Read(ref _cerrado) == 1) return;

            try
            {
                var sb = new StringBuilder();
                sb.Append("Turno: ").AppendLine(turno);
                sb.Append("Acción: ").AppendLine(accion);
                sb.Append("Resultado: ").AppendLine(resultado);
                sb.Append("Hora: ").Append(DateTime.Now.ToString("HH:mm:ss"))
                  .Append(" (t+").Append(_cronometro.Elapsed.ToString(@"hh\:mm\:ss")).AppendLine(")");
                sb.AppendLine();

                _cola.Add(sb.ToString());
            }
            catch (InvalidOperationException)
            {
                // La cola ya se cerró (partida terminando): se descarta el evento.
            }
            catch (Exception)
            {
                // Nunca propagar errores del registro al juego.
            }
        }

        /// <summary>
        /// Devuelve true SOLO la primera vez que se pregunta por una entidad (unidad o
        /// edificio) destruida. Sirve para no registrar dos veces la misma baja.
        /// </summary>
        public bool MarcarDestruccionComoRegistrada(int idEntidad)
        {
            lock (_bloqueoDestrucciones)
            {
                return _destruccionesRegistradas.Add(idEntidad);
            }
        }

        private void BucleEscritura()
        {
            try
            {
                foreach (string bloque in _cola.GetConsumingEnumerable())
                    _escritor.Write(bloque);
            }
            catch (Exception)
            {
                // Disco lleno / archivo cerrado: el hilo termina sin afectar al juego.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _cerrado, 1) == 1) return;

            try
            {
                if (!EstaActivo) return;

                _cola.CompleteAdding();
                _hiloEscritor?.Join(2000); // espera a que se vacíe lo pendiente
                _escritor?.Flush();
                _escritor?.Dispose();
            }
            catch (Exception)
            {
                // ignorar: estamos cerrando
            }
        }
    }
}
