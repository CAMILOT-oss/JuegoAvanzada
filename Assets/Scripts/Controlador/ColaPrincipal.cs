using System;
using System.Collections.Concurrent;

namespace ImperiosEnGuerra.Controlador
{
    /// <summary>
    /// Los Task de recolección/construcción/entrenamiento y el hilo de red reciben o
    /// terminan cosas en segundo plano. El Modelo (Jugador, Mapa) ya es thread-safe
    /// (usa locks), así que técnicamente esos hilos podrían tocarlo directamente.
    ///
    /// Aun así, esta cola centraliza esos resultados y los aplica todos desde
    /// Update() (hilo principal de Unity). Esto da dos ventajas:
    ///   1. Orden determinístico: las acciones se aplican una por una, en el orden
    ///      en que llegaron, evitando que dos cambios "se pisen" de forma rara.
    ///   2. Es el único lugar seguro para tocar la Vista/Unity API si en algún
    ///      momento una acción necesita disparar algo directamente sobre un
    ///      GameObject (los MonoBehaviour NO pueden tocarse desde otro hilo).
    /// </summary>
    public class ColaPrincipal
    {
        private readonly ConcurrentQueue<Action> _pendientes = new ConcurrentQueue<Action>();

        /// <summary>Encola una acción para que se ejecute en el próximo Update() del hilo principal.</summary>
        public void Encolar(Action accion)
        {
            if (accion != null)
                _pendientes.Enqueue(accion);
        }

        /// <summary>Ejecuta todas las acciones pendientes. Llamar una vez por frame desde GameController.Update().</summary>
        public void ProcesarPendientes()
        {
            while (_pendientes.TryDequeue(out Action accion))
            {
                accion();
            }
        }
    }
}
