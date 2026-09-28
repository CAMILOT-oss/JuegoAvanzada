using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Maneja la construcción de un edificio: descuenta recursos, lo coloca en el mapa
    /// (visualmente "en obra", porque EstaConstruido empieza en false) y tras el tiempo
    /// correspondiente lo marca como terminado.
    /// </summary>
    public static class ControladorConstruccion
    {
        /// <summary>
        /// Intenta iniciar la construcción. Devuelve null si no se pudo iniciar
        /// (recursos insuficientes o casilla ocupada), o el edificio recién creado si sí.
        /// El Task devuelto sigue corriendo en segundo plano hasta terminar la obra.
        /// </summary>
        public static Edificio IniciarConstruccion(
            Jugador jugador,
            Func<string, Coordenada, Edificio> fabricaEdificio, // ej: (owner,pos) => new Cuartel(owner,pos)
            Dictionary<TipoRecurso, int> costo,
            Coordenada posicion,
            out Task tareaConstruccion,
            int ancho = 1,
            int alto = 1,
            CancellationToken token = default)
        {
            tareaConstruccion = null;

            lock (jugador.Mapa.BloqueoMapa)
            {
                // Se valida el rectángulo completo (ancho x alto) ANTES de crear la instancia,
                // porque todavía no existe el objeto Edificio del que leer su propio tamaño.
                if (!ValidadorAcciones.PuedeConstruir(jugador, posicion, costo, ancho, alto))
                    return null;

                if (!jugador.IntentarConsumirRecursos(costo))
                    return null;

                Edificio edificio = fabricaEdificio(jugador.Nombre, posicion);

                if (!jugador.Mapa.ColocarEdificio(edificio, posicion))
                {
                    // No se pudo colocar pese a la validación previa (carrera con otra acción):
                    // devolvemos los recursos para no perderlos.
                    DevolverRecursos(jugador, costo);
                    return null;
                }

                jugador.AgregarEdificio(edificio);

                tareaConstruccion = ConstruirAsync(edificio, token);
                return edificio;
            }
        }

        private static Task ConstruirAsync(Edificio edificio, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(edificio.TiempoConstruccionSegundos * 1000, token).ConfigureAwait(false);
                    if (!edificio.EstaDestruido)
                        edificio.EstaConstruido = true;
                }
                catch (TaskCanceledException)
                {
                    // Construcción cancelada (ej: partida terminó); no hace falta hacer nada más.
                }
            }, token);
        }

        private static void DevolverRecursos(Jugador jugador, Dictionary<TipoRecurso, int> costo)
        {
            foreach (var par in costo)
                jugador.AgregarRecurso(par.Key, par.Value);
        }
    }
}
