namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Edificio de apoyo donde los Aldeanos pueden descargar recursos
    /// cuando están más cerca del Almacén que del Centro Urbano.
    /// </summary>
    public class Almacen : Edificio
    {
        public Almacen(string propietario, Coordenada posicion)
            : base("Almacén", propietario, posicion, vidaMaxima: 150, tiempoConstruccionSegundos: 10)
        {
        }
    }
}
