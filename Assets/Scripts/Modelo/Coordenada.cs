using System;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Representa una posición (fila, columna) dentro del mapa 15x15.
    /// Se usa tanto en el Modelo como en los mensajes de red (Controlador).
    /// </summary>
    [Serializable]
    public struct Coordenada : IEquatable<Coordenada>
    {
        public int Fila { get; set; }
        public int Columna { get; set; }

        public Coordenada(int fila, int columna)
        {
            Fila = fila;
            Columna = columna;
        }

        /// <summary>
        /// Distancia de Chebyshev (útil para rango de ataque/recolección en una grilla).
        /// </summary>
        public int DistanciaA(Coordenada otra)
        {
            int df = Math.Abs(Fila - otra.Fila);
            int dc = Math.Abs(Columna - otra.Columna);
            return Math.Max(df, dc);
        }

        public bool Equals(Coordenada otra) => Fila == otra.Fila && Columna == otra.Columna;

        public override bool Equals(object obj) => obj is Coordenada c && Equals(c);

        public override int GetHashCode() => HashCode.Combine(Fila, Columna);

        public override string ToString() => $"({Fila},{Columna})";

        public static bool operator ==(Coordenada a, Coordenada b) => a.Equals(b);
        public static bool operator !=(Coordenada a, Coordenada b) => !a.Equals(b);
    }
}
