using System;
using System.Collections.Generic;
using UnityEngine;
using ImperiosEnGuerra.Modelo;

namespace ImperiosEnGuerra.Vista
{
    /// <summary>
    /// Genera la grilla visual (todas las CasillaView) a partir del objeto Mapa del Modelo,
    /// una sola vez al iniciar la partida. El mapa en sí no vuelve a cambiar de tamaño,
    /// así que no necesita sincronizarse en cada frame como sí lo hacen las unidades/edificios.
    /// </summary>
    public class MapaView : MonoBehaviour
    {
        [Header("Prefab de casilla (con CasillaView)")]
        [SerializeField] private GameObject prefabCasilla;

        [Header("Sprites de terreno")]
        [SerializeField] private Sprite spriteLibre;
        [SerializeField] private Sprite spriteBosque;
        [SerializeField] private Sprite spriteAgua;
        [SerializeField] private Sprite spriteMontana;
        [SerializeField] private Sprite spriteYacimientoOro;
        [SerializeField] private Sprite spriteYacimientoMadera;
        [SerializeField] private Sprite spriteZonaComida;

        private readonly Dictionary<Coordenada, CasillaView> _casillas = new Dictionary<Coordenada, CasillaView>();

        /// <summary>Instancia una CasillaView por cada Casilla del Modelo.</summary>
        public void GenerarVista(Mapa mapa)
        {
            LimpiarVista();

            for (int f = 0; f < mapa.Filas; f++)
            {
                for (int c = 0; c < mapa.Columnas; c++)
                {
                    var coordenada = new Coordenada(f, c);
                    var casillaModelo = mapa.ObtenerCasilla(coordenada);
                    if (casillaModelo == null) continue;

                    GameObject instancia = Instantiate(prefabCasilla, transform);
                    CasillaView vista = instancia.GetComponent<CasillaView>();
                    if (vista == null)
                        vista = instancia.AddComponent<CasillaView>();

                    vista.Configurar(coordenada, ObtenerSpritePorTerreno(casillaModelo.Terreno));
                    _casillas[coordenada] = vista;
                }
            }
        }

        public CasillaView ObtenerVistaCasilla(Coordenada coordenada) =>
            _casillas.TryGetValue(coordenada, out var vista) ? vista : null;

        private Sprite ObtenerSpritePorTerreno(TipoTerreno tipo)
        {
            return tipo switch
            {
                TipoTerreno.Bosque => spriteBosque,
                TipoTerreno.Agua => spriteAgua,
                TipoTerreno.Montana => spriteMontana,
                TipoTerreno.YacimientoOro => spriteYacimientoOro,
                TipoTerreno.YacimientoMadera => spriteYacimientoMadera,
                TipoTerreno.ZonaComida => spriteZonaComida,
                _ => spriteLibre
            };
        }

        private void LimpiarVista()
        {
            foreach (Transform hijo in transform)
                Destroy(hijo.gameObject);
            _casillas.Clear();
        }
    }
}
