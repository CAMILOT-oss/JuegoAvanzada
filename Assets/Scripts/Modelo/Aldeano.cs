namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Unidad civil encargada de recolectar recursos y construir edificios.
    /// La recolección real (con Task/Thread y delays) la maneja el Controlador;
    /// aquí solo se guarda el estado de qué recurso transporta.
    /// </summary>
    public class Aldeano : Unidad
    {
        public const int CapacidadMaximaCarga = 10;

        public TipoRecurso? RecursoAsignado { get; set; }
        public int CargaActual { get; private set; }

        public Aldeano(string propietario, Coordenada posicion)
            : base("Aldeano", propietario, posicion, vidaMaxima: 25, ataque: 2, rangoAtaque: 1, velocidad: 2)
        {
        }

        public bool CargaLlena => CargaActual >= CapacidadMaximaCarga;

        /// <summary>
        /// Suma recurso a la carga que lleva el aldeano, sin sobrepasar la capacidad.
        /// Devuelve la cantidad realmente añadida.
        /// </summary>
        public int Cargar(int cantidad)
        {
            int espacioDisponible = CapacidadMaximaCarga - CargaActual;
            int aCargar = cantidad < espacioDisponible ? cantidad : espacioDisponible;
            CargaActual += aCargar;
            return aCargar;
        }

        /// <summary>
        /// Vacía la carga (al entregarla en el Centro Urbano o Almacén) y la devuelve.
        /// </summary>
        public int Descargar()
        {
            int carga = CargaActual;
            CargaActual = 0;
            return carga;
        }
    }
}
