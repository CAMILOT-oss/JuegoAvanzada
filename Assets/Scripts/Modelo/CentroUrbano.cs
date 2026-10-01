namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Edificio principal de cada jugador. Si es destruido, el jugador pierde la partida.
    /// Permite entrenar Aldeanos.
    /// </summary>
    public class CentroUrbano : Edificio
    {
        public CentroUrbano(string propietario, Coordenada posicion)
            : base("Centro Urbano", propietario, posicion, vidaMaxima: 500, tiempoConstruccionSegundos: 0, ancho: 2, alto: 2)
        {
            // El Centro Urbano inicial ya está construido desde el arranque de la partida.
            EstaConstruido = true;
        }
    }
}