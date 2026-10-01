using System;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Representa el estado completo de una partida entre dos jugadores.
    /// El Controlador consulta y actualiza esta clase; la Vista solo la lee para mostrarla.
    /// </summary>
    public class Partida
    {
        public Jugador Jugador1 { get; }
        public Jugador Jugador2 { get; }

        public EstadoPartida Estado { get; set; } = EstadoPartida.EnPreparacion;
        public string Ganador { get; private set; } // null mientras no haya terminado
        public DateTime FechaInicio { get; private set; }
        public DateTime? FechaFin { get; private set; }

        public Partida(Jugador jugador1, Jugador jugador2)
        {
            Jugador1 = jugador1;
            Jugador2 = jugador2;
        }

        public void IniciarPartida()
        {
            Estado = EstadoPartida.EnCurso;
            FechaInicio = DateTime.Now;
        }

        /// <summary>
        /// Comprueba si alguno de los dos jugadores fue derrotado.
        /// Si es así, marca la partida como Finalizada y registra al ganador.
        /// Devuelve true si la partida terminó en esta verificación.
        /// </summary>
        public bool VerificarGanador()
        {
            if (Estado == EstadoPartida.Finalizada) return true;

            bool derrota1 = Jugador1.EstaDerrotado();
            bool derrota2 = Jugador2.EstaDerrotado();

            if (derrota1 && derrota2)
            {
                FinalizarPartida("Empate");
                return true;
            }
            if (derrota1)
            {
                FinalizarPartida(Jugador2.Nombre);
                return true;
            }
            if (derrota2)
            {
                FinalizarPartida(Jugador1.Nombre);
                return true;
            }

            return false;
        }

        private void FinalizarPartida(string ganador)
        {
            Ganador = ganador;
            Estado = EstadoPartida.Finalizada;
            FechaFin = DateTime.Now;
        }
    }
}
