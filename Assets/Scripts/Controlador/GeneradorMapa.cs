using System;
using System.Collections.Generic;
using System.Linq;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Puebla un Mapa recién creado con depósitos de recurso (oro, madera, comida)
    /// sobre un terreno que se mantiene TipoTerreno.Libre en todo el resto (sin
    /// bosque/agua/montaña — todo el mapa queda transitable). Es una clase
    /// Modelo-only (no depende de UnityEngine), así que vive junto al resto del
    /// Controlador y se llama una sola vez, al armar la partida, ANTES de que la
    /// Vista genere la grilla visual.
    ///
    /// No toca ninguna casilla que ya tenga un Edificio, ni ninguna casilla dentro
    /// de una "zona segura" (un radio alrededor de un punto, típicamente el Centro
    /// Urbano inicial) — así el jugador siempre arranca con espacio libre alrededor
    /// para construir y para que las unidades entrenadas tengan dónde aparecer.
    /// </summary>
    public static class GeneradorMapa
    {
        /// <summary>
        /// Coloca los depósitos de recurso sobre "mapa". "semilla" hace la generación
        /// reproducible: la misma semilla siempre da el mismo mapa (útil para
        /// depurar). "zonasSeguras" es una lista de (centro, radio) que el generador
        /// nunca toca.
        /// </summary>
        public static void Generar(
            Mapa mapa,
            int semilla,
            int cantidadYacimientosOro = 10,
            int cantidadYacimientosMadera = 14,
            int cantidadZonasComida = 10,
            int cantidadInicialOro = 500,
            int cantidadInicialMadera = 400,
            int cantidadInicialComida = 300,
            IEnumerable<(Coordenada centro, int radio)> zonasSeguras = null)
        {
            var random = new Random(semilla);
            var zonas = zonasSeguras?.ToList() ?? new List<(Coordenada centro, int radio)>();

            // Ya no se genera bosque/agua/montaña: toda casilla que no reciba un
            // depósito de recurso queda como TipoTerreno.Libre (el valor por defecto
            // de Casilla), así que todo el mapa es transitable.
            ColocarDepositos(mapa, random, TipoRecurso.Oro, TipoTerreno.YacimientoOro,
                cantidadYacimientosOro, cantidadInicialOro, zonas);
            ColocarDepositos(mapa, random, TipoRecurso.Madera, TipoTerreno.YacimientoMadera,
                cantidadYacimientosMadera, cantidadInicialMadera, zonas);
            ColocarDepositos(mapa, random, TipoRecurso.Comida, TipoTerreno.ZonaComida,
                cantidadZonasComida, cantidadInicialComida, zonas);
        }

        private static bool EstaEnZonaSegura(Coordenada coordenada, List<(Coordenada centro, int radio)> zonas)
        {
            foreach (var (centro, radio) in zonas)
                if (centro.DistanciaA(coordenada) <= radio)
                    return true;
            return false;
        }

        /// <summary>Ubica "cantidad" depósitos del tipo dado en casillas libres y fuera de
        /// zonas seguras, eligiendo posiciones al azar hasta lograrlo o agotar los intentos.</summary>
        private static void ColocarDepositos(
            Mapa mapa, Random random, TipoRecurso tipoRecurso, TipoTerreno terrenoAsociado,
            int cantidad, int cantidadInicial, List<(Coordenada centro, int radio)> zonasSeguras)
        {
            int colocados = 0;
            int intentos = 0;
            int maxIntentos = Math.Max(cantidad * 40, 100);

            while (colocados < cantidad && intentos < maxIntentos)
            {
                intentos++;

                var coordenada = new Coordenada(random.Next(mapa.Filas), random.Next(mapa.Columnas));
                if (EstaEnZonaSegura(coordenada, zonasSeguras)) continue;

                var casilla = mapa.ObtenerCasilla(coordenada);
                if (casilla == null || casilla.Edificio != null || casilla.Recurso != null) continue;

                var deposito = new DepositoRecurso(tipoRecurso, coordenada, cantidadInicial);
                if (mapa.ColocarRecurso(deposito, coordenada, terrenoAsociado))
                    colocados++;
            }
        }
    }
}
