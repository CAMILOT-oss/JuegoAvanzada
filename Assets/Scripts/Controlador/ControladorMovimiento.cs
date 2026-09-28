using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Mueve una unidad hacia un destino, un paso (una celda) a la vez, en un Task
    /// aparte para no bloquear el hilo principal de Unity ni el resto de la simulación.
    /// La velocidad de la unidad (celdas/segundo) define cuánto se tarda cada paso.
    /// </summary>
    public static class ControladorMovimiento
    {
        /// <summary>
        /// Lanza el movimiento en segundo plano. "token" permite cancelarlo
        /// (por ejemplo, si al jugador le mandan atacar a mitad de camino).
        /// Devuelve true si la unidad realmente terminó parada en "destino", o false
        /// si el camino se bloqueó a mitad de camino (por ejemplo, otra unidad ocupando
        /// la celda) — el que llama a esto SIEMPRE debe chequear este resultado antes
        /// de asumir que la unidad "llegó" a algún lado.
        /// </summary>
        public static Task<bool> MoverAsync(Unidad unidad, Coordenada destino, Mapa mapa, CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                unidad.Estado = EstadoUnidad.Moviendose;

                while (unidad.EstaViva && unidad.Posicion != destino && !token.IsCancellationRequested)
                {
                    Coordenada siguientePaso = CalcularSiguientePaso(unidad.Posicion, destino);

                    lock (mapa.BloqueoMapa)
                    {
                        if (!ValidadorAcciones.PuedeMoverse(unidad, siguientePaso, mapa))
                            break; // camino bloqueado (otra unidad/edificio); el llamador decide qué hacer

                        mapa.MoverUnidad(unidad, siguientePaso);
                    }

                    // A más velocidad, menos tiempo por celda.
                    int milisegundosPorCelda = unidad.Velocidad > 0 ? 1000 / unidad.Velocidad : 1000;
                    await Task.Delay(milisegundosPorCelda, token).ConfigureAwait(false);
                }

                bool llego = unidad.Posicion == destino;

                if (unidad.EstaViva)
                    unidad.Estado = EstadoUnidad.Inactiva;

                return llego;
            }, token);
        }

        /// <summary>Da un paso de una sola celda en dirección al destino (movimiento tipo "rey" de ajedrez).</summary>
        private static Coordenada CalcularSiguientePaso(Coordenada origen, Coordenada destino)
        {
            int deltaFila = System.Math.Sign(destino.Fila - origen.Fila);
            int deltaColumna = System.Math.Sign(destino.Columna - origen.Columna);
            return new Coordenada(origen.Fila + deltaFila, origen.Columna + deltaColumna);
        }
    }
}
