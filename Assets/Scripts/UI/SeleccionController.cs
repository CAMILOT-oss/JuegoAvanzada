using System;
using UnityEngine;
using UnityEngine.EventSystems;
using ImperiosEnGuerra.Modelo;
using ImperiosEnGuerra.Vista;
using ImperiosEnGuerra.Controlador;

namespace ImperiosEnGuerra.UI
{
    /// <summary>
    /// Traduce clics del mouse en llamadas al GameController. Es la puerta de entrada
    /// de la UI: nunca toca el Modelo directamente, todo pasa por gameController.Solicitar...().
    ///
    /// Requisito en los prefabs: cada unidad/edificio necesita un Collider2D
    /// (por ejemplo un BoxCollider2D del tamaño del sprite) para que Physics2D.OverlapPoint
    /// pueda detectarlo al hacer clic.
    ///
    /// Controles:
    ///   - Clic izquierdo sobre una unidad/edificio propio -> lo selecciona.
    ///   - Clic izquierdo en modo "colocando edificio" -> construye ahí y vuelve a modo normal.
    ///   - Clic derecho con una unidad propia seleccionada, sobre una unidad enemiga -> atacar.
    ///   - Clic derecho con una unidad propia seleccionada, sobre un edificio enemigo -> asediarlo.
    ///   - Clic derecho con un Aldeano seleccionado, sobre una casilla con un depósito de
    ///     recurso -> recolectar (en loop, hasta agotarlo o darle otra orden).
    ///   - Clic derecho con una unidad propia seleccionada, sobre el suelo -> moverse ahí.
    /// </summary>
    public class SeleccionController : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private IndicadorSeleccion indicadorSeleccion;
        [SerializeField] private LayerMask capaSeleccionable = ~0; // por defecto, todas las capas

        public UnidadView UnidadSeleccionada { get; private set; }
        public EdificioView EdificioSeleccionado { get; private set; }
        public ModoInteraccion Modo { get; private set; } = ModoInteraccion.Normal;

        /// <summary>Se dispara cuando cambia la unidad seleccionada (o se deselecciona, con null).</summary>
        public event Action<UnidadView> SeleccionUnidadCambio;

        /// <summary>Se dispara cuando cambia el edificio seleccionado (o se deselecciona, con null).</summary>
        public event Action<EdificioView> SeleccionEdificioCambio;

