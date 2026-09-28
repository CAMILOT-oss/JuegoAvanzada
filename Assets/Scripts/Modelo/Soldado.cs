namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Unidad militar cuerpo a cuerpo.
    /// </summary>
    public class Soldado : Unidad
    {
        public Soldado(string propietario, Coordenada posicion)
            : base("Soldado", propietario, posicion, vidaMaxima: 50, ataque: 8, rangoAtaque: 1, velocidad: 3)
        {
        }
    }
}
