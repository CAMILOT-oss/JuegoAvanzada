using System.Collections.Generic;
using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Punto único de entrada de la capa Vista. El Controlador le entrega la Partida
    /// una vez (Inicializar) y, a partir de ahí, este componente:
    ///   1) Genera la vista del mapa.
    ///   2) En cada frame, revisa las listas de Unidades/Edificios de cada Jugador:
    ///      - si aparece una entidad nueva -> instancia su prefab y la registra.
    ///      - si una entidad ya no está viva/fue destruida -> reproduce la animación y la quita.
    ///      - si sigue viva -> sincroniza su posición/vida visualmente.
    ///
    /// IMPORTANTE (frontera MVC): esta clase solo LEE el Modelo. Jamás debe llamar a
    /// métodos que cambien Jugador, Unidad, Edificio o Partida: eso es responsabilidad
    /// exclusiva del Controlador.
    ///
    /// OJO: el nombre del jugador local NO se configura acá con un campo propio del
    /// Inspector (eso fue un bug real: como GameController tenía SU PROPIO campo
    /// "nombreJugadorLocal" por separado, si los dos textos no coincidían exactamente
    /// el HUD terminaba mostrando los recursos del jugador RIVAL en vez de los tuyos,
    /// y como ambos arrancan con los mismos recursos iniciales, el bug era invisible
    /// hasta que gastabas algo). Ahora GameController es la ÚNICA fuente de verdad:
    /// te lo pasa como parámetro de Inicializar().
    /// </summary>
    public class GameViewManager : MonoBehaviour
    {
        [Header("Referencias de escena")]
        [SerializeField] private MapaView mapaView;
        [SerializeField] private HUDView hudView;
        [SerializeField] private PanelFinPartidaView panelFinPartida;

        [Header("Prefabs de unidades (con UnidadView)")]
        [SerializeField] private GameObject prefabAldeano;
        [SerializeField] private GameObject prefabSoldado;
        [SerializeField] private GameObject prefabArquero;

        [Header("Prefabs de edificios (con EdificioView)")]
        [SerializeField] private GameObject prefabCentroUrbano;
        [SerializeField] private GameObject prefabCuartel;
        [SerializeField] private GameObject prefabAlmacen;

        private Partida _partida;
        private string _nombreJugadorLocal;
        private bool _finDePartidaMostrado;

        private readonly Dictionary<int, UnidadView> _vistasUnidades = new Dictionary<int, UnidadView>();
        private readonly Dictionary<int, EdificioView> _vistasEdificios = new Dictionary<int, EdificioView>();

        /// <summary>
        /// Llamado una vez por el Controlador al arrancar la partida.
        /// "nombreJugadorLocal" tiene que ser EXACTAMENTE el mismo valor que usa
        /// GameController.NombreJugadorLocal (por eso ahora se pasa como parámetro
        /// en vez de configurarse acá aparte, a mano, en el Inspector).
        /// </summary>
        public void Inicializar(Partida partida, string nombreJugadorLocal)
        {
            _partida = partida;
            _nombreJugadorLocal = nombreJugadorLocal;
            _finDePartidaMostrado = false;

            // OJO: "partida.Jugador1" es posicional (el primer argumento que le pasaron
            // a "new Partida(...)"), NO el nombre real "Jugador1". Como en el host tu
            // jugador local se llama "Jugador1" pero en el cliente se llama "Jugador2",
            // asumir "siempre Jugador1" hacía que cada instancia dibujara un mapa
            // generado con una semilla distinta (el mapa del OTRO). Hay que buscar por
            // nombre, igual que ya hace ActualizarHUD más abajo.
            if (mapaView != null)
            {
                Jugador jugadorLocal = partida.Jugador1.Nombre == _nombreJugadorLocal ? partida.Jugador1 : partida.Jugador2;
                mapaView.GenerarVista(jugadorLocal.Mapa);
            }

            // Genera de entrada las entidades iniciales (por ejemplo el Centro Urbano de cada jugador).
            SincronizarJugador(partida.Jugador1);
            SincronizarJugador(partida.Jugador2);
            ActualizarHUD();
        }

        private void Update()
        {
            if (_partida == null) return;

            SincronizarJugador(_partida.Jugador1);
            SincronizarJugador(_partida.Jugador2);
            ActualizarHUD();
            VerificarFinDePartida();
        }

        private void SincronizarJugador(Jugador jugador)
        {
            if (jugador == null) return;

            // --- Unidades ---
            foreach (var unidad in jugador.Unidades)
            {
                if (!unidad.EstaViva)
                {
                    if (_vistasUnidades.TryGetValue(unidad.Id, out var vistaMuerta))
                    {
                        vistaMuerta.Destruir();
                        _vistasUnidades.Remove(unidad.Id);
                    }
                    continue;
                }

                if (_vistasUnidades.TryGetValue(unidad.Id, out var vistaExistente))
                {
                    vistaExistente.SincronizarConModelo();
                }
                else
                {
                    CrearVistaUnidad(unidad);
                }
            }

            // --- Edificios ---
            foreach (var edificio in jugador.Edificios)
            {
                if (edificio.EstaDestruido)
                {
                    if (_vistasEdificios.TryGetValue(edificio.Id, out var vistaMuerta))
                    {
                        vistaMuerta.Destruir();
                        _vistasEdificios.Remove(edificio.Id);
                    }
                    continue;
                }

                if (_vistasEdificios.TryGetValue(edificio.Id, out var vistaExistente))
                {
                    vistaExistente.SincronizarConModelo();
                }
                else
                {
                    CrearVistaEdificio(edificio);
                }
            }
        }

        private void CrearVistaUnidad(Unidad unidad)
        {
            GameObject prefab = unidad switch
            {
                Aldeano => prefabAldeano,
                Soldado => prefabSoldado,
                Arquero => prefabArquero,
                _ => null
            };
            if (prefab == null) return;

            GameObject instancia = Instantiate(prefab, transform);
            UnidadView vista = instancia.GetComponent<UnidadView>();
            if (vista == null) vista = instancia.AddComponent<UnidadView>();

            vista.Vincular(unidad);
            _vistasUnidades[unidad.Id] = vista;
        }

        private void CrearVistaEdificio(Edificio edificio)
        {
            GameObject prefab = edificio switch
            {
                CentroUrbano => prefabCentroUrbano,
                Cuartel => prefabCuartel,
                Almacen => prefabAlmacen,
                _ => null
            };
            if (prefab == null) return;

            GameObject instancia = Instantiate(prefab, transform);
            EdificioView vista = instancia.GetComponent<EdificioView>();
            if (vista == null) vista = instancia.AddComponent<EdificioView>();

            vista.Vincular(edificio);
            _vistasEdificios[edificio.Id] = vista;
        }

        private void ActualizarHUD()
        {
            if (hudView == null) return;

            Jugador local = _partida.Jugador1.Nombre == _nombreJugadorLocal ? _partida.Jugador1 : _partida.Jugador2;
            hudView.ActualizarRecursos(local);
        }

        private void VerificarFinDePartida()
        {
            if (_finDePartidaMostrado || _partida.Estado != EstadoPartida.Finalizada) return;

            _finDePartidaMostrado = true;
            if (panelFinPartida != null)
                panelFinPartida.MostrarResultado(_partida.Ganador, _nombreJugadorLocal);
        }
    }
}