using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Building
{
    public enum CellType : byte { Solid, Dug, Room, Surface, Blocked }

    /// <summary>
    /// Rejilla fina del hormiguero (diseno-construccion-beta.md §2 y §8).
    /// Los datos van en un array plano; (0,0) es la casilla de abajo a la izquierda y
    /// la esquina inferior izquierda del mapa es la posición de este objeto.
    /// Encima del terreno hay unas filas de superficie que no se excavan.
    /// </summary>
    public class NestGrid : MonoBehaviour
    {
        /// <summary>Lado de un bloque de trabajo, en casillas.</summary>
        public const int ChunkSize = 3;

        // §3: anillo de pared alrededor de las salas y alto de las puertas (= ancho de la zona de acceso).
        public const int RoomWallThickness = 2;
        public const int DoorSize = 3;

        public static NestGrid Instance { get; private set; }

        [Header("Medidas")]
        [Tooltip("Columnas del mapa")]
        [SerializeField] private int width = 120;
        [Tooltip("Filas de terreno excavable")]
        [SerializeField] private int terrainRows = 78;
        [Tooltip("Filas de superficie encima del terreno (hierba y cielo, no se excavan)")]
        [SerializeField] private int surfaceRows = 12;
        [Tooltip("Lado de una casilla en unidades de mundo")]
        [SerializeField] private float cellSize = 0.25f;

        [Header("Estratos (filas contadas desde arriba del terreno)")]
        [Tooltip("Filas de tierra")]
        [SerializeField] private int soilRows = 24;
        [Tooltip("Filas de arcilla debajo de la tierra; el resto hasta el fondo es roca")]
        [SerializeField] private int clayRows = 27;

        [Header("Mapa inicial")]
        [Tooltip("Rectángulos excavados al empezar, en casillas: pozo y túneles entre salas")]
        [SerializeField] private RectInt[] startingDug =
        {
            new RectInt(59, 39, 3, 39),   // pozo de entrada
            new RectInt(56, 39, 3, 3),    // túnel Guardería - pozo
            new RectInt(62, 39, 2, 3),    // túnel pozo - Sala Real
            new RectInt(91, 39, 4, 3),    // túnel Sala Real - Despensa
        };
        [Tooltip("Salas iniciales: sus datos y la esquina inferior izquierda de su huella; el tamaño es data.footprint. " +
                 "Pasan a Room aunque también estén en startingDug y RoomSpawner pone su prefab")]
        [SerializeField] private StartingRoom[] startingRooms =
        {
            new StartingRoom { origin = new Vector2Int(35, 39) },   // Guardería (21 × 12)
            new StartingRoom { origin = new Vector2Int(64, 39) },   // Sala Real (27 × 15, techo a 24 de profundidad)
            new StartingRoom { origin = new Vector2Int(95, 39) },   // Despensa (21 × 12)
        };

        /// <summary>Sala del mapa inicial: sus datos y la esquina inferior izquierda de su huella, en casillas.</summary>
        [Serializable]
        public struct StartingRoom
        {
            public RoomData data;
            public Vector2Int origin;
        }

        private CellType[] cells;
        private bool warnedNoData;

        /// <summary>Salas del mapa inicial; las que no tienen RoomData no se colocan.</summary>
        public IReadOnlyList<StartingRoom> StartingRooms => startingRooms;

        public int Width => width;
        public int Height => terrainRows + surfaceRows;
        public float CellSize => cellSize;

        /// <summary>Filas de terreno excavable. La primera fila de superficie es esta (y = TerrainRows).</summary>
        public int TerrainRows => terrainRows;

        /// <summary>Rectángulo del mapa entero (terreno + superficie) en el mundo.</summary>
        public Bounds WorldBounds
        {
            get
            {
                var size = new Vector3(width * cellSize, Height * cellSize, 0f);
                return new Bounds(transform.position + size * 0.5f, size);
            }
        }

        /// <summary>Una casilla ha cambiado de tipo.</summary>
        public event Action<Vector2Int> CellChanged;

        /// <summary>Por dónde se puede pasar (holgura, suelo). Para el dibujo, la superficie cuenta como cerrada.</summary>
        public static bool IsOpen(CellType type) =>
            type == CellType.Dug || type == CellType.Room || type == CellType.Surface;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildStartingMap();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Se escribe el array directamente, sin eventos: el renderer lo pinta entero en su Start.
        private void BuildStartingMap()
        {
            cells = new CellType[width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < width; x++)
                    cells[y * width + x] = y >= terrainRows ? CellType.Surface
                        : x == 0 || x == width - 1 || y == 0 ? CellType.Blocked
                        : CellType.Solid;

            foreach (var r in startingDug)
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                        if (Get(new Vector2Int(x, y)) == CellType.Solid) cells[y * width + x] = CellType.Dug;

            foreach (var s in startingRooms)
            {
                if (s.data == null)
                {
                    if (!warnedNoData) Debug.LogWarning("[NestGrid] Hay salas iniciales sin RoomData: no se colocan. Usa Reset en el componente para el mapa por defecto.", this);
                    warnedNoData = true;
                    continue;
                }
                var r = new RectInt(s.origin, s.data.footprint);
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        CellType t = Get(new Vector2Int(x, y));
                        if (t == CellType.Solid || t == CellType.Dug) cells[y * width + x] = CellType.Room;
                    }
            }
        }

#if UNITY_EDITOR
        // Un inicializador de campo no puede apuntar a assets: el mapa por defecto toma sus RoomData de aquí al añadir el componente o al hacer Reset.
        private void Reset()
        {
            string[] assets = { "Room_Nursery", "Room_Royal", "Room_Storage" };
            for (int i = 0; i < startingRooms.Length && i < assets.Length; i++)
                startingRooms[i].data = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomData>($"Assets/_Project/Data/Rooms/{assets[i]}.asset");
        }
#endif

        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = (world - transform.position) / cellSize;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        /// <summary>Centro de la casilla.</summary>
        public Vector3 CellToWorld(Vector2Int cell) =>
            transform.position + new Vector3((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize, 0f);

        public Vector2Int CellToChunk(Vector2Int cell) =>
            new Vector2Int(Mathf.FloorToInt(cell.x / (float)ChunkSize), Mathf.FloorToInt(cell.y / (float)ChunkSize));

        /// <summary>Fuera del mapa devuelve Blocked.</summary>
        public CellType Get(Vector2Int cell) =>
            InBounds(cell) ? cells[cell.y * width + cell.x] : CellType.Blocked;

        /// <summary>
        /// Cambia una casilla entre Solid, Dug y Room (Solid→Room lo usa A10).
        /// Superficie y bordes no cambian. Avisa con CellChanged y, si se excava tierra, con CellDug.
        /// </summary>
        public void Set(Vector2Int cell, CellType type)
        {
            if (type == CellType.Surface || type == CellType.Blocked) return;
            CellType old = Get(cell);
            if (old == type || old == CellType.Surface || old == CellType.Blocked) return;

            cells[cell.y * width + cell.x] = type;
            CellChanged?.Invoke(cell);
            if (old == CellType.Solid && type == CellType.Dug) EventBus.Publish(new CellDug(cell));
        }

        public Stratum StratumAt(Vector2Int cell)
        {
            int depth = terrainRows - 1 - cell.y;
            if (depth < soilRows) return Stratum.Soil;
            return depth < soilRows + clayRows ? Stratum.Clay : Stratum.Rock;
        }

        /// <summary>La casilla y sus 4 vecinas (arriba, abajo, izquierda y derecha) están abiertas.</summary>
        public bool HasClearance(Vector2Int cell) =>
            IsOpen(Get(cell))
            && IsOpen(Get(cell + Vector2Int.up)) && IsOpen(Get(cell + Vector2Int.down))
            && IsOpen(Get(cell + Vector2Int.left)) && IsOpen(Get(cell + Vector2Int.right));

        /// <summary>
        /// Anillo de pared de las salas (§3): tierra (Solid) a 2 casillas o menos de una casilla Room, que no se puede excavar.
        /// Las zonas de acceso (bloque 3×3 justo fuera de cada puerta) no son pared.
        /// </summary>
        public bool IsRoomWall(Vector2Int cell)
        {
            if (Get(cell) != CellType.Solid || IsAccessZone(cell)) return false;
            for (int dy = -RoomWallThickness; dy <= RoomWallThickness; dy++)
                for (int dx = -RoomWallThickness; dx <= RoomWallThickness; dx++)
                    if (Get(new Vector2Int(cell.x + dx, cell.y + dy)) == CellType.Room) return true;
            return false;
        }

        /// <summary>
        /// Zona de acceso de una sala ya hecha (Room): a 1–3 casillas en horizontal de su primera o última columna,
        /// en sus 3 filas inferiores (las de la puerta).
        /// </summary>
        public bool IsAccessZone(Vector2Int cell)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int d = 1; d <= DoorSize; d++)
                {
                    var door = new Vector2Int(cell.x + side * d, cell.y);
                    if (Get(door) != CellType.Room) continue;
                    if (Get(new Vector2Int(door.x, door.y - DoorSize)) != CellType.Room) return true;
                    break;   // la primera casilla de sala de este lado decide
                }
            return false;
        }

        /// <summary>Suelo bajo un punto: borde de arriba de la primera casilla cerrada por debajo. Dentro de tierra devuelve el punto tal cual.</summary>
        public Vector3 FloorPoint(Vector3 world)
        {
            Vector2Int c = WorldToCell(world);
            if (!IsOpen(Get(c))) return world;
            while (IsOpen(Get(new Vector2Int(c.x, c.y - 1)))) c.y--;
            world.y = transform.position.y + c.y * cellSize;
            return world;
        }

        private bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < Height;

        private void OnDrawGizmosSelected()
        {
            Vector3 o = transform.position;
            int h = terrainRows + surfaceRows;
            float w = width * cellSize, hw = h * cellSize;

            Gizmos.color = new Color(1f, 1f, 1f, 0.08f);
            for (int x = 0; x <= width; x++) Gizmos.DrawLine(o + new Vector3(x * cellSize, 0f), o + new Vector3(x * cellSize, hw));
            for (int y = 0; y <= h; y++) Gizmos.DrawLine(o + new Vector3(0f, y * cellSize), o + new Vector3(w, y * cellSize));

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            for (int x = 0; x <= width; x += ChunkSize) Gizmos.DrawLine(o + new Vector3(x * cellSize, 0f), o + new Vector3(x * cellSize, hw));
            for (int y = 0; y <= h; y += ChunkSize) Gizmos.DrawLine(o + new Vector3(0f, y * cellSize), o + new Vector3(w, y * cellSize));

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o + new Vector3(0f, terrainRows * cellSize), o + new Vector3(w, terrainRows * cellSize));
        }
    }
}
