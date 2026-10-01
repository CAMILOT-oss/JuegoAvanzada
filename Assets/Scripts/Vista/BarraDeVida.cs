using UnityEngine;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Barra de vida hecha con dos sprites: un "Fondo" (marco) y un "Relleno" que se achica
    /// según el porcentaje de vida.
    ///
    /// El tamaño se define acá, en el Inspector (Ancho / Alto / Borde, en unidades del mundo:
    /// 1 = una casilla), y el script calcula solo la escala de los sprites sin importar de
    /// cuántos píxeles sean ni cuál sea su Pixels Per Unit. También mantiene fijo el borde
    /// izquierdo del relleno sin importar dónde tenga el pivote el sprite, así que no hace
    /// falta tocar el Sprite Editor.
    ///
    /// Armado en el prefab (una vez por prefab de unidad/edificio):
    ///   BarraDeVida (este componente, un poco arriba del sprite principal)
    ///     |- Fondo   (SpriteRenderer, gris/negro, sprite de 1 píxel blanco)
    ///     |- Relleno (SpriteRenderer, verde,      sprite de 1 píxel blanco)
    /// Si los hijos se llaman "Fondo" y "Relleno" se encuentran solos; si no, arrastralos a mano.
    /// La barra queda centrada en el origen del objeto "BarraDeVida": para subirla o bajarla
    /// mové ese objeto, no los hijos.
    /// </summary>
    public class BarraDeVida : MonoBehaviour
    {
        [Header("Piezas")]
        [SerializeField] private Transform fondo;      // opcional
        [SerializeField] private Transform relleno;
        [SerializeField] private SpriteRenderer rellenoRenderer;

        [Header("Tamaño (unidades del mundo: 1 = una casilla)")]
        [Tooltip("Largo total de la barra. Unidades: ~0.8. Edificios de 2x2: ~1.8.")]
        [SerializeField] private float ancho = 0.9f;
        [SerializeField] private float alto = 0.12f;
        [Tooltip("Grosor del marco que asoma alrededor del relleno.")]
        [SerializeField] private float borde = 0.03f;

        [Header("Dibujo")]
        [Tooltip("Que sea mayor que el Order in Layer de los sprites de unidades y edificios.")]
        [SerializeField] private int ordenDeDibujo = 100;

        [Header("Colores")]
        [SerializeField] private Color colorVidaAlta = Color.green;
        [SerializeField] private Color colorVidaMedia = Color.yellow;
        [SerializeField] private Color colorVidaBaja = Color.red;

        private bool _configurada;
        private float _escalaXCompleta = 1f;   // escala X del relleno con la vida al 100%
        private float _anchoSpriteRelleno = 1f; // ancho del sprite en unidades, con escala 1
        private float _pivoteXRelleno = 0.5f;
        private float _pivoteYRelleno = 0.5f;

        private void Awake() => Configurar();

        private void Configurar()
        {
            if (_configurada) return;

            if (relleno == null) relleno = transform.Find("Relleno");
            if (fondo == null) fondo = transform.Find("Fondo");
            if (relleno == null) return;

            if (rellenoRenderer == null)
                rellenoRenderer = relleno.GetComponent<SpriteRenderer>();

            Vector3 escala = relleno.localScale;
            if (rellenoRenderer != null && rellenoRenderer.sprite != null)
            {
                Sprite s = rellenoRenderer.sprite;
                _anchoSpriteRelleno = s.bounds.size.x;
                _pivoteXRelleno = s.rect.width > 0 ? s.pivot.x / s.rect.width : 0.5f;
                _pivoteYRelleno = s.rect.height > 0 ? s.pivot.y / s.rect.height : 0.5f;

                _escalaXCompleta = ancho / _anchoSpriteRelleno;
                escala = new Vector3(_escalaXCompleta, alto / s.bounds.size.y, 1f);
                rellenoRenderer.sortingOrder = ordenDeDibujo + 1; // siempre encima del fondo
            }
            else
            {
                _escalaXCompleta = escala.x;
            }
            relleno.localScale = escala;

            ConfigurarFondo();
            ColocarRelleno(1f);
            _configurada = true;
        }

        private void ConfigurarFondo()
        {
            if (fondo == null) return;

            var fondoRenderer = fondo.GetComponent<SpriteRenderer>();
            if (fondoRenderer == null || fondoRenderer.sprite == null) return;

            Sprite s = fondoRenderer.sprite;
            float anchoTotal = ancho + 2f * borde;
            float altoTotal = alto + 2f * borde;

            fondo.localScale = new Vector3(anchoTotal / s.bounds.size.x, altoTotal / s.bounds.size.y, 1f);

            float pivoteX = s.rect.width > 0 ? s.pivot.x / s.rect.width : 0.5f;
            float pivoteY = s.rect.height > 0 ? s.pivot.y / s.rect.height : 0.5f;

            // Centrado en el origen de este objeto, sin importar el pivote del sprite.
            float x = (pivoteX - 0.5f) * anchoTotal;
            float y = (pivoteY - 0.5f) * altoTotal;
            fondo.localPosition = new Vector3(x, y, fondo.localPosition.z);

            fondoRenderer.sortingOrder = ordenDeDibujo;
        }

        /// <summary>Achica el relleno desde la derecha, dejando fijo su borde izquierdo.</summary>
        private void ColocarRelleno(float porcentaje)
        {
            float escalaX = _escalaXCompleta * porcentaje;

            Vector3 escala = relleno.localScale;
            escala.x = escalaX;
            relleno.localScale = escala;

            float bordeIzquierdo = -ancho * 0.5f;
            float x = bordeIzquierdo + _pivoteXRelleno * _anchoSpriteRelleno * escalaX;
            float y = (_pivoteYRelleno - 0.5f) * alto;
            relleno.localPosition = new Vector3(x, y, relleno.localPosition.z);
        }

        /// <summary>Actualiza el largo y el color de la barra según vidaActual/vidaMaxima.</summary>
        public void SetPorcentaje(int vidaActual, int vidaMaxima)
        {
            Configurar(); // por si la barra estaba desactivada cuando se creó
            if (relleno == null || vidaMaxima <= 0) return;

            float porcentaje = Mathf.Clamp01((float)vidaActual / vidaMaxima);
            ColocarRelleno(porcentaje);

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