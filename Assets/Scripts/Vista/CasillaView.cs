using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Componente que va sobre el prefab de una casilla/tile del mapa.
    /// Muestra el terreno correspondiente y puede resaltarse (selección, casilla válida, etc.).
    /// </summary>
    public class CasillaView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject resaltado; // hijo con un sprite semitransparente para hover/selección

        public Coordenada Posicion { get; private set; }

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configurar(Coordenada posicion, Sprite spriteTerreno)
        {
            Posicion = posicion;
            transform.position = ConversorCoordenadas.AMundo(posicion);
            gameObject.name = $"Casilla_{posicion.Fila}_{posicion.Columna}";

            if (spriteRenderer != null && spriteTerreno != null)
                spriteRenderer.sprite = spriteTerreno;
        }

        public void MostrarResaltado(bool activo)
        {
            if (resaltado != null)
                resaltado.SetActive(activo);
        }
    }
}
