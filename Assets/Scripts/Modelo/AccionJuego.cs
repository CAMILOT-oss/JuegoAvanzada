using System;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// DTO (Data Transfer Object) que representa una acción de un jugador.
    /// Es la clase que se serializa a JSON (System.Text.Json / Newtonsoft.Json) y se envía
    /// por sockets/WebSockets/REST a la instancia del oponente. Al ser un POCO plano,
    /// pertenece al Modelo; el Controlador es quien la crea, la serializa y la interpreta.
    /// </summary>
    [Serializable]
    public class AccionJuego
    {
        public TipoAccion Tipo { get; set; }
        public string JugadorOrigen { get; set; }
        public Coordenada Origen { get; set; }
        public Coordenada Destino { get; set; }

        /// <summary>
        /// Identifica qué se construye/entrena/mueve/ataca, por ejemplo "Cuartel", "Soldado",
        /// o el Id (como string) de la unidad/edificio objetivo en un ataque.
        /// </summary>
        public string IdentificadorEntidad { get; set; }

        /// <summary>
        /// Id (como string) de la unidad ATACANTE, solo usado en Atacar/AtacarEdificio.
        /// Es necesario para que quien reciba la acción pueda revalidar rango y aplicar
        /// el daño REAL del atacante en vez de un valor fijo — nunca hay que confiar en
        /// que el emisor ya validó todo correctamente.
        /// </summary>
        public string IdentificadorAtacante { get; set; }

        /// <summary>
        /// Id (como string) que va a tener la unidad/edificio NUEVO, solo usado en
        /// Construir/EntrenarUnidad. Lo decide el emisor y el receptor lo respeta, así las
        /// dos instancias nombran a la misma entidad con el mismo Id (los contadores
        /// automáticos de cada proceso NO coinciden entre sí).
        /// </summary>
        public string IdentificadorNuevaEntidad { get; set; }

        public DateTime MarcaDeTiempo { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// DTO con el resultado de procesar una AccionJuego, para informarle al emisor
    /// si su acción fue válida y qué pasó (por ejemplo, "Impacto - Unidad destruida").
    /// </summary>
    [Serializable]
    public class ResultadoAccion
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public AccionJuego AccionOriginal { get; set; }
    }
}

