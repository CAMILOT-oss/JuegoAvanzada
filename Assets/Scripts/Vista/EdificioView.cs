using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Componente que va sobre el prefab de un edificio (CentroUrbano, Cuartel, Almacén).
    /// Igual que UnidadView, solo lee del Modelo y nunca lo modifica.
    /// </summary>
    public class EdificioView : EntidadViewBase
    {
        [Header("Mientras se está construyendo")]
        [SerializeField] private GameObject overlayEnConstruccion; // ej: un sprite de "andamios" semitransparente
        [SerializeField] private float alfaEnConstruccion = 0.5f;

        public Edificio EdificioModelo { get; private set; }

        public void Vincular(Edificio edificio)
        {
            EdificioModelo = edificio;
            gameObject.name = $"{edificio.Nombre}_{edificio.Id}";
            transform.position = ConversorCoordenadas.AMundoCentro(edificio.Posicion, edificio.Ancho, edificio.Alto);
            ActualizarVida(edificio.VidaActual, edificio.VidaMaxima);
            AplicarEstadoConstruccion(edificio.EstaConstruido);
        }

        public void SincronizarConModelo()
        {
            if (EdificioModelo == null) return;

            ActualizarVida(EdificioModelo.VidaActual, EdificioModelo.VidaMaxima);
            AplicarEstadoConstruccion(EdificioModelo.EstaConstruido);
        }

        private void AplicarEstadoConstruccion(bool estaConstruido)
        {
            if (overlayEnConstruccion != null)
                overlayEnConstruccion.SetActive(!estaConstruido);

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = estaConstruido ? 1f : alfaEnConstruccion;
                spriteRenderer.color = c;
            }
        }
    }
}

