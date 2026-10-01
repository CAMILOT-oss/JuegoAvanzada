# Imperios en Guerra

Videojuego de estrategia en tiempo real (RTS) para dos jugadores, inspirado en Age of Empires, desarrollado en **C# con Unity** .

**Integrante:** Juan Camilo Triana Paipa — 20242020014

## Conceptos aplicados

| Concepto         | Implementación                                                                                                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **MVC**          | Carpetas/espacios de nombres `Modelo`, `Vista` y `Controlador` (más `UI` para la entrada del usuario). El Modelo son clases C# planas sin `UnityEngine`.                         |
| **Concurrencia** | `Task` para movimiento, recolección, construcción, entrenamiento y combate; `lock`, `Interlocked` y `ConcurrentQueue` para sincronizar; hilo escritor dedicado para el registro. |
| **Red**          | Sockets TCP (`System.Net.Sockets`); mensajes JSON, uno por línea.                                                                                                                |
| **Archivos**     | `configuracion.txt`, `log_partida.txt` y `resultado_final.txt`.                                                                                                                  |

## Requisitos

- Unity 6 (6000.6.0f1) o superior
- TextMeshPro (importar "TMP Essential Resources" si Unity lo solicita)

## Cómo abrir el proyecto

1. Clonar el repositorio: `git clone <URL-del-repositorio>`
2. En Unity Hub: **Add → Add project from disk** y elegir la carpeta clonada.
3. Abrir la escena principal (`SampleScene`) y pulsar Play.

## Cómo jugar en red (dos instancias)

Se necesitan dos ejecutables (o un build y el editor). Los argumentos de línea de comandos pisan lo configurado en el Inspector del `GameController`:

```bash
# Instancia 1 (host)
Juego.exe --host --local=Jugador1 --rival=Jugador2 --puerto=7777

# Instancia 2 (cliente)
Juego.exe --cliente --ip=127.0.0.1 --puerto=7777 --local=Jugador2 --rival=Jugador1
```

Para jugar en dos equipos distintos, usar en `--ip=` la IP del host y permitir el puerto en el firewall.

### Controles

- **Clic izquierdo:** seleccionar unidad o edificio propio / colocar edificio.
- **Clic derecho con unidad seleccionada:** mover, atacar (unidad o edificio enemigo) o recolectar (si es un Aldeano sobre un depósito).

## Archivos generados

Se guardan en `<Application.persistentDataPath>/Registros/<NombreJugador>/` (la ruta exacta se imprime en la consola de Unity al iniciar). Se puede cambiar con el argumento `--salida=RUTA`.

| Archivo               | Contenido                                                                |
| --------------------- | ------------------------------------------------------------------------ |
| `configuracion.txt`   | Configuración inicial: mapa, semilla, red, recursos, depósitos y costos. |
| `log_partida.txt`     | Registro de cada evento relevante de la partida.                         |
| `resultado_final.txt` | Ganador, estadísticas y estado final del mapa.                           |

## Estructura del código

```
Assets/Scripts/
├── Modelo/        Estado del juego (POCO, sin UnityEngine)
├── Vista/         Representación gráfica (solo lee el Modelo)
├── Controlador/   Validación, concurrencia, red y archivos
└── UI/            Entrada del usuario (clics y paneles)
```

## Documentación

El informe técnico se encuentra en la carpeta `docs/`.

Diagramas de flujo y de clases (UML): `_[completar ruta, p. ej. docs/diagramas/]_`.

Pruebas de escritorio: `_[completar ruta una vez armadas]_`.
