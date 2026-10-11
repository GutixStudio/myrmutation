using System.Collections.Generic;
using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Capa de PERCEPCIÓN de la IA (GDD §11): responde preguntas sobre la hormiga y el mundo, no actúa.
    ///
    /// Preguntas (tarjeta B1):  TieneHambre → IsHungry · EstaCansada → IsTired ·
    ///                          EstaEnObjetivo → IsAtTarget · sala más cercana de un tipo → NearestRoom
    ///
    /// Hambre y energía se leen de un componente IAntNeeds del mismo objeto (AntNeeds), que es
    /// quien fija los umbrales. Si no hay ninguno, la hormiga nunca tiene hambre ni está cansada.
    /// </summary>
    [RequireComponent(typeof(Ant))]
    public class AntSensor : MonoBehaviour
    {
        [Tooltip("Distancia a la que se considera que está en un punto")]
        [SerializeField] private float targetTolerance = 0.15f;

        private Ant ant;
        private AntActuator actuator;
        private IAntNeeds needs;
        private readonly List<Vector3> pathBuffer = new List<Vector3>();

        public Ant Ant => ant;

        private void Awake()
        {
            ant = GetComponent<Ant>();
            actuator = GetComponent<AntActuator>();
            needs = GetComponent<IAntNeeds>();
        }

        // ================= NECESIDADES =================

        /// <summary>Hay un componente de necesidades en la hormiga.</summary>
        public bool HasNeeds => Needs != null;

        /// <summary>0 = saciada · 1 = muerta de hambre (0 si no hay necesidades).</summary>
        public float Hunger01 => Needs != null ? Needs.Hunger01 : 0f;

        /// <summary>1 = descansada · 0 = agotada (1 si no hay necesidades).</summary>
        public float Energy01 => Needs != null ? Needs.Energy01 : 1f;

        /// <summary>TieneHambre.</summary>
        public bool IsHungry => Needs != null && Needs.IsHungry;

        /// <summary>EstaCansada.</summary>
        public bool IsTired => Needs != null && Needs.IsTired;

        /// <summary>Ha descansado lo suficiente para volver al trabajo.</summary>
        public bool IsRested => Needs == null || Needs.IsRested;

        /// <summary>Llegó a energía 0 y aún no se ha recuperado del todo.</summary>
        public bool IsExhausted => Needs != null && Needs.IsExhausted;

        /// <summary>Puede trabajar (false mientras está agotada).</summary>
        public bool CanWork => Needs == null || Needs.CanWork;

        /// <summary>Está descansando ahora mismo.</summary>
        public bool IsResting => Needs != null && Needs.IsResting;

        // ================= POSICIÓN =================

        /// <summary>
        /// EstaEnObjetivo: ha terminado de moverse y está en el último destino que se le pidió.
        /// False si nunca se le ha mandado a ningún sitio.
        /// </summary>
        public bool IsAtTarget =>
            actuator != null && actuator.HasDestination &&
            actuator.Current != AntAction.Moving && IsAt(actuator.Destination);

        /// <summary>Está (casi) en este punto.</summary>
        public bool IsAt(Vector3 point)
        {
            Vector2 d = transform.position - point;
            return d.sqrMagnitude <= targetTolerance * targetTolerance;
        }

        /// <summary>Está en el punto de trabajo de esta sala.</summary>
        public bool IsAtRoom(Room room) => room != null && IsAt(room.WorkPosition);

        /// <summary>
        /// Punto del túnel más cercano (donde descansa si no tiene sala).
        /// Se obtiene pidiendo al navegador un camino de aquí a aquí: su primer punto es el nodo
        /// más cercano. Así solo usa INavigator, como pide A. Sin navegador: su propia posición.
        /// </summary>
        public Vector3 NearestTunnelPoint()
        {
            var nav = Navigation.Current;
            Vector3 here = transform.position;
            if (nav != null && nav.TryGetPath(here, here, pathBuffer) && pathBuffer.Count > 0)
                return pathBuffer[0];
            return here;
        }

        // ================= SALAS =================

        /// <summary>
        /// Sala más cercana de un tipo, medida por el camino real por los túneles
        /// (si no hay navegador, en línea recta). Ignora las salas a las que no se puede llegar.
        /// onlyBuilt = false incluye también salas en obras.
        /// Devuelve null si no hay ninguna.
        /// </summary>
        public Room NearestRoom(RoomType type, bool onlyBuilt = true)
        {
            Room best = null;
            float bestCost = float.MaxValue;
            Vector3 from = transform.position;

            foreach (var room in Room.All)
            {
                if (room == null || room.Data == null || room.Data.type != type) continue;
                if (onlyBuilt && !room.IsBuilt) continue;

                float cost = DistanceTo(from, room.WorkPosition);
                if (cost < bestCost) { bestCost = cost; best = room; }
            }
            return best;
        }

        /// <summary>Longitud del camino hasta un punto (infinito si no se puede llegar).</summary>
        public float DistanceTo(Vector3 from, Vector3 to)
        {
            var nav = Navigation.Current;
            if (nav == null) return Vector2.Distance(from, to);
            if (!nav.TryGetPath(from, to, pathBuffer)) return float.MaxValue;

            float length = 0f;
            Vector3 prev = from;
            foreach (var p in pathBuffer) { length += Vector2.Distance(prev, p); prev = p; }
            return length;
        }

        // Se busca de nuevo por si AntNeeds se añade después de Awake.
        private IAntNeeds Needs
        {
            get
            {
                if (needs == null || (needs is Object o && o == null)) needs = GetComponent<IAntNeeds>();
                return needs;
            }
        }
    }
}
