using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using ImperiosEnGuerra.Modelo;
using ImperiosEnGuerra.Vista;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Único MonoBehaviour "cerebro" del juego. Es el intermediario obligatorio entre
    /// la UI/Input (que llama a los métodos públicos "Solicitar..."), el Modelo
    /// (que crea/modifica) y la Vista (a la que solo le pasa la Partida una vez).
    ///
    /// Flujo típico de una acción del jugador local:
    ///   UI hace clic -> GameController.SolicitarMover(...) -> valida -> modifica Modelo
    ///   -> lanza Task en Controlador -> arma AccionJuego -> la manda por red al rival.
    ///
    /// Flujo típico de una acción que llega del rival:
    ///   ControladorRed recibe JSON -> lo encola -> GameController.OnAccionRecibida(...)
    ///   -> vuelve a VALIDAR (nunca confiar en la red) -> aplica sobre el Jugador rival.
    ///
    /// Cada instancia del juego simula TODO por su cuenta (sus unidades y las del rival) y solo
    /// se mandan las ÓRDENES por red. Para que eso funcione, las dos instancias tienen que
    /// coincidir en: el mapa (misma semilla), dónde arranca cada jugador (por NOMBRE, no por
    /// rol local/rival) y cómo se llama cada entidad (IDs determinísticos, ver más abajo).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Vista")]
        [SerializeField] private GameViewManager gameViewManager;

        [Header("Red")]
        [SerializeField] private bool actuarComoHost = true;
        [SerializeField] private string ipHost = "127.0.0.1";
        [SerializeField] private int puerto = 7777;

        [Header("Jugadores")]
        [SerializeField] private string nombreJugadorLocal = "Jugador1";
        [SerializeField] private string nombreJugadorRival = "Jugador2";

        [Header("Archivos de la partida")]
        [Tooltip("Carpeta base donde se guardan configuracion.txt, log_partida.txt y resultado_final.txt. " +
                 "Si está vacía se usa Application.persistentDataPath/Registros. Dentro se crea una subcarpeta " +
                 "por jugador, para que dos instancias en el mismo equipo no se pisen los archivos.")]
        [SerializeField] private string carpetaSalida = "";

        [Header("Generación de mapa")]
        [SerializeField] private int radioZonaSeguraCentroUrbano = 3;
        [Tooltip("Tiene que ser IGUAL en las dos instancias para que vean el mismo mapa.")]
        [SerializeField] private int semillaMapa = 12345;

        private Partida _partida;
        private Jugador _jugadorLocal;
        private Jugador _jugadorRival;

        private readonly ColaPrincipal _colaPrincipal = new ColaPrincipal();
        private ControladorRed _red;

        // Manejo de archivos (.txt). Ver GestorArchivosPartida / RegistroPartida.
        private GestorArchivosPartida _archivos;
        private DetectorEventosPartida _detector;
        private bool _resultadoGuardado;

        /// <summary>Nombre del jugador local, para que la UI filtre qué puede seleccionar/ordenar.</summary>
        public string NombreJugadorLocal => nombreJugadorLocal;

        /// <summary>Acceso de solo lectura al Jugador local (la UI puede leerlo, nunca modificarlo directamente).</summary>
        public Jugador JugadorLocal => _jugadorLocal;

        // Un CancellationTokenSource por unidad, para poder cancelar su tarea actual
        // (movimiento/recolección/ataque) si se le da una orden nueva.
        private readonly Dictionary<int, CancellationTokenSource> _tareasPorUnidad = new Dictionary<int, CancellationTokenSource>();

        // ------------------------------------------------------------------
        //  IDs determinísticos
        //
        // Unidad.Id / Edificio.Id se autogeneran con un contador estático LOCAL a cada
        // proceso: el host y el cliente cuentan distinto, así que el "Id 5" de uno NO es la
        // misma entidad que el "Id 5" del otro. Como por red las órdenes identifican a las
        // unidades por Id (mover, atacar, recolectar), todas caían en la unidad equivocada
        // (o en ninguna) del otro lado. Solución: cada jugador numera SUS entidades dentro
        // de su propio rango, y el Id nuevo viaja en el mensaje para que el otro lado lo use.
        // ------------------------------------------------------------------
        private const int RangoIdsPorJugador = 1_000_000;
        private int _contadorIdsLocal;

        private static int BaseIdsDe(string nombreJugador) =>
            nombreJugador == "Jugador1" ? RangoIdsPorJugador : 2 * RangoIdsPorJugador;

        private int SiguienteIdLocal() => Interlocked.Increment(ref _contadorIdsLocal);

        private async void Start()
        {
            AplicarArgumentosDeLineaDeComandos();

            _jugadorLocal = new Jugador(nombreJugadorLocal);
            _jugadorRival = new Jugador(nombreJugadorRival);
            _partida = new Partida(_jugadorLocal, _jugadorRival);
            _contadorIdsLocal = BaseIdsDe(_jugadorLocal.Nombre);

            ConfigurarInicioDePartida();
            IniciarArchivos(); // configuracion.txt + log_partida.txt

            _red = new ControladorRed(_colaPrincipal);
            _red.AccionRecibida += OnAccionRecibida;
            _red.ConexionPerdida += OnConexionPerdida;

            gameViewManager.Inicializar(_partida, nombreJugadorLocal);
            _partida.IniciarPartida();
            Registrar("Sistema", "Inicio de partida",
                $"Partida en curso - local: {nombreJugadorLocal}, rival: {nombreJugadorRival}");

            try
            {
                if (actuarComoHost)
                {
                    Registrar("Sistema", "Conexión", $"Esperando al rival en el puerto {puerto}");
                    await _red.EscucharAsync(puerto);
                }
                else
                {
                    Registrar("Sistema", "Conexión", $"Conectando con el host {ipHost}:{puerto}");
                    await _red.ConectarAsync(ipHost, puerto);
                }
                Registrar("Sistema", "Conexión", "Establecida - los dos jugadores están conectados");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo establecer la conexión de red: {e.Message}");
                Registrar("Sistema", "Conexión", $"Error - no se pudo establecer la conexión: {e.Message}");
            }
        }

        // ------------------------------------------------------------------
        //  Manejo de archivos (configuracion.txt / log_partida.txt / resultado_final.txt)
        // ------------------------------------------------------------------

        /// <summary>Crea la carpeta de salida, escribe configuracion.txt y deja listo el registro.
        /// Cualquier fallo de disco solo produce una advertencia: el juego sigue normalmente.</summary>
        private void IniciarArchivos()
        {
            try
            {
                _archivos = new GestorArchivosPartida(ResolverCarpetaSalida());

                if (!_archivos.Registro.EstaActivo)
                    Debug.LogWarning($"No se pudo abrir {GestorArchivosPartida.NombreLog}: {_archivos.Registro.ErrorInicial}");

                string error = _archivos.GuardarConfiguracion(_partida, new ParametrosPartida
                {
                    NombreJugadorLocal = nombreJugadorLocal,
                    SemillaMapa = semillaMapa,
                    RadioZonaSegura = radioZonaSeguraCentroUrbano,
                    EsHost = actuarComoHost,
                    IpHost = ipHost,
                    Puerto = puerto
                });

                if (error != null)
                {
                    Debug.LogWarning($"No se pudo guardar {GestorArchivosPartida.NombreConfiguracion}: {error}");
                    Registrar("Sistema", "Configuración inicial", $"Error al guardar: {error}");
                }
                else
                {
                    Registrar("Sistema", "Configuración inicial",
                        $"Guardada en {GestorArchivosPartida.NombreConfiguracion} (semilla {semillaMapa}, mapa {Mapa.TamanoPorDefecto}x{Mapa.TamanoPorDefecto})");
                }

                _detector = new DetectorEventosPartida(_archivos.Registro, _partida);

                ControladorCombate.UnidadDestruida += OnUnidadDestruida;
                ControladorCombate.EdificioDestruido += OnEdificioDestruido;

                Debug.Log($"[Archivos] Los .txt de la partida se guardan en: {_archivos.Carpeta}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo inicializar el manejo de archivos: {e.Message}");
            }
        }

        private string ResolverCarpetaSalida()
        {
            string baseDir = string.IsNullOrWhiteSpace(carpetaSalida)
                ? Path.Combine(Application.persistentDataPath, "Registros")
                : carpetaSalida;

            string nombreSeguro = nombreJugadorLocal;
            foreach (char invalido in Path.GetInvalidFileNameChars())
                nombreSeguro = nombreSeguro.Replace(invalido, '_');

            return Path.Combine(baseDir, nombreSeguro);
        }

        /// <summary>Anota un evento en log_partida.txt. Seguro desde cualquier hilo; no hace nada si el registro no está activo.</summary>
        private void Registrar(string turno, string accion, string resultado)
        {
            _archivos?.Registro.Registrar(turno, accion, resultado);
        }

        /// <summary>Se ejecuta cuando la partida termina: anota el fin y guarda resultado_final.txt (una sola vez).</summary>
        private void FinalizarArchivos()
        {
            _resultadoGuardado = true;
            if (_archivos == null) return;

            Registrar("Sistema", "Fin de partida", $"Ganador: {_partida.Ganador}");

            string error = _archivos.GuardarResultadoFinal(_partida, nombreJugadorLocal);
            if (error != null)
                Debug.LogWarning($"No se pudo guardar {GestorArchivosPartida.NombreResultado}: {error}");
            else
                Debug.Log($"[Archivos] Resultado final guardado en: {Path.Combine(_archivos.Carpeta, GestorArchivosPartida.NombreResultado)}");
        }

        // Estos dos métodos los llaman los hilos de combate (Tasks), por eso solo tocan el registro,
        // que es seguro entre hilos.
        private void OnUnidadDestruida(Unidad atacante, Unidad objetivo)
        {
            var archivos = _archivos;
            if (archivos == null || !archivos.Registro.MarcarDestruccionComoRegistrada(objetivo.Id)) return;

            archivos.Registro.Registrar(atacante.Propietario, "Ataque",
                $"Impacto - Unidad enemiga destruida: {objetivo.Nombre} de {objetivo.Propietario} (Id {objetivo.Id}) " +
                $"abatida por {atacante.Nombre} (Id {atacante.Id})");
        }

        private void OnEdificioDestruido(Unidad atacante, Edificio objetivo)
        {
            var archivos = _archivos;
            if (archivos == null || !archivos.Registro.MarcarDestruccionComoRegistrada(objetivo.Id)) return;

            archivos.Registro.Registrar(atacante.Propietario, "Ataque a edificio",
                $"Impacto - Edificio enemigo destruido: {objetivo.Nombre} de {objetivo.Propietario} (Id {objetivo.Id}) " +
                $"por {atacante.Nombre} (Id {atacante.Id})");
        }

        private void OnConexionPerdida()
        {
            Registrar("Sistema", "Conexión", "Perdida - el rival se desconectó o hubo un error de comunicación");
            Debug.LogWarning("Se perdió la conexión con el rival.");
        }

        private static string NombreAccion(TipoAccion tipo) => tipo switch
        {
            TipoAccion.MoverUnidad => "Movimiento",
            TipoAccion.Atacar => "Ataque",
            TipoAccion.AtacarEdificio => "Ataque a edificio",
            TipoAccion.Construir => "Construcción",
            TipoAccion.EntrenarUnidad => "Entrenamiento",
            TipoAccion.Recolectar => "Recolección",
            _ => tipo.ToString()
        };

        /// <summary>Texto legible de una acción recibida del rival (para log_partida.txt).</summary>
        private static string DescribirAccionRemota(AccionJuego a) => a.Tipo switch
        {
            TipoAccion.MoverUnidad => $"unidad {a.IdentificadorEntidad} de {a.Origen} a {a.Destino}",
            TipoAccion.Atacar => $"unidad {a.IdentificadorAtacante} ataca a la unidad {a.IdentificadorEntidad} en {a.Destino}",
            TipoAccion.AtacarEdificio => $"unidad {a.IdentificadorAtacante} asedia el edificio {a.IdentificadorEntidad} en {a.Destino}",
            TipoAccion.Construir => $"{a.IdentificadorEntidad} en {a.Destino} (Id nuevo {a.IdentificadorNuevaEntidad})",
            TipoAccion.EntrenarUnidad => $"{a.IdentificadorEntidad} desde el edificio en {a.Destino} (Id nuevo {a.IdentificadorNuevaEntidad})",
            TipoAccion.Recolectar => $"unidad {a.IdentificadorEntidad} hacia el depósito en {a.Destino}",
            _ => a.Tipo.ToString()
        };

        /// <summary>
        /// Permite lanzar el mismo build como host o como cliente sin tener que recompilar:
        /// los argumentos de línea de comandos pisan los valores configurados en el Inspector.
        /// Ejemplos: "--host", "--cliente", "--ip=192.168.1.5", "--puerto=7777",
        /// "--local=Jugador2", "--rival=Jugador1", "--salida=C:\\ruta\\de\\archivos".
        /// </summary>
        private void AplicarArgumentosDeLineaDeComandos()
        {
            string[] args = Environment.GetCommandLineArgs();

            foreach (string arg in args)
            {
                if (arg.Equals("--host", StringComparison.OrdinalIgnoreCase))
                    actuarComoHost = true;
                else if (arg.Equals("--cliente", StringComparison.OrdinalIgnoreCase))
                    actuarComoHost = false;
                else if (arg.StartsWith("--ip=", StringComparison.OrdinalIgnoreCase))
                    ipHost = arg.Substring("--ip=".Length);
                else if (arg.StartsWith("--puerto=", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(arg.Substring("--puerto=".Length), out int puertoArg))
                    puerto = puertoArg;
                else if (arg.StartsWith("--local=", StringComparison.OrdinalIgnoreCase))
                    nombreJugadorLocal = arg.Substring("--local=".Length);
                else if (arg.StartsWith("--rival=", StringComparison.OrdinalIgnoreCase))
                    nombreJugadorRival = arg.Substring("--rival=".Length);
                else if (arg.StartsWith("--salida=", StringComparison.OrdinalIgnoreCase))
                    carpetaSalida = arg.Substring("--salida=".Length);
            }
        }

        private void Update()
        {
            _colaPrincipal.ProcesarPendientes();
            _detector?.Revisar(_partida);

            if (_partida.VerificarGanador() && !_resultadoGuardado)
                FinalizarArchivos();
        }

        private void OnDestroy()
        {
            _red?.Dispose();
            foreach (var cts in _tareasPorUnidad.Values) cts.Cancel();

            ControladorCombate.UnidadDestruida -= OnUnidadDestruida;
            ControladorCombate.EdificioDestruido -= OnEdificioDestruido;

            if (!_resultadoGuardado)
                Registrar("Sistema", "Cierre", "La aplicación se cerró antes de que terminara la partida");
            _archivos?.Dispose(); // vacía la cola pendiente y cierra log_partida.txt
        }

        /// <summary>
        /// Dónde arranca cada jugador, según su NOMBRE (nunca según sea "local" o "rival":
        /// ese rol se invierte entre el host y el cliente). "Jugador1" arriba a la izquierda,
        /// cualquier otro abajo a la derecha (para un Centro Urbano de 2x2 en un mapa de 15x15).
        /// </summary>
        private static Coordenada EsquinaInicialDe(string nombreJugador) =>
            nombreJugador == "Jugador1"
                ? new Coordenada(0, 0)
                : new Coordenada(Mapa.TamanoPorDefecto - 2, Mapa.TamanoPorDefecto - 2);

        private static void ColocarCentroUrbanoInicial(Jugador jugador)
        {
            Coordenada esquina = EsquinaInicialDe(jugador.Nombre);

            var centro = new CentroUrbano(jugador.Nombre, esquina);
            centro.AsignarId(BaseIdsDe(jugador.Nombre)); // mismo Id en las dos instancias
            jugador.Mapa.ColocarEdificio(centro, esquina);
            jugador.AgregarEdificio(centro);

            jugador.AgregarRecurso(TipoRecurso.Comida, 200);
            jugador.AgregarRecurso(TipoRecurso.Madera, 150);
        }

        /// <summary>Coloca el Centro Urbano inicial de cada jugador y genera los yacimientos.</summary>
        private void ConfigurarInicioDePartida()
        {
            ColocarCentroUrbanoInicial(_jugadorLocal);
            ColocarCentroUrbanoInicial(_jugadorRival);

            // IMPORTANTE: los dos Mapas (el del jugador local y el del rival) se generan con la
            // MISMA semilla y las MISMAS zonas seguras (las dos esquinas). Antes cada Mapa usaba
            // una semilla distinta (derivada del nombre de su jugador), y como cada instancia
            // dibuja el Mapa de su jugador local, el host y el cliente veían terrenos distintos
            // y los yacimientos que clickeaba uno no existían del lado del otro.
            var zonasSeguras = new (Coordenada centro, int radio)[]
            {
                (EsquinaInicialDe("Jugador1"), radioZonaSeguraCentroUrbano),
                (EsquinaInicialDe("Jugador2"), radioZonaSeguraCentroUrbano)
            };

            GeneradorMapa.Generar(_jugadorLocal.Mapa, semillaMapa, zonasSeguras: zonasSeguras);
            GeneradorMapa.Generar(_jugadorRival.Mapa, semillaMapa, zonasSeguras: zonasSeguras);
        }

        // ------------------------------------------------------------------
        //  API pública para la UI / Input (acciones del jugador LOCAL)
        // ------------------------------------------------------------------

        public void SolicitarMover(Unidad unidad, Coordenada destino)
        {
            if (unidad.Propietario != _jugadorLocal.Nombre) return;
            if (!ValidadorAcciones.PuedeMoverse(unidad, destino, _jugadorLocal.Mapa))
            {
                Registrar(_jugadorLocal.Nombre, "Movimiento",
                    $"Rechazada - destino {destino} no válido para {unidad.Nombre} (Id {unidad.Id})");
                return;
            }

            Coordenada origenMovimiento = unidad.Posicion;
            var cts = ReiniciarTareaDeUnidad(unidad.Id);
            _ = ControladorMovimiento.MoverAsync(unidad, destino, _jugadorLocal.Mapa, cts.Token);

            Registrar(_jugadorLocal.Nombre, "Movimiento",
                $"Orden aceptada - {unidad.Nombre} (Id {unidad.Id}) de {origenMovimiento} a {destino}");
            EnviarAccion(TipoAccion.MoverUnidad, unidad.Id.ToString(), unidad.Posicion, destino);
        }

        /// <summary>Manda a "atacante" a perseguir y atacar a "objetivo" (aunque esté lejos).</summary>
        public void SolicitarAtacar(Unidad atacante, Unidad objetivo)
        {
            if (atacante == null || objetivo == null) return;
            if (atacante.Propietario != _jugadorLocal.Nombre) return;
            if (!ValidadorAcciones.PuedeOrdenarAtaque(atacante, objetivo))
            {
                Registrar(_jugadorLocal.Nombre, "Ataque",
                    $"Rechazada - {atacante.Nombre} (Id {atacante.Id}) no puede atacar a {objetivo.Nombre} (Id {objetivo.Id})");
                return;
            }

            var cts = ReiniciarTareaDeUnidad(atacante.Id);
            _ = ControladorCombate.AtacarAsync(atacante, objetivo, _jugadorLocal.Mapa, cts.Token);

            Registrar(_jugadorLocal.Nombre, "Ataque",
                $"Orden aceptada - {atacante.Nombre} (Id {atacante.Id}) ataca a {objetivo.Nombre} de {objetivo.Propietario} (Id {objetivo.Id})");
            EnviarAccion(TipoAccion.Atacar, objetivo.Id.ToString(), atacante.Posicion, objetivo.Posicion, atacante.Id.ToString());
        }

        /// <summary>Ataca un EDIFICIO rival (asedio). "atacante" tiene que ser una unidad
        /// del jugador local; "objetivo" un edificio que NO le pertenezca. Si está lejos,
        /// la unidad se acerca sola.</summary>
        public void SolicitarAtacarEdificio(Unidad atacante, Edificio objetivo)
        {
            if (atacante == null || objetivo == null) return;
            if (atacante.Propietario != _jugadorLocal.Nombre) return;
            if (objetivo.Propietario == _jugadorLocal.Nombre) return; // no fuego amigo
            if (!atacante.EstaViva || objetivo.EstaDestruido)
            {
                Registrar(_jugadorLocal.Nombre, "Ataque a edificio",
                    $"Rechazada - {atacante.Nombre} (Id {atacante.Id}) no puede atacar a {objetivo.Nombre} (Id {objetivo.Id})");
                return;
            }

            var cts = ReiniciarTareaDeUnidad(atacante.Id);
            _ = ControladorCombate.AtacarEdificioAsync(atacante, objetivo, _jugadorLocal.Mapa, cts.Token);

            Registrar(_jugadorLocal.Nombre, "Ataque a edificio",
                $"Orden aceptada - {atacante.Nombre} (Id {atacante.Id}) asedia {objetivo.Nombre} de {objetivo.Propietario} (Id {objetivo.Id})");
            EnviarAccion(TipoAccion.AtacarEdificio, objetivo.Id.ToString(), atacante.Posicion, objetivo.Posicion, atacante.Id.ToString());
        }

        public void SolicitarConstruir(string tipoEdificio, Coordenada posicion)
        {
            var costo = ObtenerCostoEdificio(tipoEdificio);
            if (costo == null) return;

            var (ancho, alto) = CostosJuego.TamanoEdificio.GetValueOrDefault(tipoEdificio, (1, 1));

            int idNuevo = SiguienteIdLocal();
            Edificio nuevo = ControladorConstruccion.IniciarConstruccion(
                _jugadorLocal,
                (owner, pos) => CrearEdificio(tipoEdificio, owner, pos, idNuevo),
                costo,
                posicion,
                out _,
                ancho, alto);

            if (nuevo != null)
            {
                Registrar(_jugadorLocal.Nombre, "Construcción",
                    $"Iniciada - {tipoEdificio} (Id {idNuevo}) en {posicion}, costo {Costo(costo)}, tiempo {nuevo.TiempoConstruccionSegundos} s");
                EnviarAccion(TipoAccion.Construir, tipoEdificio, posicion, posicion,
                    identificadorNuevaEntidad: idNuevo.ToString());
            }
            else
            {
                Registrar(_jugadorLocal.Nombre, "Construcción",
                    $"Rechazada - {tipoEdificio} en {posicion}: recursos insuficientes o casilla no válida");
            }
        }

        public void SolicitarEntrenar(string tipoUnidad, Edificio edificioEntrenador)
        {
            var costo = ObtenerCostoUnidad(tipoUnidad);
            int tiempo = ObtenerTiempoEntrenamiento(tipoUnidad);
            if (costo == null || edificioEntrenador == null) return;

            int idNuevo = SiguienteIdLocal();
            bool iniciado = ControladorEntrenamiento.IniciarEntrenamiento(
                _jugadorLocal, edificioEntrenador,
                (owner, pos) => CrearUnidad(tipoUnidad, owner, pos, idNuevo),
                costo, tiempo, idNuevo, out _);

            if (iniciado)
            {
                Registrar(_jugadorLocal.Nombre, "Entrenamiento",
                    $"Iniciado - {tipoUnidad} (Id {idNuevo}) en {edificioEntrenador.Nombre} (Id {edificioEntrenador.Id}), " +
                    $"costo {Costo(costo)}, tiempo {tiempo} s");
                EnviarAccion(TipoAccion.EntrenarUnidad, tipoUnidad,
                    edificioEntrenador.Posicion, edificioEntrenador.Posicion,
                    identificadorNuevaEntidad: idNuevo.ToString());
            }
            else
            {
                Registrar(_jugadorLocal.Nombre, "Entrenamiento",
                    $"Rechazado - {tipoUnidad} en {edificioEntrenador.Nombre}: recursos insuficientes o edificio no disponible");
            }
        }

        /// <summary>Manda a un Aldeano a recolectar de "deposito" en loop (ir, extraer, volver,
        /// descargar, repetir) hasta que el depósito se agote o le des otra orden. Entrega
        /// automáticamente en el Centro Urbano/Almacén propio más cercano al depósito.</summary>
        public void SolicitarRecolectar(Unidad unidad, DepositoRecurso deposito)
        {
            if (unidad == null || deposito == null) return;
            if (unidad.Propietario != _jugadorLocal.Nombre) return;
            if (!(unidad is Aldeano aldeano)) return; // solo los Aldeanos recolectan
            if (deposito.EstaAgotado)
            {
                Registrar(_jugadorLocal.Nombre, "Recolección", $"Rechazada - el depósito de {deposito.Tipo} en {deposito.Posicion} está agotado");
                return;
            }

            Edificio puntoEntrega = ObtenerEdificioDeEntregaMasCercano(_jugadorLocal, deposito.Posicion);
            if (puntoEntrega == null) // no hay Centro Urbano ni Almacén en pie para entregar
            {
                Registrar(_jugadorLocal.Nombre, "Recolección", "Rechazada - no hay Centro Urbano ni Almacén en pie donde entregar");
                return;
            }

            var cts = ReiniciarTareaDeUnidad(unidad.Id);
            _ = ControladorRecoleccion.RecolectarAsync(aldeano, deposito, _jugadorLocal, puntoEntrega.Posicion, cts.Token);

            Registrar(_jugadorLocal.Nombre, "Recolección",
                $"Orden aceptada - {unidad.Nombre} (Id {unidad.Id}) recolecta {deposito.Tipo} en {deposito.Posicion}, entrega en {puntoEntrega.Nombre} {puntoEntrega.Posicion}");

            EnviarAccion(TipoAccion.Recolectar, unidad.Id.ToString(), unidad.Posicion, deposito.Posicion);
        }

        // ------------------------------------------------------------------
        //  Acciones que llegan del RIVAL por red
        //
        //  Cada una arranca en ESTA instancia la misma simulación que el rival arrancó en la suya
        //  (mover, perseguir/atacar, recolectar...), para que las dos vean lo mismo.
        // ------------------------------------------------------------------

        private void OnAccionRecibida(AccionJuego accion)
        {
            Registrar(accion.JugadorOrigen ?? nombreJugadorRival, NombreAccion(accion.Tipo),
                $"Orden recibida del rival por red - {DescribirAccionRemota(accion)}");

            switch (accion.Tipo)
            {
                case TipoAccion.MoverUnidad:
                    AplicarMovimientoRemoto(accion);
                    break;
                case TipoAccion.Atacar:
                    AplicarAtaqueRemoto(accion);
                    break;
                case TipoAccion.AtacarEdificio:
                    AplicarAtaqueEdificioRemoto(accion);
                    break;
                case TipoAccion.Construir:
                    AplicarConstruccionRemota(accion);
                    break;
                case TipoAccion.EntrenarUnidad:
                    AplicarEntrenamientoRemoto(accion);
                    break;
                case TipoAccion.Recolectar:
                    AplicarRecoleccionRemota(accion);
                    break;
            }
        }

        private void AplicarMovimientoRemoto(AccionJuego accion)
        {
            if (!int.TryParse(accion.IdentificadorEntidad, out int idUnidad)) return;

            Unidad unidad = BuscarUnidad(_jugadorRival, idUnidad);
            if (unidad == null || !ValidadorAcciones.PuedeMoverse(unidad, accion.Destino, _jugadorRival.Mapa)) return;

            var cts = ReiniciarTareaDeUnidad(unidad.Id);
            _ = ControladorMovimiento.MoverAsync(unidad, accion.Destino, _jugadorRival.Mapa, cts.Token);
        }

        private void AplicarAtaqueRemoto(AccionJuego accion)
        {
            if (!int.TryParse(accion.IdentificadorAtacante, out int idAtacante)) return;
            if (!int.TryParse(accion.IdentificadorEntidad, out int idObjetivo)) return;

            Unidad atacante = BuscarUnidad(_jugadorRival, idAtacante);
            Unidad objetivo = BuscarUnidad(_jugadorLocal, idObjetivo);
            if (atacante == null || objetivo == null) return;

            // Revalidamos de nuevo acá (vivos, dueños distintos): nunca hay que confiar en que
            // el que mandó la acción ya la validó bien de su lado. El daño lo pone el atacante REAL.
            if (!ValidadorAcciones.PuedeOrdenarAtaque(atacante, objetivo)) return;

            var cts = ReiniciarTareaDeUnidad(atacante.Id);
            _ = ControladorCombate.AtacarAsync(atacante, objetivo, _jugadorRival.Mapa, cts.Token);
        }

        private void AplicarAtaqueEdificioRemoto(AccionJuego accion)
        {
            if (!int.TryParse(accion.IdentificadorAtacante, out int idAtacante)) return;
            if (!int.TryParse(accion.IdentificadorEntidad, out int idEdificio)) return;

            Unidad atacante = BuscarUnidad(_jugadorRival, idAtacante);
            Edificio objetivo = BuscarEdificio(_jugadorLocal, idEdificio);
            if (atacante == null || objetivo == null) return;
            if (!atacante.EstaViva || objetivo.EstaDestruido) return;
            if (objetivo.Propietario == atacante.Propietario) return; // no fuego amigo

            var cts = ReiniciarTareaDeUnidad(atacante.Id);
            _ = ControladorCombate.AtacarEdificioAsync(atacante, objetivo, _jugadorRival.Mapa, cts.Token);
        }

        private void AplicarConstruccionRemota(AccionJuego accion)
        {
            var costo = ObtenerCostoEdificio(accion.IdentificadorEntidad);
            if (costo == null) return;
            if (!int.TryParse(accion.IdentificadorNuevaEntidad, out int idNuevo)) return;

            var (ancho, alto) = CostosJuego.TamanoEdificio.GetValueOrDefault(accion.IdentificadorEntidad, (1, 1));

            AsegurarRecursosDelRival(costo);

            ControladorConstruccion.IniciarConstruccion(
                _jugadorRival,
                (owner, pos) => CrearEdificio(accion.IdentificadorEntidad, owner, pos, idNuevo),
                costo, accion.Destino, out _, ancho, alto);
        }

        private void AplicarEntrenamientoRemoto(AccionJuego accion)
        {
            var costo = ObtenerCostoUnidad(accion.IdentificadorEntidad);
            int tiempo = ObtenerTiempoEntrenamiento(accion.IdentificadorEntidad);
            if (costo == null) return;
            if (!int.TryParse(accion.IdentificadorNuevaEntidad, out int idNuevo)) return;

            Edificio entrenador = _jugadorRival.Mapa.ObtenerCasilla(accion.Destino)?.Edificio;
            if (entrenador == null || entrenador.Propietario != _jugadorRival.Nombre) return;

            // El emisor ya validó que su edificio estaba terminado. Nuestra copia puede ir unos
            // milisegundos atrasada (su obra arrancó cuando llegó el mensaje de construir), así
            // que no rechazamos la orden por eso.
            entrenador.EstaConstruido = true;

            AsegurarRecursosDelRival(costo);

            ControladorEntrenamiento.IniciarEntrenamiento(
                _jugadorRival, entrenador,
                (owner, pos) => CrearUnidad(accion.IdentificadorEntidad, owner, pos, idNuevo),
                costo, tiempo, idNuevo, out _);
        }

        private void AplicarRecoleccionRemota(AccionJuego accion)
        {
            if (!int.TryParse(accion.IdentificadorEntidad, out int idUnidad)) return;

            Unidad unidad = BuscarUnidad(_jugadorRival, idUnidad);
            if (!(unidad is Aldeano aldeano)) return;

            var deposito = _jugadorRival.Mapa.ObtenerCasilla(accion.Destino)?.Recurso;
            if (deposito == null || deposito.EstaAgotado) return;

            Edificio puntoEntrega = ObtenerEdificioDeEntregaMasCercano(_jugadorRival, deposito.Posicion);
            if (puntoEntrega == null) return;

            var cts = ReiniciarTareaDeUnidad(unidad.Id);
            _ = ControladorRecoleccion.RecolectarAsync(aldeano, deposito, _jugadorRival, puntoEntrega.Posicion, cts.Token);
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Nuestra copia de la economía del rival nunca va a ser EXACTAMENTE igual a la real
        /// (los viajes de recolección se simulan en cada instancia con relojes distintos, así
        /// que difieren en unos pocos recursos). Si la copia rechazara una compra del rival por
        /// faltarle 5 de oro, esa unidad/edificio nunca aparecería de nuestro lado. El emisor ya
        /// validó SU economía real antes de mandar el mensaje, así que acá completamos lo que
        /// falte. Esto solo afecta a la copia del rival, jamás a tus propios recursos.
        /// </summary>
        private void AsegurarRecursosDelRival(Dictionary<TipoRecurso, int> costo)
        {
            foreach (var par in costo)
            {
                int falta = par.Value - _jugadorRival.ObtenerRecurso(par.Key);
                if (falta > 0)
                    _jugadorRival.AgregarRecurso(par.Key, falta);
            }
        }

        private void EnviarAccion(TipoAccion tipo, string identificador, Coordenada origen, Coordenada destino,
            string identificadorAtacante = null, string identificadorNuevaEntidad = null)
        {
            var accion = new AccionJuego
            {
                Tipo = tipo,
                JugadorOrigen = _jugadorLocal.Nombre,
                Origen = origen,
                Destino = destino,
                IdentificadorEntidad = identificador,
                IdentificadorAtacante = identificadorAtacante,
                IdentificadorNuevaEntidad = identificadorNuevaEntidad
            };
            _ = _red.EnviarAccionAsync(accion);
        }

        private static string Costo(Dictionary<TipoRecurso, int> costo)
        {
            var partes = new List<string>();
            foreach (var par in costo) partes.Add($"{par.Value} {par.Key}");
            return string.Join(", ", partes);
        }

        private CancellationTokenSource ReiniciarTareaDeUnidad(int unidadId)
        {
            if (_tareasPorUnidad.TryGetValue(unidadId, out var anterior))
                anterior.Cancel();

            var nueva = new CancellationTokenSource();
            _tareasPorUnidad[unidadId] = nueva;
            return nueva;
        }

        private static Unidad BuscarUnidad(Jugador jugador, int id)
        {
            foreach (var unidad in jugador.Unidades)
                if (unidad.Id == id) return unidad;
            return null;
        }

        private static Edificio BuscarEdificio(Jugador jugador, int id)
        {
            foreach (var edificio in jugador.Edificios)
                if (edificio.Id == id) return edificio;
            return null;
        }

        /// <summary>Busca, entre los edificios EN PIE de "jugador", el CentroUrbano o Almacén
        /// más cercano a "desde" (por distancia de Chebyshev) — ahí es donde un Aldeano
        /// entrega lo que recolectó.</summary>
        private static Edificio ObtenerEdificioDeEntregaMasCercano(Jugador jugador, Coordenada desde)
        {
            Edificio masCercano = null;
            int mejorDistancia = int.MaxValue;

            foreach (var edificio in jugador.Edificios)
            {
                if (!edificio.EstaConstruido || edificio.EstaDestruido) continue;
                if (!(edificio is CentroUrbano || edificio is Almacen)) continue;

                int distancia = edificio.Posicion.DistanciaA(desde);
                if (distancia < mejorDistancia)
                {
                    mejorDistancia = distancia;
                    masCercano = edificio;
                }
            }

            return masCercano;
        }

        private static Unidad CrearUnidad(string tipo, string propietario, Coordenada posicion, int id)
        {
            Unidad unidad = tipo switch
            {
                "Aldeano" => new Aldeano(propietario, posicion),
                "Soldado" => new Soldado(propietario, posicion),
                "Arquero" => new Arquero(propietario, posicion),
                _ => null
            };
            unidad?.AsignarId(id);
            return unidad;
        }

        private static Edificio CrearEdificio(string tipo, string propietario, Coordenada posicion, int id)
        {
            Edificio edificio = tipo switch
            {
                "Cuartel" => new Cuartel(propietario, posicion),
                "Almacen" => new Almacen(propietario, posicion),
                "CentroUrbano" => new CentroUrbano(propietario, posicion),
                _ => null
            };
            edificio?.AsignarId(id);
            return edificio;
        }

        private static Dictionary<TipoRecurso, int> ObtenerCostoUnidad(string tipo) => tipo switch
        {
            "Aldeano" => CostosJuego.CostoAldeano,
            "Soldado" => CostosJuego.CostoSoldado,
            "Arquero" => CostosJuego.CostoArquero,
            _ => null
        };

        private static Dictionary<TipoRecurso, int> ObtenerCostoEdificio(string tipo) => tipo switch
        {
            "Cuartel" => CostosJuego.CostoCuartel,
            "Almacen" => CostosJuego.CostoAlmacen,
            _ => null
        };

        private static int ObtenerTiempoEntrenamiento(string tipo) => tipo switch
        {
            "Aldeano" => CostosJuego.TiempoEntrenamientoAldeanoSegundos,
            "Soldado" => CostosJuego.TiempoEntrenamientoSoldadoSegundos,
            "Arquero" => CostosJuego.TiempoEntrenamientoArqueroSegundos,
            _ => 0
        };
    }
}