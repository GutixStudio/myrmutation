using UnityEngine;

namespace Myrmutation.Core.Data
{
    [CreateAssetMenu(menuName = "Myrmutation/Room", fileName = "Room_")]
    public class RoomData : ScriptableObject
    {
        public RoomType type;
        public string displayName;
        [TextArea] public string description;
        public Sprite sprite;
        public Sprite icon;

        [Header("Construcción")]
        [Tooltip("Obreras mínimas asignadas para empezar a construir")]
        public int minWorkers = 2;
        [Tooltip("Puntos de trabajo necesarios (se reducen con la suma de fuerza por segundo)")]
        public float workCost = 30f;

        [Header("Uso")]
        public int workerCapacity = 4;
    }
}
