using System.Threading;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Clase base de toda unidad del juego (aldeanos y unidades militares).
    /// Es una clase POCO: no conoce nada de UnityEngine ni de la interfaz gráfica.
    /// </summary>
    public abstract class Unidad
    {
        // Generador de IDs thread-safe: varias unidades pueden crearse desde distintos hilos.
        // OJO: este contador es LOCAL a cada proceso, así que NO sirve para identificar una
        // unidad por red (el host y el cliente cuentan distinto). Por eso GameController
        // le pisa el Id con AsignarId(...) usando un ID determinístico que viaja en el mensaje.
        private static int _contadorId = 0;

        public int Id { get; private set; }
        public string Nombre { get; set; }
        public string Propietario { get; set; } // Nombre del Jugador dueño de la unidad

        public int VidaMaxima { get; protected set; }
        public int VidaActual { get; set; }
        public int Ataque { get; protected set; }
        public int RangoAtaque { get; protected set; }
        public int Velocidad { get; protected set; } // celdas por segundo

        public Coordenada Posicion { get; set; }
        public EstadoUnidad Estado { get; set; } = EstadoUnidad.Inactiva;

        public bool EstaViva => VidaActual > 0;

        protected Unidad(string nombre, string propietario, Coordenada posicion,
                          int vidaMaxima, int ataque, int rangoAtaque, int velocidad)
        {
            Id = Interlocked.Increment(ref _contadorId);
            Nombre = nombre;
            Propietario = propietario;
            Posicion = posicion;
            VidaMaxima = vidaMaxima;
            VidaActual = vidaMaxima;
            Ataque = ataque;
            RangoAtaque = rangoAtaque;
            Velocidad = velocidad;
        }

        /// <summary>
        /// Reemplaza el Id autogenerado por uno acordado entre las dos instancias del juego.
        /// Hay que llamarlo apenas se crea la unidad, ANTES de agregarla a Jugador.Unidades.
        /// </summary>
        public void AsignarId(int id) => Id = id;

        /// <summary>
        /// Aplica daño a la unidad. Se marca virtual por si alguna unidad
        /// (por ejemplo con armadura) necesita reducir el daño recibido.
        /// </summary>
        public virtual void RecibirDano(int cantidad)
        {
            if (cantidad <= 0) return;
            VidaActual -= cantidad;
            if (VidaActual < 0) VidaActual = 0;
        }

        public bool PuedeAtacar(Coordenada objetivo) => Posicion.DistanciaA(objetivo) <= RangoAtaque;
    }
}
