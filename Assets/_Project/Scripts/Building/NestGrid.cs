using System;
using Myrmutation.Core;
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
        [Tooltip("Rectángulos excavados al empezar, en casillas: pozo, Guardería, Sala Real, Despensa y sus túneles")]
        [SerializeField] private RectInt[] startingDug =
        {
            new RectInt(59, 39, 3, 39),   // pozo de entrada
            new RectInt(35, 39, 21, 12),  // Guardería
            new RectInt(56, 39, 3, 3),    // túnel Guardería - pozo
            new RectInt(64, 39, 27, 15),  // Sala Real (techo a 24 de profundidad)
            new RectInt(62, 39, 2, 3),    // túnel pozo - Sala Real
            new RectInt(95, 39, 21, 12),  // Despensa
            new RectInt(91, 39, 4, 3),    // túnel Sala Real - Despensa
        };

        private CellType[] cells;

        public int Width => width;
        public int Height => terrainRows + surfaceRows;
        public float CellSize => cellSize;

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
        }

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
