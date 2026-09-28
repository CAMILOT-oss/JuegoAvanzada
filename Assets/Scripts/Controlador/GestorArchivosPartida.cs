using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>Datos de arranque que el GameController le entrega al gestor para documentarlos
    /// en configuracion.txt (no viven en el Modelo porque son ajustes de la sesión).</summary>
    public sealed class ParametrosPartida
    {
        public string NombreJugadorLocal { get; set; }
        public int SemillaMapa { get; set; }
        public int RadioZonaSegura { get; set; }
        public bool EsHost { get; set; }
        public string IpHost { get; set; }
        public int Puerto { get; set; }
    }

    /// <summary>
    /// Manejo de archivos (System.IO) de la partida. Genera los tres archivos exigidos:
    ///   - configuracion.txt   : configuración inicial (se escribe una vez al arrancar).
    ///   - log_partida.txt     : bitácora de eventos (la escribe RegistroPartida en su hilo).
    ///   - resultado_final.txt : ganador, estadísticas y estado final del mapa (al terminar).
    ///
    /// Es parte del Controlador: lee el Modelo (Partida/Jugador/Mapa) pero NUNCA lo modifica,
    /// y no depende de UnityEngine (la carpeta de salida la decide GameController).
    /// Todos los métodos capturan sus excepciones: un fallo de disco no interrumpe el juego.
    /// </summary>
    public sealed class GestorArchivosPartida : IDisposable
    {
        public const string NombreConfiguracion = "configuracion.txt";
        public const string NombreLog = "log_partida.txt";
        public const string NombreResultado = "resultado_final.txt";

        private static readonly Encoding Utf8SinBom = new UTF8Encoding(false);

        public string Carpeta { get; }
        public RegistroPartida Registro { get; }

        public GestorArchivosPartida(string carpeta)
        {
            Carpeta = carpeta;
            try { Directory.CreateDirectory(carpeta); }
            catch (Exception) { /* RegistroPartida reportará el problema en ErrorInicial */ }

            Registro = new RegistroPartida(Path.Combine(carpeta, NombreLog));
        }

        // ------------------------------------------------------------------
        //  configuracion.txt
        // ------------------------------------------------------------------

        /// <summary>Guarda la configuración inicial. Devuelve null si salió bien o el mensaje de error.</summary>
        public string GuardarConfiguracion(Partida partida, ParametrosPartida p)
        {
            try
            {
                Jugador local = partida.Jugador1.Nombre == p.NombreJugadorLocal ? partida.Jugador1 : partida.Jugador2;
                Jugador rival = ReferenceEquals(local, partida.Jugador1) ? partida.Jugador2 : partida.Jugador1;

                var sb = new StringBuilder();
                Encabezado(sb, "IMPERIOS EN GUERRA - CONFIGURACIÓN INICIAL");
                sb.AppendLine($"Fecha de generación : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Generado por        : instancia de {p.NombreJugadorLocal}");
                sb.AppendLine();

                Seccion(sb, "PARÁMETROS GENERALES");
                sb.AppendLine($"Tamaño del mapa                 : {local.Mapa.Filas} x {local.Mapa.Columnas} casillas");
                sb.AppendLine($"Semilla de generación del mapa  : {p.SemillaMapa} (igual en ambas instancias)");
                sb.AppendLine($"Radio de zona segura (Centro U.): {p.RadioZonaSegura} casillas");
                sb.AppendLine($"Estado de la partida            : {partida.Estado}");
                sb.AppendLine();

                Seccion(sb, "COMUNICACIÓN EN RED");
                sb.AppendLine("Tecnología : Sockets TCP (System.Net.Sockets)");
                sb.AppendLine("Formato    : un mensaje JSON por línea (terminado en '\\n')");
                sb.AppendLine($"Rol local  : {(p.EsHost ? "Host (escucha conexiones)" : "Cliente (se conecta al host)")}");
                sb.AppendLine($"IP del host: {(p.EsHost ? "0.0.0.0 (todas las interfaces)" : p.IpHost)}");
                sb.AppendLine($"Puerto     : {p.Puerto}");
                sb.AppendLine();

                Seccion(sb, "JUGADORES");
                DescribirJugador(sb, local, "local");
                DescribirJugador(sb, rival, "rival");

                Seccion(sb, "DEPÓSITOS DE RECURSOS (mismos en ambos mapas)");
                var depositos = ListarDepositos(local.Mapa);
                sb.AppendLine($"Total de depósitos: {depositos.Count}");
                foreach (var grupo in depositos.GroupBy(d => d.Tipo))
                    sb.AppendLine($"  - {grupo.Key}: {grupo.Count()} depósitos, {grupo.Sum(d => d.CantidadDisponible)} unidades en total");
                sb.AppendLine();
                sb.AppendLine("  Tipo      Posición   Cantidad");
                foreach (var d in depositos)
                    sb.AppendLine($"  {d.Tipo,-9} {d.Posicion,-10} {d.CantidadDisponible}");
                sb.AppendLine();

                Seccion(sb, "TABLA DE BALANCE");
                sb.AppendLine("Costos de unidades:");
                sb.AppendLine($"  - Aldeano : {Costo(CostosJuego.CostoAldeano)} (entrenamiento {CostosJuego.TiempoEntrenamientoAldeanoSegundos} s)");
                sb.AppendLine($"  - Soldado : {Costo(CostosJuego.CostoSoldado)} (entrenamiento {CostosJuego.TiempoEntrenamientoSoldadoSegundos} s)");
                sb.AppendLine($"  - Arquero : {Costo(CostosJuego.CostoArquero)} (entrenamiento {CostosJuego.TiempoEntrenamientoArqueroSegundos} s)");
                sb.AppendLine("Costos de edificios:");
                sb.AppendLine($"  - Cuartel : {Costo(CostosJuego.CostoCuartel)} (tamaño 2x2)");
                sb.AppendLine($"  - Almacén : {Costo(CostosJuego.CostoAlmacen)} (tamaño 1x1)");
                sb.AppendLine($"Carga por viaje de recolección: {CostosJuego.CantidadExtraidaPorViaje} unidades");
                sb.AppendLine();

                Seccion(sb, "MAPA INICIAL DE CADA JUGADOR");
                sb.AppendLine(Leyenda());
                sb.AppendLine();
                sb.AppendLine($"Mapa de {local.Nombre} (local):");
                sb.AppendLine(RenderizarMapa(local));
                sb.AppendLine($"Mapa de {rival.Nombre} (rival):");
                sb.AppendLine(RenderizarMapa(rival));

                File.WriteAllText(Path.Combine(Carpeta, NombreConfiguracion), sb.ToString(), Utf8SinBom);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static void DescribirJugador(StringBuilder sb, Jugador j, string rol)
        {
            sb.AppendLine($"-- {j.Nombre} ({rol}) --");
            sb.AppendLine($"Recursos iniciales: Oro={j.ObtenerRecurso(TipoRecurso.Oro)}, " +
                          $"Madera={j.ObtenerRecurso(TipoRecurso.Madera)}, Comida={j.ObtenerRecurso(TipoRecurso.Comida)}");

            var edificios = j.Edificios;
            sb.AppendLine($"Edificios iniciales ({edificios.Count}):");
            foreach (var e in edificios)
                sb.AppendLine($"  - {e.Nombre} | Id={e.Id} | Posición={e.Posicion} | Tamaño={e.Ancho}x{e.Alto} | " +
                              $"Vida={e.VidaActual}/{e.VidaMaxima} | Construido={(e.EstaConstruido ? "Sí" : "No")}");

            var unidades = j.Unidades;
            sb.AppendLine($"Unidades iniciales ({unidades.Count}):" + (unidades.Count == 0 ? " ninguna" : ""));
            foreach (var u in unidades)
                sb.AppendLine($"  - {u.Nombre} | Id={u.Id} | Posición={u.Posicion} | Vida={u.VidaActual}/{u.VidaMaxima}");
            sb.AppendLine();
        }

        private static List<DepositoRecurso> ListarDepositos(Mapa mapa)
        {
            var lista = new List<DepositoRecurso>();
            for (int f = 0; f < mapa.Filas; f++)
                for (int c = 0; c < mapa.Columnas; c++)
                {
                    var deposito = mapa.ObtenerCasilla(new Coordenada(f, c))?.Recurso;
                    if (deposito != null) lista.Add(deposito);
                }
            return lista;
        }

        private static string Costo(Dictionary<TipoRecurso, int> costo) =>
            string.Join(", ", costo.Select(par => $"{par.Value} {par.Key}"));

        // ------------------------------------------------------------------
        //  resultado_final.txt
        // ------------------------------------------------------------------

        /// <summary>Guarda el resultado final. Devuelve null si salió bien o el mensaje de error.</summary>
        public string GuardarResultadoFinal(Partida partida, string nombreJugadorLocal)
        {
            try
            {
                Jugador local = partida.Jugador1.Nombre == nombreJugadorLocal ? partida.Jugador1 : partida.Jugador2;
                Jugador rival = ReferenceEquals(local, partida.Jugador1) ? partida.Jugador2 : partida.Jugador1;

                DateTime fin = partida.FechaFin ?? DateTime.Now;
                TimeSpan duracion = fin - partida.FechaInicio;

                var sb = new StringBuilder();
                Encabezado(sb, "IMPERIOS EN GUERRA - RESULTADO FINAL");

                Seccion(sb, "RESUMEN");
                sb.AppendLine($"Ganador            : {partida.Ganador}");
                sb.AppendLine($"Resultado (local)  : {ResultadoParaLocal(partida.Ganador, nombreJugadorLocal)}");
                sb.AppendLine($"Motivo             : {Motivo(partida, local, rival)}");
                sb.AppendLine($"Inicio de partida  : {partida.FechaInicio:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Fin de partida     : {fin:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Duración           : {duracion:hh\\:mm\\:ss}");
                sb.AppendLine($"Archivo generado en: instancia de {nombreJugadorLocal}");
                sb.AppendLine();

                Seccion(sb, "ESTADÍSTICAS POR JUGADOR");
                Estadisticas(sb, partida.Jugador1);
                Estadisticas(sb, partida.Jugador2);

                Seccion(sb, "ESTADO FINAL DEL MAPA");
                sb.AppendLine(Leyenda());
                sb.AppendLine();
                sb.AppendLine($"Mapa de {partida.Jugador1.Nombre}:");
                sb.AppendLine(RenderizarMapa(partida.Jugador1));
                sb.AppendLine($"Mapa de {partida.Jugador2.Nombre}:");
                sb.AppendLine(RenderizarMapa(partida.Jugador2));

                File.WriteAllText(Path.Combine(Carpeta, NombreResultado), sb.ToString(), Utf8SinBom);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static string ResultadoParaLocal(string ganador, string local) =>
            ganador == "Empate" ? "Empate" : (ganador == local ? "Victoria" : "Derrota");

        private static string Motivo(Partida partida, Jugador local, Jugador rival)
        {
            bool d1 = partida.Jugador1.EstaDerrotado();
            bool d2 = partida.Jugador2.EstaDerrotado();
            const string causa = "Centro Urbano destruido y sin unidades militares vivas";

            if (d1 && d2) return $"Ambos jugadores fueron derrotados a la vez ({causa}).";
            if (d1) return $"{partida.Jugador1.Nombre} fue derrotado ({causa}).";
            if (d2) return $"{partida.Jugador2.Nombre} fue derrotado ({causa}).";
            return "No determinado.";
        }

        private static void Estadisticas(StringBuilder sb, Jugador j)
        {
            var unidades = j.Unidades;
            var edificios = j.Edificios;

            var vivas = unidades.Where(u => u.EstaViva).ToList();
            int bajas = unidades.Count - vivas.Count;
            int edificiosEnPie = edificios.Count(e => !e.EstaDestruido);
            bool centro = j.ObtenerCentroUrbano() != null;

            sb.AppendLine($"-- {j.Nombre} --");
            sb.AppendLine($"Centro Urbano en pie : {(centro ? "Sí" : "No")}");
            sb.AppendLine($"Unidades vivas       : {vivas.Count} " + DesgloseUnidades(vivas));
            sb.AppendLine($"Unidades perdidas    : {bajas}");
            sb.AppendLine($"Edificios en pie     : {edificiosEnPie}");
            sb.AppendLine($"Edificios destruidos : {edificios.Count - edificiosEnPie}");
            sb.AppendLine($"Recursos finales     : Oro={j.ObtenerRecurso(TipoRecurso.Oro)}, " +
                          $"Madera={j.ObtenerRecurso(TipoRecurso.Madera)}, Comida={j.ObtenerRecurso(TipoRecurso.Comida)}");
            sb.AppendLine();
        }

        private static string DesgloseUnidades(List<Unidad> vivas)
        {
            if (vivas.Count == 0) return "";
            return "(" + string.Join(", ", vivas.GroupBy(u => u.Nombre).Select(g => $"{g.Key}: {g.Count()}")) + ")";
        }

        // ------------------------------------------------------------------
        //  Mapa en texto
        // ------------------------------------------------------------------

        private static string Leyenda() =>
            "Leyenda: . libre | O oro | M madera | F comida | C Centro Urbano | Q Cuartel | A Almacén | " +
            "a Aldeano | s Soldado | r Arquero";

        /// <summary>
        /// Dibuja el mapa de un jugador como una cuadrícula de texto. Se construye a partir de las
        /// listas de entidades del jugador (no de Casilla.UnidadOcupante, que solo guarda UNA unidad
        /// por casilla aunque haya varias superpuestas). Prioridad de dibujo: unidad > edificio > recurso.
        /// </summary>
        private static string RenderizarMapa(Jugador jugador)
        {
            Mapa mapa = jugador.Mapa;
            var celdas = new char[mapa.Filas, mapa.Columnas];

            for (int f = 0; f < mapa.Filas; f++)
                for (int c = 0; c < mapa.Columnas; c++)
                {
                    var recurso = mapa.ObtenerCasilla(new Coordenada(f, c))?.Recurso;
                    celdas[f, c] = recurso != null && !recurso.EstaAgotado ? SimboloRecurso(recurso.Tipo) : '.';
                }

            foreach (var edificio in jugador.Edificios)
            {
                if (edificio.EstaDestruido) continue;
                foreach (var celda in mapa.ObtenerCeldasOcupadas(edificio.Posicion, edificio.Ancho, edificio.Alto))
                    if (mapa.EstaDentroDelMapa(celda))
                        celdas[celda.Fila, celda.Columna] = SimboloEdificio(edificio);
            }

            foreach (var unidad in jugador.Unidades)
            {
                if (!unidad.EstaViva || !mapa.EstaDentroDelMapa(unidad.Posicion)) continue;
                celdas[unidad.Posicion.Fila, unidad.Posicion.Columna] = SimboloUnidad(unidad);
            }

            var sb = new StringBuilder();
            sb.Append("    ");
            for (int c = 0; c < mapa.Columnas; c++) sb.Append(c.ToString("D2")).Append(' ');
            sb.AppendLine();

            for (int f = 0; f < mapa.Filas; f++)
            {
                sb.Append(f.ToString("D2")).Append(" |");
                for (int c = 0; c < mapa.Columnas; c++) sb.Append(celdas[f, c]).Append("  ");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static char SimboloRecurso(TipoRecurso tipo) => tipo switch
        {
            TipoRecurso.Oro => 'O',
            TipoRecurso.Madera => 'M',
            TipoRecurso.Comida => 'F',
            _ => '?'
        };

        private static char SimboloEdificio(Edificio e) => e switch
        {
            CentroUrbano => 'C',
            Cuartel => 'Q',
            Almacen => 'A',
            _ => '#'
        };

        private static char SimboloUnidad(Unidad u) => u switch
        {
            Aldeano => 'a',
            Soldado => 's',
            Arquero => 'r',
            _ => 'u'
        };

        // ------------------------------------------------------------------
        //  Formato común
        // ------------------------------------------------------------------

        private static void Encabezado(StringBuilder sb, string titulo)
        {
            sb.AppendLine("=====================================================");
            sb.AppendLine(" " + titulo);
            sb.AppendLine("=====================================================");
            sb.AppendLine();
        }

        private static void Seccion(StringBuilder sb, string titulo)
        {
            sb.AppendLine("[" + titulo + "]");
            sb.AppendLine(new string('-', titulo.Length + 2));
        }

        public void Dispose() => Registro?.Dispose();
    }
}
