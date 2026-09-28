using UnityEngine;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Controla una barra de vida simple compuesta por un sprite de "relleno"
    /// cuyo ancho se escala según el porcentaje de vida.
    ///
    /// CÓMO ARMARLA EN EL PREFAB (una sola vez por prefab de unidad/edificio):
    /// 1. Creá un hijo vacío "BarraDeVida" un poco arriba del sprite principal.
    /// 2. Dentro, poné un SpriteRenderer "Fondo" (gris/negro) y otro "Relleno" (verde),
    ///    ambos con un sprite de 1x1 (pixel blanco) estirado con Transform.
    /// 3. El pivote/ancla del sprite "Relleno" debe quedar a la IZQUIERDA
    ///    (Sprite Editor > Pivot > Left), para que al escalar en X se achique
    ///    hacia la derecha y no hacia el centro.
    /// 4. Agregá este componente al objeto "BarraDeVida" y arrastrá "Relleno" al campo Relleno.
    /// </summary>
    public class BarraDeVida : MonoBehaviour
    {
        [SerializeField] private Transform relleno;
        [SerializeField] private Color colorVidaAlta = Color.green;
        [SerializeField] private Color colorVidaMedia = Color.yellow;
        [SerializeField] private Color colorVidaBaja = Color.red;
        [SerializeField] private SpriteRenderer rellenoRenderer;

        private Vector3 _escalaOriginal;

        private void Awake()
        {
            if (relleno != null)
            {
                _escalaOriginal = relleno.localScale;
                if (rellenoRenderer == null)
                    rellenoRenderer = relleno.GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>Actualiza el ancho y color de la barra según vidaActual/vidaMaxima.</summary>
        public void SetPorcentaje(int vidaActual, int vidaMaxima)
        {
            if (relleno == null || vidaMaxima <= 0) return;

            float porcentaje = Mathf.Clamp01((float)vidaActual / vidaMaxima);
            relleno.localScale = new Vector3(_escalaOriginal.x * porcentaje, _escalaOriginal.y, _escalaOriginal.z);

            if (rellenoRenderer != null)
            {
                if (porcentaje > 0.6f) rellenoRenderer.color = colorVidaAlta;
                else if (porcentaje > 0.3f) rellenoRenderer.color = colorVidaMedia;
                else rellenoRenderer.color = colorVidaBaja;
            }
        }

        public void Mostrar(bool visible) => gameObject.SetActive(visible);
    }
}
