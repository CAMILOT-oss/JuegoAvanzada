namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Edificio que permite entrenar unidades militares (Soldado, Arquero).
    /// </summary>
    public class Cuartel : Edificio
    {
        public Cuartel(string propietario, Coordenada posicion)
            : base("Cuartel", propietario, posicion, vidaMaxima: 200, tiempoConstruccionSegundos: 15, ancho: 2, alto: 2)
        {
        }
    }
}

