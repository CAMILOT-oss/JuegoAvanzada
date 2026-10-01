classDiagram
direction TB

    namespace Modelo {
        class Unidad {
            <<abstract>>
            +int Id
            +string Nombre
            +string Propietario
            +int VidaActual
            +int Ataque
            +int RangoAtaque
            +int Velocidad
            +Coordenada Posicion
            +EstadoUnidad Estado
            +bool EstaViva
            +AsignarId(id)
            +RecibirDano(cantidad)
            +PuedeAtacar(objetivo) bool
        }
        class Aldeano {
            +int CargaActual
            +TipoRecurso RecursoAsignado
            +Cargar(cantidad) int
            +Descargar() int
        }
        class Soldado
        class Arquero

        class Edificio {
            <<abstract>>
            +int Id
            +string Propietario
            +Coordenada Posicion
            +int VidaActual
            +int Ancho
            +int Alto
            +bool EstaConstruido
            +bool EstaDestruido
            +AsignarId(id)
            +RecibirDano(cantidad)
        }
        class CentroUrbano
        class Cuartel
        class Almacen

        class Casilla {
            +Coordenada Posicion
            +TipoTerreno Terreno
            +DepositoRecurso Recurso
            +Edificio Edificio
            +Unidad UnidadOcupante
            +bool EsTransitable
            +bool EsTransitablePorUnidad
        }
        class Mapa {
            +int Filas
            +int Columnas
            +object BloqueoMapa
            +ObtenerCasilla(pos) Casilla
            +PuedeColocarEdificio(pos, ancho, alto) bool
            +ColocarEdificio(edificio, pos) bool
            +ColocarUnidad(unidad, pos) bool
            +MoverUnidad(unidad, destino) bool
            +RemoverUnidad(unidad)
            +RemoverEdificio(edificio)
            +ColocarRecurso(deposito, pos, terreno) bool
        }
        class DepositoRecurso {
            +TipoRecurso Tipo
            +Coordenada Posicion
            +int CantidadDisponible
            +bool EstaAgotado
            +Extraer(cantidad) int
        }
        class Jugador {
            +string Nombre
            +Mapa Mapa
            +List~Unidad~ Unidades
            +List~Edificio~ Edificios
            +ObtenerRecurso(tipo) int
            +AgregarRecurso(tipo, cantidad)
            +IntentarConsumirRecursos(costo) bool
            +AgregarUnidad(unidad)
            +EliminarUnidad(unidad)
            +AgregarEdificio(edificio)
            +EliminarEdificio(edificio)
            +ObtenerCentroUrbano() CentroUrbano
            +EstaDerrotado() bool
        }
        class Partida {
            +Jugador Jugador1
            +Jugador Jugador2
            +EstadoPartida Estado
            +string Ganador
            +DateTime FechaInicio
            +DateTime FechaFin
            +IniciarPartida()
            +VerificarGanador() bool
        }
        class Coordenada {
            <<struct>>
            +int Fila
            +int Columna
            +DistanciaA(otra) int
        }
        class AccionJuego {
            +TipoAccion Tipo
            +string JugadorOrigen
            +Coordenada Origen
            +Coordenada Destino
            +string IdentificadorEntidad
            +string IdentificadorAtacante
            +string IdentificadorNuevaEntidad
            +DateTime MarcaDeTiempo
        }
        class ResultadoAccion {
            +bool Exitoso
            +string Mensaje
            +AccionJuego AccionOriginal
        }
        class TipoRecurso {
            <<enumeration>>
            Oro
            Madera
            Comida
        }
        class TipoTerreno {
            <<enumeration>>
            Libre
            Bosque
            Agua
            Montana
            YacimientoOro
            YacimientoMadera
            ZonaComida
        }
        class TipoAccion {
            <<enumeration>>
            Construir
            EntrenarUnidad
            MoverUnidad
            Atacar
            Recolectar
            AtacarEdificio
        }
        class EstadoUnidad {
            <<enumeration>>
            Inactiva
            Moviendose
            Recolectando
            Construyendo
            Entrenando
            Atacando
        }
        class EstadoPartida {
            <<enumeration>>
            EnPreparacion
            EnCurso
            Finalizada
        }
    }

    namespace Controlador {
        class GameController {
            <<MonoBehaviour>>
            +string NombreJugadorLocal
            +Jugador JugadorLocal
            +SolicitarMover(unidad, destino)
            +SolicitarAtacar(atacante, objetivo)
            +SolicitarAtacarEdificio(atacante, objetivo)
            +SolicitarConstruir(tipo, pos)
            +SolicitarEntrenar(tipo, edificio)
            +SolicitarRecolectar(unidad, deposito)
            -OnAccionRecibida(accion)
        }
        class ControladorRed {
            +bool EstaConectado
            +EscucharAsync(puerto)
            +ConectarAsync(ip, puerto)
            +EnviarAccionAsync(accion)
        }
        class ColaPrincipal {
            +Encolar(accion)
            +ProcesarPendientes()
        }
        class ControladorMovimiento {
            +MoverAsync(unidad, destino, mapa, token) bool
        }
        class ControladorCombate {
            +AtacarAsync(atacante, objetivo, mapa, token)
            +AtacarEdificioAsync(atacante, objetivo, mapa, token)
        }
        class ControladorConstruccion {
            +IniciarConstruccion(jugador, fabrica, costo, pos) Edificio
        }
        class ControladorEntrenamiento {
            +IniciarEntrenamiento(jugador, edificio, fabrica, costo) bool
        }
        class ControladorRecoleccion {
            +RecolectarAsync(aldeano, deposito, jugador, puntoEntrega, token)
        }
        class ValidadorAcciones {
            +PuedeMoverse(unidad, destino, mapa) bool
            +PuedeOrdenarAtaque(atacante, objetivo) bool
            +PuedeAtacar(atacante, objetivo) bool
            +PuedeConstruir(jugador, pos, costo, ancho, alto) bool
            +PuedeEntrenar(jugador, edificio, costo) bool
        }
        class GeneradorMapa {
            +Generar(mapa, semilla, zonasSeguras)
        }
        class CostosJuego {
            +CostoAldeano
            +CostoSoldado
            +CostoArquero
            +CostoCuartel
            +CostoAlmacen
            +TamanoEdificio
        }
        class GestorArchivosPartida {
            +string Carpeta
            +RegistroPartida Registro
            +GuardarConfiguracion(partida, parametros) string
            +GuardarResultadoFinal(partida, nombreLocal) string
        }
        class RegistroPartida {
            +bool EstaActivo
            +Registrar(turno, accion, resultado)
            +MarcarDestruccionComoRegistrada(id) bool
        }
        class DetectorEventosPartida {
            +Revisar(partida)
        }
        class ParametrosPartida {
            +string NombreJugadorLocal
            +int SemillaMapa
            +bool EsHost
        }
    }

    namespace Vista {
        class GameViewManager {
            <<MonoBehaviour>>
            +Inicializar(partida, nombreJugadorLocal)
        }
        class MapaView {
            +GenerarVista(mapa)
        }
        class CasillaView {
            +Configurar(pos, sprite)
        }
        class EntidadViewBase {
            <<abstract>>
            #SpriteRenderer spriteRenderer
            #BarraDeVida barraDeVida
            +Destruir()
        }
        class UnidadView {
            +Unidad UnidadModelo
            +Vincular(unidad)
            +SincronizarConModelo()
        }
        class EdificioView {
            +Edificio EdificioModelo
            +Vincular(edificio)
            +SincronizarConModelo()
        }
        class BarraDeVida {
            +SetPorcentaje(actual, maximo)
        }
        class HUDView {
            +ActualizarRecursos(jugador)
            +MostrarMensaje(texto)
        }
        class PanelFinPartidaView {
            +MostrarResultado(ganador, nombreLocal)
        }
        class ConversorCoordenadas {
            +AMundo(coordenada) Vector3
            +AMundoCentro(coordenada, ancho, alto) Vector3
            +ACoordenada(posicionMundo) Coordenada
        }
        class IndicadorSeleccion {
            +MostrarSobre(objetivo)
            +Ocultar()
        }
    }

    namespace UI {
        class SeleccionController {
            <<MonoBehaviour>>
            +UnidadView UnidadSeleccionada
            +EdificioView EdificioSeleccionado
            +ModoInteraccion Modo
            +EntrarModoColocarEdificio(tipo)
        }
        class PanelConstruccionUI
        class PanelEntrenamientoUI
        class ModoInteraccion {
            <<enumeration>>
            Normal
            ColocandoEdificio
        }
    }

    %% ---------- Relaciones Modelo ----------
    Unidad <|-- Aldeano
    Unidad <|-- Soldado
    Unidad <|-- Arquero
    Edificio <|-- CentroUrbano
    Edificio <|-- Cuartel
    Edificio <|-- Almacen
    Jugador "1" *-- "1" Mapa
    Jugador "1" o-- "*" Unidad
    Jugador "1" o-- "*" Edificio
    Mapa "1" *-- "225" Casilla
    Casilla "1" o-- "0..1" DepositoRecurso
    Casilla "1" o-- "0..1" Edificio
    Casilla "1" o-- "0..1" Unidad
    Partida "1" *-- "2" Jugador
    AccionJuego ..> TipoAccion
    AccionJuego ..> Coordenada
    ResultadoAccion ..> AccionJuego

    %% ---------- Relaciones Controlador ----------
    GameController "1" o-- "1" Partida
    GameController --> ControladorRed
    GameController --> ColaPrincipal
    GameController --> GestorArchivosPartida
    GameController --> DetectorEventosPartida
    GameController ..> ControladorMovimiento
    GameController ..> ControladorCombate
    GameController ..> ControladorConstruccion
    GameController ..> ControladorEntrenamiento
    GameController ..> ControladorRecoleccion
    GameController ..> ValidadorAcciones
    GameController ..> GeneradorMapa
    GameController ..> CostosJuego
    GameController --> GameViewManager : Inicializar()
    GestorArchivosPartida "1" *-- "1" RegistroPartida
    GestorArchivosPartida ..> ParametrosPartida
    DetectorEventosPartida --> RegistroPartida
    ControladorCombate ..> ValidadorAcciones
    ControladorMovimiento ..> ValidadorAcciones
    ControladorConstruccion ..> ValidadorAcciones
    ControladorEntrenamiento ..> ValidadorAcciones
    ControladorEntrenamiento ..> CostosJuego
    ControladorRecoleccion ..> ControladorMovimiento
    ControladorRed ..> AccionJuego
    ControladorRed --> ColaPrincipal

    %% ---------- Relaciones Vista ----------
    EntidadViewBase <|-- UnidadView
    EntidadViewBase <|-- EdificioView
    EntidadViewBase --> BarraDeVida
    GameViewManager --> MapaView
    GameViewManager --> HUDView
    GameViewManager --> PanelFinPartidaView
    GameViewManager "1" o-- "*" UnidadView
    GameViewManager "1" o-- "*" EdificioView
    MapaView "1" o-- "*" CasillaView
    UnidadView ..> Unidad
    EdificioView ..> Edificio
    HUDView ..> Jugador
    PanelFinPartidaView ..> Partida
    ConversorCoordenadas ..> Coordenada

    %% ---------- Relaciones UI ----------
    SeleccionController --> GameController
    SeleccionController --> IndicadorSeleccion
    SeleccionController ..> UnidadView
    SeleccionController ..> EdificioView
    PanelConstruccionUI --> SeleccionController
    PanelConstruccionUI --> GameController
    PanelEntrenamientoUI --> SeleccionController
    PanelEntrenamientoUI --> GameController
