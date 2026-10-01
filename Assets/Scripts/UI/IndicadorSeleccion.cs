using UnityEngine;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Objeto puramente visual (un anillo, un marco, unas esquinas resaltadas)
    /// que se engancha como hijo de la Vista seleccionada para indicar "esto es
    /// lo que tenés agarrado". No conoce el Modelo ni el Controlador: solo sabe
    /// pegarse a un Transform o esconderse.
    /// </summary>
    public class IndicadorSeleccion : MonoBehaviour
    {
        private void Awake() => Ocultar();

        public void MostrarSobre(Transform objetivo)
        {
            transform.SetParent(objetivo, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            gameObject.SetActive(true);
        }

        public void Ocultar()
        {
            transform.SetParent(null);
            gameObject.SetActive(false);
        }
    }
}
