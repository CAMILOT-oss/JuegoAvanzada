using System.Collections.Generic;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Tabla de balance del juego: cuánto cuesta y cuánto tarda cada cosa.
    /// Centralizarlo acá (y no como números sueltos en cada método) hace que
    /// ajustar el balance sea cuestión de tocar un solo archivo.
    /// </summary>
    public static class CostosJuego
    {
        public static readonly Dictionary<TipoRecurso, int> CostoAldeano = new()
        {
            { TipoRecurso.Comida, 50 }
        };

        public static readonly Dictionary<TipoRecurso, int> CostoSoldado = new()
        {
            { TipoRecurso.Comida, 60 },
            { TipoRecurso.Oro, 20 }
        };

        public static readonly Dictionary<TipoRecurso, int> CostoArquero = new()
        {
            { TipoRecurso.Madera, 40 },
            { TipoRecurso.Oro, 30 }
        };

        public static readonly Dictionary<TipoRecurso, int> CostoCuartel = new()
        {
            { TipoRecurso.Madera, 120 }
        };

        public static readonly Dictionary<TipoRecurso, int> CostoAlmacen = new()
        {
            { TipoRecurso.Madera, 80 }
        };

        /// <summary>Tamaño (ancho, alto) en celdas de cada edificio, usado para validar antes de construir.</summary>
        public static readonly Dictionary<string, (int Ancho, int Alto)> TamanoEdificio = new()
        {
            { "CentroUrbano", (2, 2) },
            { "Cuartel", (2, 2) },
            { "Almacen", (1, 1) }
        };

        public const int TiempoEntrenamientoAldeanoSegundos = 8;
        public const int TiempoEntrenamientoSoldadoSegundos = 12;
        public const int TiempoEntrenamientoArqueroSegundos = 12;

        public const int CantidadExtraidaPorViaje = 10; // cuánto junta un Aldeano antes de volver
    }
}
