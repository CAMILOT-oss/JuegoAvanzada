using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Traduce entre el sistema de coordenadas del Modelo (Fila, Columna, enteros)
    /// y el sistema de coordenadas del mundo de Unity (Vector3, en unidades).
    /// Es la ÚNICA clase que debe conocer esta conversión: si cambias el tamaño
    /// de celda o el origen del grid, lo cambias acá y no en cada script de Vista.
    /// </summary>
    public static class ConversorCoordenadas
    {
        /// <summary>Tamaño en unidades de Unity de cada celda del mapa.</summary>
        public const float TamanoCelda = 1f;

        /// <summary>
        /// Convierte una Coordenada del Modelo a una posición del mundo.
        /// La fila crece hacia abajo (como en una matriz), por eso Y = -fila.
        /// </summary>
        public static Vector3 AMundo(Coordenada coordenada)
        {
            return new Vector3(coordenada.Columna * TamanoCelda, -coordenada.Fila * TamanoCelda, 0f);
        }

        /// <summary>
        /// Igual que AMundo, pero para entidades que ocupan más de una celda (edificios 2x2, etc.):
        /// "coordenada" es la esquina superior-izquierda del rectángulo (como se guarda en el
        /// Modelo), y esta función devuelve el CENTRO de todo el rectángulo en el mundo,
        /// que es donde hay que ubicar el pivote del sprite para que quede bien alineado.
        /// </summary>
        public static Vector3 AMundoCentro(Coordenada coordenada, int ancho, int alto)
        {
            float offsetX = (ancho - 1) * TamanoCelda * 0.5f;
            float offsetY = (alto - 1) * TamanoCelda * 0.5f;
            Vector3 esquina = AMundo(coordenada);
            return new Vector3(esquina.x + offsetX, esquina.y - offsetY, esquina.z);
        }

        /// <summary>Convierte una posición del mundo de vuelta a una Coordenada del Modelo (por ejemplo, al hacer clic).</summary>
        public static Coordenada ACoordenada(Vector3 posicionMundo)
        {
            int columna = Mathf.RoundToInt(posicionMundo.x / TamanoCelda);
            int fila = Mathf.RoundToInt(-posicionMundo.y / TamanoCelda);
            return new Coordenada(fila, columna);
        }
    }
}

