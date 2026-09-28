using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Componente que va sobre el prefab de una unidad (Aldeano, Soldado, Arquero).
    /// Se limita a LEER el objeto Unidad del Modelo y reflejarlo visualmente:
    /// nunca modifica al Modelo (eso es trabajo del Controlador).
    /// </summary>
    public class UnidadView : EntidadViewBase
    {
        [Header("Solo Aldeano (opcional)")]
        [SerializeField] private GameObject indicadorCargando; // ej: un iconito de saco sobre la cabeza

        public Unidad UnidadModelo { get; private set; }

        private Vector3 _velocidadActualSuavizado = Vector3.zero;
        [SerializeField] private float tiempoSuavizadoMovimiento = 0.15f;

        /// <summary>Asocia esta vista a una instancia concreta del Modelo. Se llama una sola vez, al crearla.</summary>
        public void Vincular(Unidad unidad)
        {
            UnidadModelo = unidad;
            gameObject.name = $"{unidad.Nombre}_{unidad.Id}";
            transform.position = ConversorCoordenadas.AMundo(unidad.Posicion);
            ActualizarVida(unidad.VidaActual, unidad.VidaMaxima);
        }

        /// <summary>
        /// Se llama en cada frame (desde GameViewManager) para reflejar el estado actual del Modelo.
        /// </summary>
        public void SincronizarConModelo()
        {
            if (UnidadModelo == null) return;

            Vector3 posicionObjetivo = ConversorCoordenadas.AMundo(UnidadModelo.Posicion);
            transform.position = Vector3.SmoothDamp(
                transform.position, posicionObjetivo, ref _velocidadActualSuavizado, tiempoSuavizadoMovimiento);

            ActualizarVida(UnidadModelo.VidaActual, UnidadModelo.VidaMaxima);

            // Voltea el sprite según hacia dónde se mueve, para 2D top-down.
            float diferenciaX = posicionObjetivo.x - transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(diferenciaX) > 0.01f)
                spriteRenderer.flipX = diferenciaX < 0f;

            if (indicadorCargando != null && UnidadModelo is Aldeano aldeano)
                indicadorCargando.SetActive(aldeano.CargaActual > 0);
        }
    }
}
