using UnityEngine;
using UnityEngine.UI;
using ImperiosEnGuerra.Modelo;
using ImperiosEnGuerra.Controlador;

namespace ImperiosEnGuerra.UI
{
    /// <summary>
    /// Botones de construcción. Cada botón, al tocarlo, pone al SeleccionController
    /// en "modo colocar edificio"; el próximo clic en el mapa dispara la construcción.
    /// Opcionalmente deshabilita el botón si al jugador local no le alcanzan los recursos.
    /// </summary>
    public class PanelConstruccionUI : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private SeleccionController seleccionController;

        [Header("Botones")]
        [SerializeField] private Button botonCuartel;
        [SerializeField] private Button botonAlmacen;

        private void Awake()
        {
            if (botonCuartel != null)
                botonCuartel.onClick.AddListener(() => seleccionController.EntrarModoColocarEdificio("Cuartel"));

            if (botonAlmacen != null)
                botonAlmacen.onClick.AddListener(() => seleccionController.EntrarModoColocarEdificio("Almacen"));
        }

        private void Update()
        {
            // Habilita/deshabilita según recursos disponibles del jugador local.
            if (gameController.JugadorLocal == null) return;

            if (botonCuartel != null)
                botonCuartel.interactable = AlcanzaPara(CostosJuego.CostoCuartel);

            if (botonAlmacen != null)
                botonAlmacen.interactable = AlcanzaPara(CostosJuego.CostoAlmacen);
        }

        private bool AlcanzaPara(System.Collections.Generic.Dictionary<TipoRecurso, int> costo)
        {
            foreach (var par in costo)
                if (gameController.JugadorLocal.ObtenerRecurso(par.Key) < par.Value)
                    return false;
            return true;
        }
    }
}
