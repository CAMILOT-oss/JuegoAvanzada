using UnityEngine;
using TMPro;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Panel que aparece cuando la Partida.Estado pasa a Finalizada.
    /// Arrancá con el GameObject "panelRaiz" desactivado en la escena.
    /// </summary>
    public class PanelFinPartidaView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRaiz;
        [SerializeField] private TMP_Text textoResultado;

        private void Awake()
        {
            if (panelRaiz != null)
                panelRaiz.SetActive(false);
        }

        /// <summary>
        /// Muestra el panel indicando quién ganó, comparado con el nombre del jugador local
        /// (para poder mostrar "¡Ganaste!" o "Perdiste" en vez de solo el nombre).
        /// </summary>
        public void MostrarResultado(string ganador, string nombreJugadorLocal)
        {
            if (panelRaiz != null)
                panelRaiz.SetActive(true);

            if (textoResultado == null) return;

            if (ganador == "Empate")
                textoResultado.text = "¡Empate!";
            else if (ganador == nombreJugadorLocal)
                textoResultado.text = "¡Victoria!";
            else
                textoResultado.text = $"Derrota. Ganó {ganador}.";
        }

        public void Ocultar()
        {
            if (panelRaiz != null)
                panelRaiz.SetActive(false);
        }
    }
}
