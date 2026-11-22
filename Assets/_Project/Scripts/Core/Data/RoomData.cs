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
        [Tooltip("Puntos de trabajo necesarios (se reducen con la suma de fuerza por segundo)")]
        public float workCost = 30f;
        [Tooltip("Huella en casillas finas (ancho × alto). Múltiplos de 3: la sala ocupa bloques enteros")]
        public Vector2Int footprint = new Vector2Int(21, 12);
        [Tooltip("Prefab de la sala terminada (Room, WorkPoint, puertas y arte), con el pivote en la esquina inferior izquierda de la huella")]
        public GameObject prefab;

        [Header("Uso")]
        public int workerCapacity = 4;
    }
}
