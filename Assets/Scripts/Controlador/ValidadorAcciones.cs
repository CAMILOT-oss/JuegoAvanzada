using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Funciones de validación puras (no modifican nada, solo responden sí/no).
    /// Se usan antes de ejecutar cualquier acción, tanto si la pidió el jugador local
    /// por UI como si llegó por red desde el oponente (¡nunca hay que confiar ciegamente
    /// en lo que llega por red: siempre se revalida acá también!).
    /// </summary>
    public static class ValidadorAcciones
    {
        public static bool PuedeMoverse(Unidad unidad, Coordenada destino, Mapa mapa)
        {
            if (unidad == null || !unidad.EstaViva) return false;
            if (!mapa.EstaDentroDelMapa(destino)) return false;

            var casilla = mapa.ObtenerCasilla(destino);
            // EsTransitablePorUnidad (no EsTransitable): las unidades se pueden pasar
            // por encima entre sí, no se bloquean entre ellas al moverse.
            return casilla != null && casilla.EsTransitablePorUnidad;
        }

        /// <summary>
        /// ¿Se puede DAR LA ORDEN de atacar? No mira el rango: si el objetivo está lejos,
        /// ControladorCombate hace que la unidad lo persiga hasta quedar a tiro.
        /// </summary>
        public static bool PuedeOrdenarAtaque(Unidad atacante, Unidad objetivo)
        {
            if (atacante == null || objetivo == null) return false;
            if (!atacante.EstaViva || !objetivo.EstaViva) return false;
            return atacante.Propietario != objetivo.Propietario; // no fuego amigo
        }

        /// <summary>¿Puede pegar AHORA MISMO? (vivos, enemigos y dentro de rango).</summary>
        public static bool PuedeAtacar(Unidad atacante, Unidad objetivo)
        {
            if (atacante == null || objetivo == null) return false;
            if (!atacante.EstaViva || !objetivo.EstaViva) return false;
            if (atacante.Propietario == objetivo.Propietario) return false; // no fuego amigo

            return atacante.PuedeAtacar(objetivo.Posicion);
        }

        public static bool PuedeConstruir(Jugador jugador, Coordenada posicion,
            System.Collections.Generic.Dictionary<TipoRecurso, int> costo, int ancho = 1, int alto = 1)
        {
            if (jugador == null) return false;

            // Valida TODO el rectángulo que va a ocupar el edificio (ancho x alto celdas
            // a partir de "posicion" como esquina superior-izquierda), no una sola celda.
            // PuedeColocarEdificio ya rechaza, entre otras cosas, cualquier celda que
            // tenga un depósito de recurso (Mapa.cs), así que nunca se puede construir
            // encima de un yacimiento.
            if (!jugador.Mapa.PuedeColocarEdificio(posicion, ancho, alto)) return false;

            // Solo comprobamos que alcance; el descuento real lo hace IntentarConsumirRecursos
            // en el mismo momento de ejecutar, para que sea atómico.
            foreach (var par in costo)
                if (jugador.ObtenerRecurso(par.Key) < par.Value)
                    return false;

            return true;
        }

        public static bool PuedeEntrenar(Jugador jugador, Edificio edificioEntrenador,
            System.Collections.Generic.Dictionary<TipoRecurso, int> costo)
        {
            if (jugador == null || edificioEntrenador == null) return false;
            if (!edificioEntrenador.EstaConstruido || edificioEntrenador.EstaDestruido) return false;

            foreach (var par in costo)
                if (jugador.ObtenerRecurso(par.Key) < par.Value)
                    return false;

            return true;
        }
    }
}
