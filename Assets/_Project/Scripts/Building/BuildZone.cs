using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Building
{
    public enum ZoneSize { Small, Medium, Large }
    public enum Stratum { Soil, Clay, Rock }

    /// <summary>
    /// Zona prediseñada del mapa donde se puede construir una sala.
    /// A2 las coloca en la escena; A4 añade la lógica de construcción (elegir sala, obreras, progreso).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BuildZone : MonoBehaviour
    {
        public static readonly List<BuildZone> All = new List<BuildZone>();

        [SerializeField] private string zoneId = "Z1";
        [SerializeField] private ZoneSize size = ZoneSize.Medium;
        [SerializeField] private Stratum stratum = Stratum.Soil;

        [Header("Propiedades (afectan a qué conviene construir aquí)")]
        [Tooltip("Profunda y cálida: mejor para cría y hongos")]
        [SerializeField] private bool warm;
        [Tooltip("Cerca de agua: riesgo de inundación (beta)")]
        [SerializeField] private bool humid;

        [Header("Referencias")]
        [Tooltip("Punto de entrada de la zona: lo usa la navegación (A3)")]
        [SerializeField] private Transform entrance;
        [Tooltip("Sprite que se ve mientras la zona está vacía (room_empty)")]
        [SerializeField] private SpriteRenderer emptyVisual;

        public string ZoneId => zoneId;
        public ZoneSize Size => size;
        public Stratum Stratum => stratum;
        public bool Warm => warm;
        public bool Humid => humid;
        public Vector3 EntrancePosition => entrance != null ? entrance.position : transform.position;

        /// <summary>Sala construida (o en construcción) en esta zona. Null = libre.</summary>
        public Room Room { get; private set; }
        public bool IsFree => Room == null;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>A4 llama a esto al empezar a construir una sala aquí.</summary>
        public void Assign(Room room)
        {
            Room = room;
            if (emptyVisual != null) emptyVisual.enabled = room == null;
        }

        /// <summary>Zona libre que contiene este punto del mundo (para el toque de la cámara).</summary>
        public static BuildZone At(Vector2 worldPos)
        {
            var col = Physics2D.OverlapPoint(worldPos);
            return col != null ? col.GetComponent<BuildZone>() : null;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = stratum == Stratum.Soil ? new Color(0.8f, 0.6f, 0.3f)
                         : stratum == Stratum.Clay ? new Color(0.85f, 0.45f, 0.2f)
                         : new Color(0.6f, 0.6f, 0.65f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(EntrancePosition, 0.15f);
        }
    }
}
