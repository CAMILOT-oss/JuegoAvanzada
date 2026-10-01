namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Representa un depósito de recurso (una mina de oro, un árbol, una zona de caza, etc.)
    /// ubicado en una casilla del mapa. Varios Aldeanos pueden intentar extraer de él,
    /// por lo que el Controlador debe sincronizar el acceso (lock) al llamar a Extraer().
    /// </summary>
    public class DepositoRecurso
    {
        public TipoRecurso Tipo { get; private set; }
        public Coordenada Posicion { get; private set; }
        public int CantidadDisponible { get; private set; }

        public bool EstaAgotado => CantidadDisponible <= 0;

        public DepositoRecurso(TipoRecurso tipo, Coordenada posicion, int cantidadInicial)
        {
            Tipo = tipo;
            Posicion = posicion;
            CantidadDisponible = cantidadInicial;
        }

        /// <summary>
        /// Extrae hasta "cantidad" unidades del depósito y devuelve lo realmente extraído.
        /// No es thread-safe por sí sola: quien la invoque desde varios hilos (Aldeanos)
        /// debe envolver la llamada en un lock sobre este mismo objeto.
        /// </summary>
        public int Extraer(int cantidad)
        {
            if (cantidad <= 0 || EstaAgotado) return 0;

            int extraido = cantidad <= CantidadDisponible ? cantidad : CantidadDisponible;
            CantidadDisponible -= extraido;
            return extraido;
        }
    }
}
