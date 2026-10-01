using System.Threading;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Clase base de todo edificio del juego. POCO, sin lógica de presentación.
    /// </summary>
    public abstract class Edificio
    {
        // Contador LOCAL a cada proceso: no sirve para identificar un edificio por red.
        // GameController le pisa el Id con AsignarId(...) usando un ID determinístico.
        private static int _contadorId = 0;

        public int Id { get; private set; }
        public string Nombre { get; set; }
        public string Propietario { get; set; }
        public Coordenada Posicion { get; set; }

        public int VidaMaxima { get; protected set; }
        public int VidaActual { get; set; }

        /// <summary>
        /// Tamaño del edificio en celdas del mapa (por defecto 1x1). Posicion es la
        /// esquina superior-izquierda (fila/columna menor) del rectángulo que ocupa;
        /// el edificio abarca las celdas [Fila, Fila+Alto) x [Columna, Columna+Ancho).
        /// </summary>
        public int Ancho { get; protected set; } = 1;
        public int Alto { get; protected set; } = 1;

        /// <summary>Tiempo de construcción en segundos (lo usa el hilo/Task que construye).</summary>
        public int TiempoConstruccionSegundos { get; protected set; }

        /// <summary>
        /// Se pone en true cuando el hilo de construcción termina.
        /// Mientras sea false, el edificio se muestra "en obra" en la Vista.
        /// </summary>
        public bool EstaConstruido { get; set; }

        public bool EstaDestruido => VidaActual <= 0;

        protected Edificio(string nombre, string propietario, Coordenada posicion,
                            int vidaMaxima, int tiempoConstruccionSegundos, int ancho = 1, int alto = 1)
        {
            Id = Interlocked.Increment(ref _contadorId);
            Nombre = nombre;
            Propietario = propietario;
            Posicion = posicion;
            VidaMaxima = vidaMaxima;
            VidaActual = vidaMaxima;
            TiempoConstruccionSegundos = tiempoConstruccionSegundos;
            Ancho = ancho;
            Alto = alto;
            EstaConstruido = false;
        }

        /// <summary>
        /// Reemplaza el Id autogenerado por uno acordado entre las dos instancias del juego.
        /// Hay que llamarlo apenas se crea el edificio, ANTES de agregarlo a Jugador.Edificios.
        /// </summary>
        public void AsignarId(int id) => Id = id;

        public virtual void RecibirDano(int cantidad)
        {
            if (cantidad <= 0 || !EstaConstruido) return;
            VidaActual -= cantidad;
            if (VidaActual < 0) VidaActual = 0;
        }
    }
}

