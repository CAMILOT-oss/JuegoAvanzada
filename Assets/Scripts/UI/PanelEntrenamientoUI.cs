using UnityEngine;
using UnityEngine.UI;
using ImperiosEnGuerra.Modelo;
using ImperiosEnGuerra.Vista;
using ImperiosEnGuerra.Controlador;

namespace ImperiosEnGuerra.UI
{
    /// <summary>
    /// Aparece cuando el jugador selecciona un CentroUrbano (entrena Aldeanos) o un
    /// Cuartel (entrena Soldado/Arquero), y oculta el resto del tiempo.
    /// Escucha el evento SeleccionEdificioCambio de SeleccionController.
    /// </summary>
    public class PanelEntrenamientoUI : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private SeleccionController seleccionController;

        [Header("Panel")]
        [SerializeField] private GameObject panelRaiz;

        [Header("Botones")]
        [SerializeField] private Button botonEntrenarAldeano;
        [SerializeField] private Button botonEntrenarSoldado;
        [SerializeField] private Button botonEntrenarArquero;

        private Edificio _edificioActual;

        private void Awake()
        {
            seleccionController.SeleccionEdificioCambio += OnSeleccionEdificioCambio;

            botonEntrenarAldeano?.onClick.AddListener(() => Entrenar("Aldeano"));
            botonEntrenarSoldado?.onClick.AddListener(() => Entrenar("Soldado"));
            botonEntrenarArquero?.onClick.AddListener(() => Entrenar("Arquero"));

            if (panelRaiz != null)
                panelRaiz.SetActive(false);
        }

        private void OnDestroy()
        {
            if (seleccionController != null)
                seleccionController.SeleccionEdificioCambio -= OnSeleccionEdificioCambio;
        }

        private void OnSeleccionEdificioCambio(EdificioView vista)
        {
            _edificioActual = vista?.EdificioModelo;

            bool esCentroUrbano = _edificioActual is CentroUrbano;
            bool esCuartel = _edificioActual is Cuartel;
            bool puedeEntrenar = esCentroUrbano || esCuartel;

            if (panelRaiz != null)
                panelRaiz.SetActive(puedeEntrenar);

            if (botonEntrenarAldeano != null) botonEntrenarAldeano.gameObject.SetActive(esCentroUrbano);
            if (botonEntrenarSoldado != null) botonEntrenarSoldado.gameObject.SetActive(esCuartel);
            if (botonEntrenarArquero != null) botonEntrenarArquero.gameObject.SetActive(esCuartel);
        }

        private void Entrenar(string tipoUnidad)
        {
            if (_edificioActual == null) return;
            gameController.SolicitarEntrenar(tipoUnidad, _edificioActual);
        }
    }
}
