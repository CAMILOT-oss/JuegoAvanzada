flowchart TD
subgraph INICIO["Inicio de la partida (GameController.Start)"]
A1[Crear Jugador local y Jugador rival] --> A2[ConfigurarInicioDePartida: colocar Centro Urbano de cada uno]
A2 --> A3[GeneradorMapa.Generar: yacimientos con semilla fija]
A3 --> A4[IniciarArchivos: crear carpeta y escribir configuracion.txt]
A4 --> A5[GameViewManager.Inicializar: grilla visual y HUD]
A5 --> A6[Partida.IniciarPartida]
A6 --> A7{actuarComoHost?}
A7 -->|Si| A8[ControladorRed.EscucharAsync: TcpListener espera conexion]
A7 -->|No| A9[ControladorRed.ConectarAsync: TcpClient se conecta al host]
A8 --> A10[Conexion establecida]
A9 --> A10
end

    A10 --> L1

    subgraph LOOP["Bucle principal (GameController.Update, cada frame)"]
        L1[ColaPrincipal.ProcesarPendientes] --> L2[DetectorEventosPartida.Revisar]
        L2 --> L3{Partida.VerificarGanador}
        L3 -->|Continua| L1
        L3 -->|Finalizada| L4[FinalizarArchivos: resultado_final.txt]
        L4 --> L5[PanelFinPartidaView.MostrarResultado]
    end

    subgraph LOCAL["Accion del jugador LOCAL: ejemplo Atacar"]
        B1[Clic derecho sobre unidad enemiga - SeleccionController] --> B2[GameController.SolicitarAtacar]
        B2 --> B3{ValidadorAcciones.PuedeOrdenarAtaque}
        B3 -->|No valido| B4[Registrar rechazo en log_partida.txt]
        B3 -->|Valido| B5[ControladorCombate.AtacarAsync - Task en segundo plano]
        B5 --> B6[Persigue y golpea: Unidad.RecibirDano]
        B6 --> B7{Vida en 0?}
        B7 -->|No, sigue vivo| B6
        B7 -->|Si| B8[Evento UnidadDestruida hacia GameController]
        B8 --> B9[RegistroPartida: Impacto - Unidad destruida]
        B5 --> B10[Registrar orden aceptada]
        B10 --> B11[EnviarAccion: ControladorRed.EnviarAccionAsync]
    end

    B9 -.-> L2

    subgraph RED["Red: sockets TCP, un JSON por linea"]
        C1[NetworkStream.WriteAsync] --> C2((TCP / LAN))
        C2 --> C3[NetworkStream.ReadAsync del otro lado]
        C3 --> C4[JsonSerializer.Deserialize a AccionJuego]
        C4 --> C5[ColaPrincipal.Encolar]
    end

    B11 --> C1

    subgraph REMOTO["Aplicacion en la instancia RIVAL"]
        D1[GameController.Update procesa la cola] --> D2[OnAccionRecibida]
        D2 --> D3[Registrar: orden recibida del rival]
        D3 --> D4{ValidadorAcciones.PuedeOrdenarAtaque}
        D4 -->|Valido: nunca confiar en la red sin revalidar| D5[ControladorCombate.AtacarAsync sobre el Mapa del rival]
        D4 -->|Invalido| D6[Se descarta la accion]
    end

    C5 --> D1
