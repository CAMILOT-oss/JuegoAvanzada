using System.Collections.Generic;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Representa el mapa de un jugador como una matriz de Casillas (por defecto 15x15).
    /// Es responsabilidad del Controlador invocar estos métodos siempre dentro de un
    /// lock (por ejemplo, un objeto de sincronización propio del Mapa) ya que varios
    /// hilos (movimiento, construcción, ataques) pueden intentar modificarlo a la vez.
    /// </summary>
    public class Mapa
    {
        public const int TamanoPorDefecto = 15;

        public int Filas { get; }
        public int Columnas { get; }

        // Objeto exclusivo para sincronizar el acceso concurrente al mapa (lock).
        public readonly object BloqueoMapa = new object();

        private readonly Casilla[,] _casillas;

        public Mapa(int filas = TamanoPorDefecto, int columnas = TamanoPorDefecto)
        {
            Filas = filas;
            Columnas = columnas;
            _casillas = new Casilla[filas, columnas];

            for (int f = 0; f < filas; f++)
                for (int c = 0; c < columnas; c++)
                    _casillas[f, c] = new Casilla(new Coordenada(f, c));
        }

        public bool EstaDentroDelMapa(Coordenada pos) =>
            pos.Fila >= 0 && pos.Fila < Filas && pos.Columna >= 0 && pos.Columna < Columnas;

        public Casilla ObtenerCasilla(Coordenada pos) =>
            EstaDentroDelMapa(pos) ? _casillas[pos.Fila, pos.Columna] : null;

        /// <summary>
        /// Devuelve todas las coordenadas que ocuparía un edificio de tamaño (ancho x alto)
        /// si su esquina superior-izquierda estuviera en "pos". Útil tanto para validar
        /// antes de construir como para colocar/remover uno ya existente.
        /// </summary>
        public IEnumerable<Coordenada> ObtenerCeldasOcupadas(Coordenada pos, int ancho, int alto)
        {
            for (int df = 0; df < alto; df++)
                for (int dc = 0; dc < ancho; dc++)
                    yield return new Coordenada(pos.Fila + df, pos.Columna + dc);
        }

        /// <summary>
        /// Indica si TODAS las celdas que ocuparía un edificio de tamaño (ancho x alto)
        /// están dentro del mapa, sin agua/montaña/edificio, y sin ningún depósito de recurso
        /// (para que nunca quede un yacimiento tapado debajo de un edificio).
        /// Una unidad parada encima NO impide construir (las unidades se pasan por encima y,
        /// entre las dos instancias, nunca están exactamente en la misma celda al mismo tiempo).
        /// Se usa antes de gastar recursos/construir.
        /// </summary>
        public bool PuedeColocarEdificio(Coordenada pos, int ancho, int alto)
        {
            foreach (var celda in ObtenerCeldasOcupadas(pos, ancho, alto))
            {
                var casilla = ObtenerCasilla(celda);
                if (casilla == null || !casilla.EsTransitablePorUnidad || casilla.Recurso != null) return false;
            }
            return true;
        }

        /// <summary>
        /// Coloca un edificio ya validado en el mapa, ocupando TODAS las celdas de su
        /// rectángulo (Ancho x Alto), no solo "pos". Devuelve false si alguna celda no es válida
        /// (en ese caso no deja el edificio colocado a medias: no toca ninguna celda).
        /// </summary>
        public bool ColocarEdificio(Edificio edificio, Coordenada pos)
        {
            if (!PuedeColocarEdificio(pos, edificio.Ancho, edificio.Alto)) return false;

            foreach (var celda in ObtenerCeldasOcupadas(pos, edificio.Ancho, edificio.Alto))
                ObtenerCasilla(celda).Edificio = edificio;

            edificio.Posicion = pos;
            return true;
        }

        /// <summary>Coloca una unidad ya validada en el mapa. Devuelve false si no es posible.
        /// Usa EsTransitablePorUnidad: puede colocarse sobre una celda donde ya hay otra
        /// unidad (se permiten pasar por encima / compartir celda transitoriamente).</summary>
        public bool ColocarUnidad(Unidad unidad, Coordenada pos)
        {
            var casilla = ObtenerCasilla(pos);
            if (casilla == null || !casilla.EsTransitablePorUnidad) return false;

            casilla.UnidadOcupante = unidad;
            unidad.Posicion = pos;
            return true;
        }

        /// <summary>
        /// Mueve una unidad de su casilla actual a "destino" si es válido. Usa
        /// EsTransitablePorUnidad, así que dos unidades pueden cruzarse sin bloquearse.
        /// </summary>
        public bool MoverUnidad(Unidad unidad, Coordenada destino)
        {
            var casillaDestino = ObtenerCasilla(destino);
            if (casillaDestino == null || !casillaDestino.EsTransitablePorUnidad) return false;

            var casillaOrigen = ObtenerCasilla(unidad.Posicion);
            if (casillaOrigen != null && casillaOrigen.UnidadOcupante == unidad)
                casillaOrigen.UnidadOcupante = null;

            casillaDestino.UnidadOcupante = unidad;
            unidad.Posicion = destino;
            return true;
        }

        /// <summary>Quita una unidad del mapa (por ejemplo, al ser destruida).</summary>
        public void RemoverUnidad(Unidad unidad)
        {
            var casilla = ObtenerCasilla(unidad.Posicion);
            if (casilla != null && casilla.UnidadOcupante == unidad)
                casilla.UnidadOcupante = null;
        }

        /// <summary>Quita un edificio del mapa (por ejemplo, al ser destruido), liberando TODAS sus celdas.</summary>
        public void RemoverEdificio(Edificio edificio)
        {
            foreach (var celda in ObtenerCeldasOcupadas(edificio.Posicion, edificio.Ancho, edificio.Alto))
            {
                var casilla = ObtenerCasilla(celda);
                if (casilla != null && casilla.Edificio == edificio)
                    casilla.Edificio = null;
            }
        }

        /// <summary>Devuelve todas las casillas dentro de un radio (distancia de Chebyshev).</summary>
        public List<Casilla> ObtenerCasillasEnRadio(Coordenada centro, int radio)
        {
            var resultado = new List<Casilla>();
            for (int f = 0; f < Filas; f++)
            {
                for (int c = 0; c < Columnas; c++)
                {
                    var pos = new Coordenada(f, c);
                    if (centro.DistanciaA(pos) <= radio)
                        resultado.Add(_casillas[f, c]);
                }
            }
            return resultado;
        }

        /// <summary>Ubica un depósito de recurso en una casilla y ajusta su terreno.</summary>
        public bool ColocarRecurso(DepositoRecurso deposito, Coordenada pos, TipoTerreno terrenoAsociado)
        {
            var casilla = ObtenerCasilla(pos);
            if (casilla == null || casilla.Recurso != null) return false;

            casilla.Recurso = deposito;
            casilla.Terreno = terrenoAsociado;
            return true;
        }
    }
}
