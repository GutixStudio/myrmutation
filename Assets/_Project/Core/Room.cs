using System.Collections.Generic;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Core
{
    /// <summary>Sala construida en una zona. La lógica de construcción es de A (A4); la de producción, de D (D1).</summary>
    public class Room : MonoBehaviour
    {
        public static readonly List<Room> All = new List<Room>();

        [SerializeField] private RoomData data;
        [Tooltip("Punto al que van las hormigas para entrar/trabajar en la sala")]
        [SerializeField] private Transform workPoint;

        public RoomData Data { get => data; set => data = value; }
        public Vector3 WorkPosition => workPoint != null ? workPoint.position : transform.position;
        public bool IsBuilt { get; private set; }
        public float BuildProgress { get; set; }   // 0..1

        public readonly List<Ant> Workers = new List<Ant>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void CompleteBuild()
        {
            if (IsBuilt) return;
            IsBuilt = true;
            BuildProgress = 1f;
            EventBus.Publish(new RoomBuilt(this));
        }

        /// <summary>Busca la primera sala construida de un tipo (p. ej. la Despensa más cercana, en el futuro).</summary>
        public static Room FindBuilt(RoomType type)
        {
            foreach (var r in All) if (r.IsBuilt && r.Data != null && r.Data.type == type) return r;
            return null;
        }
    }
}
