using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Construcción de la alfa:
    /// tocar una zona libre → menú con las salas → se crea la sala "en obras" → las obreras libres
    /// se asignan como constructoras → el progreso avanza según la suma de su fuerza → sala terminada.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        [Serializable]
        public struct StartingRoom
        {
            public BuildZone zone;
            public RoomData room;
        }

        [Header("Salas")]
        [Tooltip("Salas que aparecen en el menú de construcción (sin la Sala Real)")]
        [SerializeField] private List<RoomData> buildableRooms = new List<RoomData>();
        [Tooltip("Salas ya construidas al empezar (p. ej. Sala Real en Z8)")]
        [SerializeField] private List<StartingRoom> startingRooms = new List<StartingRoom>();
        [SerializeField] private BuildMenuUI menu;

        [Header("Coste y velocidad")]
        [Tooltip("Cada sala construida encarece las siguientes este porcentaje (0.25 = +25 %)")]
        [SerializeField] private float costIncreasePerRoom = 0.25f;
        [SerializeField] private float soilSpeed = 1f;
        [SerializeField] private float claySpeed = 0.75f;
        [SerializeField] private float rockSpeed = 0.5f;

        [Header("Obreras")]
        [Tooltip("Constructoras extra que se reclutan por encima del mínimo, si hay libres")]
        [SerializeField] private int extraBuilders = 2;

        [Header("Visual")]
        [Tooltip("Sprite cuadrado (el mismo del fondo). Se usa si la sala no tiene sprite y para la barra de progreso")]
        [SerializeField] private Sprite squareSprite;
        [SerializeField] private int roomSortingOrder = 3;

        [Header("Pruebas (mientras no haya hormigas)")]
        [Tooltip("Si no hay ninguna hormiga, construir igualmente con esta fuerza simulada")]
        [SerializeField] private bool allowWithoutAnts = true;
        [SerializeField] private float testStrength = 3f;

        private int roomsBuilt;

        public Sprite SquareSprite => squareSprite;
        public bool AllowWithoutAnts => allowWithoutAnts;
        public float TestStrength => testStrength;
        public int RoomSortingOrder => roomSortingOrder;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() => CameraController.Tapped += OnTap;
        private void OnDisable() => CameraController.Tapped -= OnTap;

        private void Start()
        {
            foreach (var s in startingRooms)
            {
                if (s.zone == null || s.room == null) continue;
                CreateRoom(s.zone, s.room).CompleteBuild();
            }
        }

        private void OnTap(Vector2 world)
        {
            if (menu != null && menu.IsOpen) { menu.Close(); return; }   // tocar fuera cierra el menú

            var zone = BuildZone.At(world);
            if (zone == null || !zone.IsFree || menu == null) return;
            menu.Open(zone, buildableRooms, data => StartBuild(zone, data));
        }

        // ---------- API ----------
        public float CostFor(RoomData data) => data.workCost * (1f + costIncreasePerRoom * roomsBuilt);

        public float SpeedFor(Stratum s)
        {
            if (s == Stratum.Clay) return claySpeed;
            if (s == Stratum.Rock) return rockSpeed;
            return soilSpeed;
        }

        /// <summary>Empieza a construir una sala en una zona libre. Devuelve la sala (en obras) o null.</summary>
        public Room StartBuild(BuildZone zone, RoomData data)
        {
            if (zone == null || data == null || !zone.IsFree) return null;
            var room = CreateRoom(zone, data);
            var site = room.gameObject.AddComponent<ConstructionSite>();
            site.Init(this, room, CostFor(data), SpeedFor(zone.Stratum), data.minWorkers, data.minWorkers + extraBuilders);
            roomsBuilt++;
            return room;
        }

        // ---------- Creación de la sala ----------
        private Room CreateRoom(BuildZone zone, RoomData data)
        {
            var go = new GameObject("Room_" + data.type);
            go.transform.position = zone.transform.position;

            var room = go.AddComponent<Room>();
            room.Data = data;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = data.sprite != null ? data.sprite : squareSprite;
            sr.color = data.sprite != null ? Color.white : FallbackColor(data.type);
            sr.sortingOrder = roomSortingOrder;

            var box = zone.GetComponent<BoxCollider2D>();
            if (box != null && sr.sprite != null)
            {
                Vector2 s = sr.sprite.bounds.size;
                go.transform.localScale = new Vector3(box.size.x / Mathf.Max(s.x, 0.0001f), box.size.y / Mathf.Max(s.y, 0.0001f), 1f);
            }

            zone.Assign(room);
            return room;
        }

        private static Color FallbackColor(RoomType t)
        {
            switch (t)
            {
                case RoomType.Royal: return new Color(0.55f, 0.35f, 0.6f);
                case RoomType.Nursery: return new Color(0.85f, 0.75f, 0.55f);
                case RoomType.Storage: return new Color(0.8f, 0.6f, 0.25f);
                case RoomType.FungusGarden: return new Color(0.9f, 0.9f, 0.85f);
                case RoomType.Feast: return new Color(0.6f, 0.2f, 0.25f);
                default: return Color.gray;
            }
        }
    }
}
