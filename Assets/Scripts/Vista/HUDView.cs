using UnityEngine;
using TMPro;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Muestra en pantalla los recursos del jugador local y el estado general de la partida.
    /// Solo lee del Modelo (Jugador / Partida), nunca lo modifica.
    /// Requiere TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources si no lo tenés).
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Textos de recursos")]
        [SerializeField] private TMP_Text textoOro;
        [SerializeField] private TMP_Text textoMadera;
        [SerializeField] private TMP_Text textoComida;

        [Header("Estado general")]
        [SerializeField] private TMP_Text textoEstado; // ej: "En curso", turno, mensajes cortos

        public void ActualizarRecursos(Jugador jugador)
        {
            if (jugador == null) return;

            if (textoOro != null) textoOro.text = jugador.ObtenerRecurso(TipoRecurso.Oro).ToString();
            if (textoMadera != null) textoMadera.text = jugador.ObtenerRecurso(TipoRecurso.Madera).ToString();
            if (textoComida != null) textoComida.text = jugador.ObtenerRecurso(TipoRecurso.Comida).ToString();
        }

        public void MostrarMensaje(string mensaje)
        {
            if (textoEstado != null)
                textoEstado.text = mensaje;
        }
    }
}
