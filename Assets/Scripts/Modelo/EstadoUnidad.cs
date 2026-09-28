namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Estado en el que se encuentra una unidad. Lo actualiza el Controlador
    /// según el hilo (Task) que esté procesando la acción de la unidad.
    /// </summary>
    public enum EstadoUnidad
    {
        Inactiva,
        Moviendose,
        Recolectando,
        Construyendo,
        Entrenando,
        Atacando
    }
}
