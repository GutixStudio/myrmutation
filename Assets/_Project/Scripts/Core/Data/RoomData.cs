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
        [Tooltip("Aparece en el modo Construcción. Desmarcado en la Sala Real, que ya viene en el mapa inicial")]
        public bool buildable = true;
        [Tooltip("Obreras mínimas asignadas para empezar a construir")]
        public int minWorkers = 2;
        [Tooltip("Puntos de trabajo necesarios (se reducen con la suma de fuerza por segundo)")]
        public float workCost = 30f;
        [Tooltip("Huella en casillas finas (ancho × alto). Múltiplos de 3: la sala ocupa bloques enteros")]
        public Vector2Int footprint = new Vector2Int(21, 12);

        [Header("Uso")]
        public int workerCapacity = 4;
    }
}
