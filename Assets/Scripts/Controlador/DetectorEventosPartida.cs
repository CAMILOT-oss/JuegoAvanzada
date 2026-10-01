using System.Collections.Generic;
using System.Diagnostics;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Observa el Modelo (solo lectura) y anota en el registro dos eventos que ocurren dentro de
    /// Tasks de segundo plano y que, por eso, no pasan por ningún método del GameController:
    ///   - una construcción que terminó (Edificio.EstaConstruido pasó a true);
    ///   - una unidad entrenada que apareció en el mapa.
    /// Se consulta desde GameController.Update() (hilo principal), como máximo cada
    /// IntervaloMilisegundos. Así no hace falta modificar ControladorConstruccion ni
    /// ControladorEntrenamiento, que ya funcionan.
    /// </summary>
    public sealed class DetectorEventosPartida
    {
        private const long IntervaloMilisegundos = 250;

        private readonly RegistroPartida _registro;
        private readonly HashSet<int> _edificiosCompletados = new HashSet<int>();
        private readonly HashSet<int> _unidadesConocidas = new HashSet<int>();
        private readonly Stopwatch _reloj = Stopwatch.StartNew();
        private long _ultimaRevision;

        public DetectorEventosPartida(RegistroPartida registro, Partida partidaInicial)
        {
            _registro = registro;

            // Lo que ya existe al arrancar (Centros Urbanos iniciales) no se anuncia como "nuevo".
            Sembrar(partidaInicial.Jugador1);
            Sembrar(partidaInicial.Jugador2);
        }

        private void Sembrar(Jugador jugador)
        {
            foreach (var e in jugador.Edificios)
                if (e.EstaConstruido) _edificiosCompletados.Add(e.Id);
            foreach (var u in jugador.Unidades)
                _unidadesConocidas.Add(u.Id);
        }

        /// <summary>Llamar una vez por frame; internamente se limita la frecuencia.</summary>
        public void Revisar(Partida partida)
        {
            long ahora = _reloj.ElapsedMilliseconds;
            if (ahora - _ultimaRevision < IntervaloMilisegundos) return;
            _ultimaRevision = ahora;

            Revisar(partida.Jugador1);
            Revisar(partida.Jugador2);
        }

        private void Revisar(Jugador jugador)
        {
            foreach (var edificio in jugador.Edificios)
            {
                if (edificio.EstaDestruido || !edificio.EstaConstruido) continue;
                if (!_edificiosCompletados.Add(edificio.Id)) continue;

                _registro.Registrar(jugador.Nombre, "Construcción",
                    $"Completada - {edificio.Nombre} (Id {edificio.Id}) en {edificio.Posicion}");
            }

            foreach (var unidad in jugador.Unidades)
            {
                if (!_unidadesConocidas.Add(unidad.Id)) continue;

                _registro.Registrar(jugador.Nombre, "Entrenamiento",
                    $"Completado - {unidad.Nombre} (Id {unidad.Id}) apareció en {unidad.Posicion}");
            }
        }
    }
}
