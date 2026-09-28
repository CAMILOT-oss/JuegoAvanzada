using System;
using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Combate: la unidad PERSIGUE a su objetivo hasta quedar dentro de su rango y
    /// después le pega (un golpe por segundo) hasta que muera, o hasta que se cancele
    /// el token (por ejemplo, porque le dieron otra orden).
    ///
    /// Antes, si el objetivo estaba fuera de rango al dar la orden, el ataque no hacía
    /// nada: no había ninguna lógica de acercamiento. Un Soldado (rango 1) o un Arquero
    /// (rango 3) casi nunca están lo bastante cerca al hacer clic, así que "no podían atacar".
    ///
    /// "mapaDelAtacante" es el Mapa del DUEÑO del atacante (el del jugador local si lo
    /// ordenó el jugador local; el del rival si la orden llegó por red).
    /// </summary>
    public static class ControladorCombate
    {
        private const int MilisegundosPorAtaque = 1000;

        /// <summary>Se dispara (desde el hilo del atacante) cuando un golpe deja a una unidad en 0 de vida.
        /// Lo usa GameController para anotarlo en log_partida.txt; puede dispararse más de una vez
        /// por la misma baja si dos atacantes la rematan a la vez, quien escuche debe tolerarlo.</summary>
        public static event Action<Unidad, Unidad> UnidadDestruida;

        /// <summary>Igual que UnidadDestruida, pero para un edificio (atacante, edificio).</summary>
        public static event Action<Unidad, Edificio> EdificioDestruido;

        public static Task AtacarAsync(Unidad atacante, Unidad objetivo, Mapa mapaDelAtacante,
            CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && atacante.EstaViva && objetivo.EstaViva)
                    {
                        if (atacante.PuedeAtacar(objetivo.Posicion))
                        {
                            atacante.Estado = EstadoUnidad.Atacando;
                            objetivo.RecibirDano(atacante.Ataque);
                            if (!objetivo.EstaViva) NotificarSinRiesgo(() => UnidadDestruida?.Invoke(atacante, objetivo));
                            await Task.Delay(MilisegundosPorAtaque, token).ConfigureAwait(false);
                        }
                        else
                        {
                            atacante.Estado = EstadoUnidad.Moviendose;
                            if (!DarPasoHacia(atacante, objetivo.Posicion, mapaDelAtacante))
                                break; // no hay por dónde acercarse

                            await Task.Delay(MilisegundosPorCelda(atacante), token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Le dieron otra orden a mitad de camino: es normal, no hay nada que limpiar.
                }
                finally
                {
                    if (atacante.EstaViva)
                        atacante.Estado = EstadoUnidad.Inactiva;
                }
            }, token);
        }

        /// <summary>Variante para atacar un edificio (por ejemplo, asediar el Centro Urbano enemigo).
        /// La distancia se mide hasta la celda MÁS CERCANA del edificio (ocupa Ancho x Alto celdas),
        /// no hasta su esquina superior-izquierda.</summary>
        public static Task AtacarEdificioAsync(Unidad atacante, Edificio objetivo, Mapa mapaDelAtacante,
            CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && atacante.EstaViva && !objetivo.EstaDestruido)
                    {
                        if (DistanciaAEdificio(atacante.Posicion, objetivo) <= atacante.RangoAtaque)
                        {
                            atacante.Estado = EstadoUnidad.Atacando;
                            objetivo.RecibirDano(atacante.Ataque);
                            if (objetivo.EstaDestruido) NotificarSinRiesgo(() => EdificioDestruido?.Invoke(atacante, objetivo));
                            await Task.Delay(MilisegundosPorAtaque, token).ConfigureAwait(false);
                        }
                        else
                        {
                            atacante.Estado = EstadoUnidad.Moviendose;
                            if (!DarPasoHacia(atacante, CeldaMasCercana(atacante.Posicion, objetivo), mapaDelAtacante))
                                break;

                            await Task.Delay(MilisegundosPorCelda(atacante), token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    if (atacante.EstaViva)
                        atacante.Estado = EstadoUnidad.Inactiva;
                }
            }, token);
        }

        /// <summary>Ejecuta una notificación sin permitir que un error en quien escucha
        /// (por ejemplo, el registro) interrumpa el combate.</summary>
        private static void NotificarSinRiesgo(Action notificacion)
        {
            try { notificacion(); }
            catch (Exception) { }
        }

        private static int MilisegundosPorCelda(Unidad unidad) =>
            unidad.Velocidad > 0 ? 1000 / unidad.Velocidad : 1000;

        /// <summary>Da UN paso hacia "destino": primero en diagonal; si está bloqueado
        /// (por ejemplo por un edificio), prueba solo en fila y después solo en columna.
        /// Devuelve false si no pudo avanzar por ningún lado.</summary>
        private static bool DarPasoHacia(Unidad unidad, Coordenada destino, Mapa mapa)
        {
            Coordenada actual = unidad.Posicion;
            int df = Math.Sign(destino.Fila - actual.Fila);
            int dc = Math.Sign(destino.Columna - actual.Columna);

            var opciones = new[]
            {
                new Coordenada(actual.Fila + df, actual.Columna + dc),
                new Coordenada(actual.Fila + df, actual.Columna),
                new Coordenada(actual.Fila, actual.Columna + dc)
            };

            lock (mapa.BloqueoMapa)
            {
                foreach (var paso in opciones)
                {
                    if (paso == actual) continue;
                    if (!ValidadorAcciones.PuedeMoverse(unidad, paso, mapa)) continue;

                    mapa.MoverUnidad(unidad, paso);
                    return true;
                }
            }
            return false;
        }

        private static int DistanciaAEdificio(Coordenada desde, Edificio edificio)
        {
            Coordenada cercana = CeldaMasCercana(desde, edificio);
            return desde.DistanciaA(cercana);
        }

        /// <summary>La celda del rectángulo del edificio que queda más cerca de "desde".</summary>
        private static Coordenada CeldaMasCercana(Coordenada desde, Edificio edificio)
        {
            int fila = Math.Min(Math.Max(desde.Fila, edificio.Posicion.Fila),
                                edificio.Posicion.Fila + edificio.Alto - 1);
            int columna = Math.Min(Math.Max(desde.Columna, edificio.Posicion.Columna),
                                   edificio.Posicion.Columna + edificio.Ancho - 1);
            return new Coordenada(fila, columna);
        }
    }
}
