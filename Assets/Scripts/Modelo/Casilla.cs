namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Una celda de la matriz del mapa. Puede tener terreno, un depósito de recurso,
    /// un edificio y/o una unidad ocupándola.
    /// </summary>
    public class Casilla
    {
        public Coordenada Posicion { get; }
        public TipoTerreno Terreno { get; set; }
        public DepositoRecurso Recurso { get; set; } // null si no hay recurso en esta casilla
        public Edificio Edificio { get; set; }        // null si no hay edificio
        public Unidad UnidadOcupante { get; set; }     // null si no hay unidad parada aquí

        public Casilla(Coordenada posicion, TipoTerreno terreno = TipoTerreno.Libre)
        {
            Posicion = posicion;
            Terreno = terreno;
        }

        /// <summary>
        /// Transitable para CONSTRUIR: el terreno lo permite y no hay edificio ni
        /// unidad ocupándola (no se puede levantar un edificio arriba de una unidad).
        /// </summary>
        public bool EsTransitable =>
            Terreno != TipoTerreno.Agua &&
            Terreno != TipoTerreno.Montana &&
            Edificio == null &&
            UnidadOcupante == null;

        /// <summary>
        /// Transitable para MOVERSE: el terreno lo permite y no hay edificio, pero
        /// NO le importa si ya hay otra unidad ahí — las unidades se pueden pasar
        /// por encima entre sí (aldeanos, soldados, arqueros), no se chocan.
        /// </summary>
        public bool EsTransitablePorUnidad =>
            Terreno != TipoTerreno.Agua &&
            Terreno != TipoTerreno.Montana &&
            Edificio == null;
    }
}

