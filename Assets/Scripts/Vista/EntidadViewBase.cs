using System.Collections;
using UnityEngine;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Base común para UnidadView y EdificioView: ambas tienen una barra de vida
    /// y una animación simple de destrucción. No conoce clases del Modelo directamente
    /// (eso lo agregan las clases hijas), solo maneja lo puramente visual.
    /// </summary>
    public abstract class EntidadViewBase : MonoBehaviour
    {
        [Header("Referencias comunes (asignar en el prefab)")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected BarraDeVida barraDeVida;

        [Header("Animación")]
        [SerializeField] private float duracionDesvanecido = 0.4f;

        protected virtual void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (barraDeVida == null)
                barraDeVida = GetComponentInChildren<BarraDeVida>();
        }

        /// <summary>Refleja la vida actual/máxima del Modelo en la barra de vida.</summary>
        protected void ActualizarVida(int vidaActual, int vidaMaxima)
        {
            if (barraDeVida != null)
                barraDeVida.SetPorcentaje(vidaActual, vidaMaxima);
        }

        /// <summary>
        /// Se llama cuando la entidad del Modelo ya no está viva/fue destruida.
        /// Reproduce un desvanecido simple y luego destruye el GameObject de la Vista.
        /// </summary>
        public void Destruir()
        {
            StopAllCoroutines();
            StartCoroutine(CoDesvanecerYDestruir());
        }

        private IEnumerator CoDesvanecerYDestruir()
        {
            float tiempo = 0f;
            Color colorInicial = spriteRenderer != null ? spriteRenderer.color : Color.white;

            while (tiempo < duracionDesvanecido)
            {
                tiempo += Time.deltaTime;
                if (spriteRenderer != null)
                {
                    float alfa = Mathf.Lerp(colorInicial.a, 0f, tiempo / duracionDesvanecido);
                    spriteRenderer.color = new Color(colorInicial.r, colorInicial.g, colorInicial.b, alfa);
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
