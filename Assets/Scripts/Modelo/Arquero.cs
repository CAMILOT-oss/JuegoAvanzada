namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Unidad militar de ataque a distancia.
    /// </summary>
    public class Arquero : Unidad
    {
        public Arquero(string propietario, Coordenada posicion)
            : base("Arquero", propietario, posicion, vidaMaxima: 35, ataque: 6, rangoAtaque: 3, velocidad: 3)
        {
        }
    }
}
