namespace ImperiosEnGuerra.UI
{
    /// <summary>
    /// En qué modo está esperando el clic del jugador. Se usa para que un mismo
    /// clic izquierdo signifique "seleccionar" en modo normal, o "colocar edificio
    /// acá" cuando el jugador venía de tocar un botón de construcción.
    /// </summary>
    public enum ModoInteraccion
    {
        Normal,
        ColocandoEdificio
    }
}
