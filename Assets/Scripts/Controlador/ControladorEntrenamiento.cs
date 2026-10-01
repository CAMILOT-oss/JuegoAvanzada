using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Maneja el entrenamiento de una unidad desde un edificio (CentroUrbano -> Aldeano,
    /// Cuartel -> Soldado/Arquero). Descuenta el costo al iniciar y, tras el tiempo de
    /// entrenamiento, coloca la unidad nueva en una casilla libre junto al edificio,
    /// siempre "hacia adentro" del mapa (ver TryBuscarCasillaLibreAdyacente).
    /// </summary>
    public static class ControladorEntrenamiento
    {
        /// <param name="indiceSpawn">Número que elige, de forma DETERMINÍSTICA, cuál de las casillas
        /// candidatas junto al edificio usa esta unidad. Se pasa el Id acordado de la unidad, así
        /// las unidades no aparecen todas apiladas en la misma celda y, a la vez, las dos
        /// instancias del juego eligen exactamente la misma casilla.</param>
        public static bool IniciarEntrenamiento(
            Jugador jugador,
            Edificio edificioEntrenador,
            Func<string, Coordenada, Unidad> fabricaUnidad, // ej: (owner,pos) => new Soldado(owner,pos)
            Dictionary<TipoRecurso, int> costo,
            int tiempoEntrenamientoSegundos,
            int indiceSpawn,
            out Task tareaEntrenamiento,
            CancellationToken token = default)
        {
            tareaEntrenamiento = null;

            if (!ValidadorAcciones.PuedeEntrenar(jugador, edificioEntrenador, costo)) return false;
            if (!jugador.IntentarConsumirRecursos(costo)) return false;

            tareaEntrenamiento = EntrenarAsync(
                jugador, edificioEntrenador, fabricaUnidad, costo, tiempoEntrenamientoSegundos, indiceSpawn, token);
            return true;
        }

        private static Task EntrenarAsync(
            Jugador jugador, Edificio edificioEntrenador, Func<string, Coordenada, Unidad> fabricaUnidad,
            Dictionary<TipoRecurso, int> costo, int tiempoEntrenamientoSegundos, int indiceSpawn,
            CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(tiempoEntrenamientoSegundos * 1000, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (edificioEntrenador.EstaDestruido) return; // si volaron el edificio, se pierde el entrenamiento

                if (!TryBuscarCasillaLibreAdyacente(jugador.Mapa, edificioEntrenador, indiceSpawn, out Coordenada posicionSpawn))
                {
                    // No hay dónde colocar la unidad (caso extremo: edificio rodeado).
                    // Devolvemos los recursos en vez de crear una unidad "fantasma"
                    // que quedaría en Jugador.Unidades sin estar realmente en el mapa.
                    foreach (var par in costo)
                        jugador.AgregarRecurso(par.Key, par.Value);
                    return;
                }

                Unidad nuevaUnidad = fabricaUnidad(jugador.Nombre, posicionSpawn);

                lock (jugador.Mapa.BloqueoMapa)
                {
                    if (!jugador.Mapa.ColocarUnidad(nuevaUnidad, posicionSpawn))
                    {
                        foreach (var par in costo)
                            jugador.AgregarRecurso(par.Key, par.Value);
                        return;
                    }
                }
                jugador.AgregarUnidad(nuevaUnidad);
            }, token);
        }

        /// <summary>
        /// Busca dónde aparece la unidad recién entrenada. Solo mira las dos franjas
        /// "hacia adentro" del mapa desde el edificio: si el edificio está en la mitad
        /// superior/izquierda del mapa, busca a la derecha y abajo; si está en la mitad
        /// inferior/derecha (el caso espejado, por ejemplo el Centro Urbano del rival en
        /// la esquina opuesta), busca a la izquierda y arriba. Así nunca intenta spawnear
        /// fuera del mapa, sin importar en qué esquina esté el edificio.
        ///
        /// Junta hasta 8 casillas candidatas (de la más cercana a la más lejana) y elige una
        /// con "indiceSpawn" (módulo la cantidad de candidatas). No mira si ya hay otra unidad
        /// parada ahí: las unidades se pueden solapar, y depender de eso haría que el host y el
        /// cliente elijan casillas distintas para la misma unidad.
        /// Devuelve false si no encontró ninguna.
        /// </summary>
        private static bool TryBuscarCasillaLibreAdyacente(Mapa mapa, Edificio edificio, int indiceSpawn,
            out Coordenada resultado)
        {
            Coordenada posicion = edificio.Posicion;
            int ancho = edificio.Ancho;
            int alto = edificio.Alto;

            int signoFila = (posicion.Fila + alto / 2.0 < mapa.Filas / 2.0) ? 1 : -1;
            int signoColumna = (posicion.Columna + ancho / 2.0 < mapa.Columnas / 2.0) ? 1 : -1;

            const int maxCapas = 12;
            const int maxCandidatos = 8;
            var candidatos = new List<Coordenada>();

            for (int capa = 0; capa < maxCapas && candidatos.Count < maxCandidatos; capa++)
            {
                // Franja pegada al lado "de abajo" (o "de arriba", según signoFila).
                int fila = signoFila > 0 ? posicion.Fila + alto + capa : posicion.Fila - 1 - capa;
                for (int paso = 0; paso <= ancho - 1 + capa; paso++)
                {
                    int columna = signoColumna > 0 ? posicion.Columna + paso : posicion.Columna + ancho - 1 - paso;
                    var candidato = new Coordenada(fila, columna);
                    if (EsCasillaValida(mapa, candidato)) candidatos.Add(candidato);
                }

                // Franja pegada al lado "de la derecha" (o "de la izquierda").
                int columnaBorde = signoColumna > 0 ? posicion.Columna + ancho + capa : posicion.Columna - 1 - capa;
                for (int paso = 0; paso <= alto - 1 + capa; paso++)
                {
                    int filaCandidata = signoFila > 0 ? posicion.Fila + paso : posicion.Fila + alto - 1 - paso;
                    var candidato = new Coordenada(filaCandidata, columnaBorde);
                    if (EsCasillaValida(mapa, candidato)) candidatos.Add(candidato);
                }
            }

            if (candidatos.Count == 0)
            {
                resultado = default;
                return false;
            }

            resultado = candidatos[Math.Abs(indiceSpawn) % candidatos.Count];
            return true;
        }

        private static bool EsCasillaValida(Mapa mapa, Coordenada coordenada)
        {
            var casilla = mapa.ObtenerCasilla(coordenada);
            return casilla != null && casilla.EsTransitablePorUnidad;
        }
    }
}