        private string _tipoEdificioAConstruir;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) ManejarClicIzquierdo();
            if (Input.GetMouseButtonDown(1)) ManejarClicDerecho();
        }

        /// <summary>
        /// True si el clic actual cayó sobre un elemento de UI (un Button, un Panel, etc.)
        /// en vez de sobre el mundo del juego. Hay que chequear esto SIEMPRE antes de
        /// interpretar un clic como "seleccionar/construir/mover/atacar", porque si no,
        /// cualquier botón de la UI (Entrenar, Construir...) va a competir con la
        /// selección del mundo en el mismo frame y va a comportarse de forma errática.
        /// </summary>
        private bool ClicSobreUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void ManejarClicIzquierdo()
        {
            if (ClicSobreUI()) return; // el clic fue sobre un botón/panel de la UI, no sobre el mapa

            Vector3 posicionMundo = ObtenerPosicionMundoDelMouse();

            if (Modo == ModoInteraccion.ColocandoEdificio)
            {
                Coordenada coordenada = ConversorCoordenadas.ACoordenada(posicionMundo);
                gameController.SolicitarConstruir(_tipoEdificioAConstruir, coordenada);
                CancelarModoColocacion();
                return;
            }

            Collider2D impacto = Physics2D.OverlapPoint(posicionMundo, capaSeleccionable);
            if (impacto == null)
            {
                LimpiarSeleccion();
                return;
            }

            var unidadView = impacto.GetComponentInParent<UnidadView>();
            var edificioView = impacto.GetComponentInParent<EdificioView>();

            if (unidadView != null && unidadView.UnidadModelo.Propietario == gameController.NombreJugadorLocal)
                Seleccionar(unidadView);
            else if (edificioView != null && edificioView.EdificioModelo.Propietario == gameController.NombreJugadorLocal)
                Seleccionar(edificioView);
            else
                LimpiarSeleccion();
        }

        private void ManejarClicDerecho()
        {
            if (ClicSobreUI()) return; // el clic fue sobre un botón/panel de la UI, no sobre el mapa
            if (UnidadSeleccionada == null) return; // solo unidades reciben órdenes de movimiento/ataque

            Vector3 posicionMundo = ObtenerPosicionMundoDelMouse();
            Collider2D impacto = Physics2D.OverlapPoint(posicionMundo, capaSeleccionable);

            if (impacto != null)
            {
                var unidadObjetivo = impacto.GetComponentInParent<UnidadView>();
                if (unidadObjetivo != null && unidadObjetivo.UnidadModelo.Propietario != gameController.NombreJugadorLocal)
                {
                    gameController.SolicitarAtacar(UnidadSeleccionada.UnidadModelo, unidadObjetivo.UnidadModelo);
                    return;
                }

                var edificioObjetivo = impacto.GetComponentInParent<EdificioView>();
                if (edificioObjetivo != null && edificioObjetivo.EdificioModelo.Propietario != gameController.NombreJugadorLocal)
                {
                    gameController.SolicitarAtacarEdificio(UnidadSeleccionada.UnidadModelo, edificioObjetivo.EdificioModelo);
                    return;
                }
            }

            Coordenada destino = ConversorCoordenadas.ACoordenada(posicionMundo);

            // Si es un Aldeano y clickeaste una casilla con un depósito de recurso (todavía no
            // agotado), lo mandamos a recolectar en vez de simplemente pararse ahí.
            if (UnidadSeleccionada.UnidadModelo is Aldeano)
            {
                Casilla casillaDestino = gameController.JugadorLocal.Mapa.ObtenerCasilla(destino);
                DepositoRecurso deposito = casillaDestino?.Recurso;
                if (deposito != null && !deposito.EstaAgotado)
                {
                    gameController.SolicitarRecolectar(UnidadSeleccionada.UnidadModelo, deposito);
                    return;
                }
            }

            gameController.SolicitarMover(UnidadSeleccionada.UnidadModelo, destino);
        }

        /// <summary>Llamado por un botón de construcción (ver PanelConstruccionUI).</summary>
        public void EntrarModoColocarEdificio(string tipoEdificio)
        {
            Modo = ModoInteraccion.ColocandoEdificio;
            _tipoEdificioAConstruir = tipoEdificio;
        }

        public void CancelarModoColocacion()
        {
            Modo = ModoInteraccion.Normal;
            _tipoEdificioAConstruir = null;
        }

        private void Seleccionar(UnidadView unidad)
        {
            LimpiarSeleccion();
            UnidadSeleccionada = unidad;
            indicadorSeleccion?.MostrarSobre(unidad.transform);
            SeleccionUnidadCambio?.Invoke(unidad);
        }

        private void Seleccionar(EdificioView edificio)
        {
            LimpiarSeleccion();
            EdificioSeleccionado = edificio;
            indicadorSeleccion?.MostrarSobre(edificio.transform);
            SeleccionEdificioCambio?.Invoke(edificio);
        }

        private void LimpiarSeleccion()
        {
            bool habiaAlgo = UnidadSeleccionada != null || EdificioSeleccionado != null;
            UnidadSeleccionada = null;
            EdificioSeleccionado = null;
            indicadorSeleccion?.Ocultar();

            if (habiaAlgo)
            {
                SeleccionUnidadCambio?.Invoke(null);
                SeleccionEdificioCambio?.Invoke(null);
            }
        }

        private Vector3 ObtenerPosicionMundoDelMouse()
        {
            Vector3 posicion = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            posicion.z = 0f;
            return posicion;
        }
    }
}