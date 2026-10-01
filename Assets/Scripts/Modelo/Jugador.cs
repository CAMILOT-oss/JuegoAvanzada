using System.Collections.Generic;
using System.Linq;

namespace ImperiosEnGuerra.Modelo
{
    /// <summary>
    /// Representa a un jugador: su nombre, su mapa, sus recursos, sus unidades y edificios.
    /// El acceso a los recursos está protegido con lock porque varios Aldeanos
    /// (cada uno en su propio hilo/Task) pueden intentar sumar o gastar recursos a la vez.
    /// </summary>
    public class Jugador
    {
        public string Nombre { get; set; }
        public Mapa Mapa { get; }

        // Listas privadas: se exponen solo de lectura (ver Unidades/Edificios abajo).
        // Toda alta/baja pasa por AgregarUnidad/EliminarUnidad (o su equivalente de Edificio),
        // que toman _bloqueoListas, porque el Controlador puede agregar/quitar entidades
        // desde varios hilos/Task a la vez (dos entrenamientos terminando juntos, etc.).
        private readonly List<Unidad> _unidades = new List<Unidad>();
        private readonly List<Edificio> _edificios = new List<Edificio>();
        private readonly object _bloqueoListas = new object();

        private readonly Dictionary<TipoRecurso, int> _recursos;
        private readonly object _bloqueoRecursos = new object();

        /// <summary>
        /// Copia instantánea (snapshot) de las unidades vivas o no en este momento.
        /// Se devuelve una copia y no la lista interna para que quien la recorra
        /// (por ejemplo GameViewManager en cada frame) nunca choque con un hilo
        /// que esté agregando/quitando unidades al mismo tiempo.
        /// </summary>
        public List<Unidad> Unidades
        {
            get { lock (_bloqueoListas) { return new List<Unidad>(_unidades); } }
        }

        public List<Edificio> Edificios
        {
            get { lock (_bloqueoListas) { return new List<Edificio>(_edificios); } }
        }

        public Jugador(string nombre, int filasMapa = Mapa.TamanoPorDefecto, int columnasMapa = Mapa.TamanoPorDefecto)
        {
            Nombre = nombre;
            Mapa = new Mapa(filasMapa, columnasMapa);
            _recursos = new Dictionary<TipoRecurso, int>
            {
                { TipoRecurso.Oro, 0 },
                { TipoRecurso.Madera, 0 },
                { TipoRecurso.Comida, 0 }
            };
        }

        /// <summary>Lectura segura de la cantidad actual de un recurso.</summary>
        public int ObtenerRecurso(TipoRecurso tipo)
        {
            lock (_bloqueoRecursos)
            {
                return _recursos[tipo];
            }
        }

        /// <summary>Suma recurso de forma segura entre hilos (por ejemplo, al descargar un Aldeano).</summary>
        public void AgregarRecurso(TipoRecurso tipo, int cantidad)
        {
            if (cantidad <= 0) return;
            lock (_bloqueoRecursos)
            {
                _recursos[tipo] += cantidad;
            }
        }

        /// <summary>
        /// Verifica y descuenta atómicamente un costo compuesto por varios recursos.
        /// Devuelve true solo si había recursos suficientes de TODOS los tipos requeridos,
        /// en cuyo caso los descuenta; si no alcanza, no descuenta nada.
        /// </summary>
        public bool IntentarConsumirRecursos(Dictionary<TipoRecurso, int> costo)
        {
            lock (_bloqueoRecursos)
            {
                foreach (var par in costo)
                {
                    if (!_recursos.ContainsKey(par.Key) || _recursos[par.Key] < par.Value)
                        return false;
                }

                foreach (var par in costo)
                    _recursos[par.Key] -= par.Value;

                return true;
            }
        }

        /// <summary>Agrega una unidad de forma segura entre hilos (ej: al terminar un entrenamiento).</summary>
        public void AgregarUnidad(Unidad unidad)
        {
            lock (_bloqueoListas) { _unidades.Add(unidad); }
        }

        /// <summary>Quita una unidad (ej: al morir) de forma segura entre hilos.</summary>
        public void EliminarUnidad(Unidad unidad)
        {
            lock (_bloqueoListas) { _unidades.Remove(unidad); }
        }

        /// <summary>Agrega un edificio de forma segura entre hilos (ej: al terminar una construcción).</summary>
        public void AgregarEdificio(Edificio edificio)
        {
            lock (_bloqueoListas) { _edificios.Add(edificio); }
        }

        /// <summary>Quita un edificio (ej: al ser destruido) de forma segura entre hilos.</summary>
        public void EliminarEdificio(Edificio edificio)
        {
            lock (_bloqueoListas) { _edificios.Remove(edificio); }
        }

        public CentroUrbano ObtenerCentroUrbano() =>
            Edificios.OfType<CentroUrbano>().FirstOrDefault(e => !e.EstaDestruido);

        /// <summary>
        /// El jugador queda derrotado en el momento en que su Centro Urbano es destruido,
        /// sin importar qué unidades le queden vivas (antes también exigía no tener
        /// Soldados/Arqueros vivos; se simplificó a pedido).
        /// </summary>
        public bool EstaDerrotado() => ObtenerCentroUrbano() == null;
    }
}