using System.Threading;
using System.Threading.Tasks;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Ciclo de vida de un Aldeano recolectando: caminar hasta el depósito, extraer,
    /// caminar de vuelta al Centro Urbano/Almacén más cercano, descargar, repetir.
    /// Corre como un único Task de larga duración por Aldeano asignado a recolectar.
    /// </summary>
    public static class ControladorRecoleccion
    {
        /// <summary>Distancia máxima (Chebyshev) a la que un Aldeano ya puede extraer del
        /// depósito. Con 1, alcanza con estar en cualquiera de las 8 celdas alrededor
        /// (o encima), así varios Aldeanos pueden juntarse en el mismo depósito sin
        /// pelearse por la única celda exacta donde está el recurso.</summary>
        private const int RangoRecoleccion = 1;

        public static Task RecolectarAsync(Aldeano aldeano, DepositoRecurso deposito, Jugador jugador,
            Coordenada puntoDeEntrega, CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                while (aldeano.EstaViva && !deposito.EstaAgotado && !token.IsCancellationRequested)
                {
                    // 1) Acercarse al depósito (a una celda adyacente si la celda exacta
                    //    está ocupada por otro Aldeano). Si no logró quedar a rango
                    //    (bloqueado), esperamos un toque y reintentamos, EN VEZ DE
                    //    extraer igual sin haber llegado — ese era el bug.
                    bool llegoARango = await AcercarseAAsync(aldeano, deposito.Posicion, jugador.Mapa, token);
                    if (!aldeano.EstaViva || token.IsCancellationRequested) break;
                    if (!llegoARango)
                    {
                        await Task.Delay(400, token).ConfigureAwait(false);
                        continue;
                    }

                    // 2) Extraer. El depósito puede ser compartido por varios Aldeanos:
                    //    se sincroniza con un lock sobre el propio depósito.
                    aldeano.Estado = EstadoUnidad.Recolectando;
                    int extraido;
                    lock (deposito)
                    {
                        extraido = deposito.Extraer(CostosJuego.CantidadExtraidaPorViaje);
                    }
                    aldeano.Cargar(extraido);

                    if (extraido == 0) break; // se agotó justo antes de que llegáramos a extraer

                    await Task.Delay(1000, token).ConfigureAwait(false); // tiempo de "picar" el recurso

                    // 3) Volver a entregar.
                    await ControladorMovimiento.MoverAsync(aldeano, puntoDeEntrega, jugador.Mapa, token);
                    if (!aldeano.EstaViva || token.IsCancellationRequested) break;

                    int cargaEntregada = aldeano.Descargar();
                    jugador.AgregarRecurso(deposito.Tipo, cargaEntregada);
                }

                if (aldeano.EstaViva)
                    aldeano.Estado = EstadoUnidad.Inactiva;
            }, token);
        }

        /// <summary>
        /// Camina hasta quedar a distancia &lt;= RangoRecoleccion del depósito. Si la celda
        /// exacta está ocupada, busca la celda libre más cercana alrededor y va ahí en
        /// su lugar. Devuelve true solo si de verdad terminó a rango (no simplemente
        /// "se movió"): esto es lo que evita extraer recursos sin haber llegado.
        /// </summary>
        private static async Task<bool> AcercarseAAsync(Unidad unidad, Coordenada objetivo, Mapa mapa, CancellationToken token)
        {
            if (unidad.Posicion.DistanciaA(objetivo) <= RangoRecoleccion)
                return true;

            Coordenada destino = BuscarCeldaLibreCerca(mapa, objetivo) ?? objetivo;
            await ControladorMovimiento.MoverAsync(unidad, destino, mapa, token);

            return unidad.Posicion.DistanciaA(objetivo) <= RangoRecoleccion;
        }

        /// <summary>Busca una celda transitable a 1 casilla del centro (o el propio centro
        /// si está libre). Devuelve null si no encontró ninguna (caso extremo).</summary>
        private static Coordenada? BuscarCeldaLibreCerca(Mapa mapa, Coordenada centro)
        {
            var celdaCentro = mapa.ObtenerCasilla(centro);
            if (celdaCentro != null && celdaCentro.EsTransitable)
                return centro;

            foreach (var casilla in mapa.ObtenerCasillasEnRadio(centro, RangoRecoleccion))
            {
                if (casilla.Posicion != centro && casilla.EsTransitable)
                    return casilla.Posicion;
            }
            return null;
        }
    }
}
