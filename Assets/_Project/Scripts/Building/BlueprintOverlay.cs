using System.Collections.Generic;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Myrmutation.Building
{
    /// <summary>
    /// Plano de arquitecto del modo Construcción (diseno-construccion-beta.md §5). Crea sus propios objetos al arrancar.
    /// Dentro del modo, por encima del terreno y las hormigas: papel azul marino con ruido, rejilla por bloques (más apagada
    /// en la superficie y los bordes), contorno blanco de lo excavado, planos en azul claro (relleno + contorno) y la
    /// vista previa del pincel o del fantasma (contorno; rojo donde no se puede).
    /// Fuera del modo: solo el relleno tenue de los planos y una etiqueta sobre cada sala en obras.
    /// Las formas son marching squares (como NestRenderer) con las piezas *_bp (relleno) y *_line (contorno).
    /// </summary>
    public class BlueprintOverlay : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Planificador. Si se deja vacío, usa Digging.Current")]
        [SerializeField] private DigPlanner planner;

        [Header("Sprites (Art/Tiles/Placeholder/Blueprint)")]
        [Tooltip("Las 15 piezas de relleno ms_XXXX_bp, en cualquier orden")]
        [SerializeField] private Sprite[] fillPieces;
        [Tooltip("Las 15 piezas de contorno ms_XXXX_line, en cualquier orden")]
        [SerializeField] private Sprite[] linePieces;
        [Tooltip("bp_grid_chunk: baldosa de un bloque (importada con Mesh Type Full Rect y Wrap Mode Repeat)")]
        [SerializeField] private Sprite gridSprite;
        [Tooltip("bp_paper_noise: ruido del papel que se repite (Full Rect y Repeat)")]
        [SerializeField] private Sprite noiseSprite;

        [Header("Colores")]
        [SerializeField] private Color paperColor = new Color(0.118f, 0.247f, 0.451f, 0.88f);   // #1E3F73
        [SerializeField] private Color noiseColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("Rejilla donde no se puede pintar (superficie y bordes)")]
        [SerializeField] private Color dimGridColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color existingColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color planColor = new Color(0.561f, 0.827f, 1f, 1f);   // #8FD3FF
        [Tooltip("Alfa del relleno de los planos dentro del modo")]
        [SerializeField] private float planFillAlphaOpen = 0.2f;
        [Tooltip("Alfa del relleno de los planos fuera del modo")]
        [SerializeField] private float planFillAlphaClosed = 0.25f;
        [SerializeField] private Color brushColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color eraseColor = new Color(0.561f, 0.827f, 1f, 0.9f);
        [SerializeField] private Color validColor = new Color(0.561f, 0.827f, 1f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.42f, 0.42f, 1f);   // #FF6B6B

        [Header("Orden de dibujo")]
        [Tooltip("Dentro del modo: el papel; el resto va encima (+1 … +6). Por encima de las hormigas")]
        [SerializeField] private int sortingOrder = 300;
        [Tooltip("Relleno de los planos fuera del modo (por debajo de las hormigas)")]
        [SerializeField] private int closedSortingOrder = 5;
        [SerializeField] private int labelSortingOrder = 150;

        [Header("Etiquetas de obra")]
        [SerializeField] private float labelFontSize = 3f;
        [Tooltip("Segundos (sin escala) entre refrescos de las etiquetas")]
        [SerializeField] private float labelRefreshInterval = 0.5f;

        private enum Layer { Existing, Planned, Preview }

        private Tile[] fillTiles, lineTiles;
        private Tilemap fillMap, existingMap, lineMap, previewMap;
        private TilemapRenderer fillRenderer;
        private GameObject modeRoot;
        private Sprite paperSprite;
        private int w, h;
        private bool open;

        // Vista previa: casillas marcadas y cuáles van en rojo.
        private bool[] preview, previewBad;
        private readonly List<Vector2Int> previewCells = new List<Vector2Int>(256);
        private readonly List<Vector2Int> nextCells = new List<Vector2Int>(256);
        private Color previewColor;

        private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        private float labelTimer;

        private void Start()
        {
            if (nest == null) nest = NestGrid.Instance;
            if (planner == null) planner = Digging.Current as DigPlanner;
            w = nest.Width;
            h = nest.Height;
            preview = new bool[w * h];
            previewBad = new bool[w * h];

            fillTiles = NestRenderer.BuildTiles(fillPieces, this);
            lineTiles = NestRenderer.BuildTiles(linePieces, this);
            foreach (var t in lineTiles) if (t != null) t.flags = TileFlags.None;   // SetColor por pieza en la vista previa

            float cs = nest.CellSize;
            Vector3 dualOrigin = nest.transform.position - new Vector3(cs, cs, 0f) * 0.5f;   // rejilla dual, como NestRenderer

            // Fuera del modo solo queda el relleno de los planos.
            fillMap = NewTilemap(NewGrid("Planos", transform, dualOrigin, cs), "Relleno", closedSortingOrder, out fillRenderer);

            modeRoot = new GameObject("Modo Construcción");
            modeRoot.transform.SetParent(transform, false);
            BuildPaper();
            Transform dual = NewGrid("Plano (dual)", modeRoot.transform, dualOrigin, cs);
            existingMap = NewTilemap(dual, "Excavado", sortingOrder + 3, out _);
            lineMap = NewTilemap(dual, "Contorno de planos", sortingOrder + 5, out _);
            previewMap = NewTilemap(dual, "Vista previa", sortingOrder + 6, out _);
            existingMap.color = existingColor;
            lineMap.color = planColor;

            for (int y = 0; y <= h; y++)
                for (int x = 0; x <= w; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    existingMap.SetTile(p, lineTiles[Mask(Layer.Existing, x, y)]);
                    int plan = Mask(Layer.Planned, x, y);
                    fillMap.SetTile(p, fillTiles[plan]);
                    lineMap.SetTile(p, lineTiles[plan]);
                }

            nest.CellChanged += OnCellChanged;
            planner.PlannedChanged += OnPlannedChanged;
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (nest != null) nest.CellChanged -= OnCellChanged;
            if (planner != null) planner.PlannedChanged -= OnPlannedChanged;
            if (fillTiles != null) foreach (var t in fillTiles) if (t != null) Destroy(t);
            if (lineTiles != null) foreach (var t in lineTiles) if (t != null) Destroy(t);
            if (paperSprite != null) Destroy(paperSprite);
        }

        private void Update()
        {
            if (open) return;
            labelTimer -= Time.unscaledDeltaTime;
            if (labelTimer > 0f) return;
            labelTimer = labelRefreshInterval;
            RefreshLabels();
        }

        // ================= API para BuildModeController =================

        public void SetOpen(bool value)
        {
            open = value;
            modeRoot.SetActive(value);
            Color c = planColor;
            c.a = value ? planFillAlphaOpen : planFillAlphaClosed;
            fillMap.color = c;
            fillRenderer.sortingOrder = value ? sortingOrder + 4 : closedSortingOrder;
            if (!value) HidePreview();
            for (int i = 0; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
            labelTimer = 0f;
        }

        /// <summary>Círculo del pincel (el mismo que estampa DigPlanner). El de túnel marca en rojo lo que no se puede pintar.</summary>
        public void ShowBrush(Vector3 center, float radiusCells, bool erase)
        {
            nextCells.Clear();
            Vector2Int c0 = nest.WorldToCell(center);
            int r = Mathf.CeilToInt(radiusCells);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = new Vector2Int(c0.x + dx, c0.y + dy);
                    if (!InGrid(c) || ((Vector2)(nest.CellToWorld(c) - center) / nest.CellSize).sqrMagnitude > radiusCells * radiusCells) continue;
                    CellType t = nest.Get(c);
                    if (t == CellType.Dug || t == CellType.Room) continue;   // lo excavado no es prohibido: solo no se pinta
                    nextCells.Add(c);
                }
            ShowCells(erase ? eraseColor : brushColor, !erase);
        }

        /// <summary>Fantasma de sala: el contorno de su huella, azul claro o rojo.</summary>
        public void ShowGhost(RectInt rect, bool valid)
        {
            nextCells.Clear();
            foreach (var c in rect.allPositionsWithin)
                if (InGrid(c)) nextCells.Add(c);
            ShowCells(valid ? validColor : invalidColor, false);
        }

        public void HidePreview()
        {
            nextCells.Clear();
            ShowCells(previewColor, false);
        }

        // ================= DIBUJO =================

        // Papel, ruido y rejilla: SpriteRenderers (Tiled) del tamaño del mapa, ajustados a bloques desde la casilla (0, 0).
        private void BuildPaper()
        {
            float cs = nest.CellSize, block = cs * NestGrid.ChunkSize;
            int blocksX = Mathf.CeilToInt(w / (float)NestGrid.ChunkSize);
            int blocksY = Mathf.CeilToInt(h / (float)NestGrid.ChunkSize);
            Vector3 origin = nest.transform.position;

            paperSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            Bounds b = nest.WorldBounds;
            var paper = NewSprite("Papel", paperSprite, paperColor, sortingOrder);
            paper.transform.position = b.center;
            paper.transform.localScale = new Vector3(b.size.x, b.size.y, 1f);

            if (noiseSprite != null)
                Tiled(NewSprite("Ruido", noiseSprite, noiseColor, sortingOrder + 1), origin, new Vector2(b.size.x, b.size.y));
            if (gridSprite == null) return;

            // Apagada en todo el mapa; normal en el terreno, sin la fila de bloques del borde ni la superficie.
            Tiled(NewSprite("Rejilla apagada", gridSprite, dimGridColor, sortingOrder + 2), origin, new Vector2(blocksX, blocksY) * block);
            int terrainBlocks = nest.TerrainRows / NestGrid.ChunkSize;
            if (blocksX > 2 && terrainBlocks > 1)
                Tiled(NewSprite("Rejilla", gridSprite, gridColor, sortingOrder + 2), origin + new Vector3(block, block, 0f),
                      new Vector2(blocksX - 2, terrainBlocks - 1) * block);
        }

        private SpriteRenderer NewSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(modeRoot.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // Repite el sprite en el rectángulo de esquina inferior izquierda 'min' y tamaño 'size' (en unidades).
        private static void Tiled(SpriteRenderer sr, Vector3 min, Vector2 size)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            Vector2 pivot = sr.sprite.pivot / sr.sprite.rect.size;
            sr.transform.position = min + (Vector3)(size * pivot);
        }

        private void ShowCells(Color color, bool markForbidden)
        {
            bool same = color == previewColor && nextCells.Count == previewCells.Count;
            for (int i = 0; same && i < nextCells.Count; i++) same = nextCells[i] == previewCells[i];
            if (same) return;
            previewColor = color;

            foreach (var c in previewCells) preview[Index(c)] = false;
            foreach (var c in nextCells)
            {
                preview[Index(c)] = true;
                previewBad[Index(c)] = markForbidden && !planner.CanPaint(c);
            }
            foreach (var c in previewCells) RefreshPreviewAround(c);
            foreach (var c in nextCells) RefreshPreviewAround(c);
            previewCells.Clear();
            previewCells.AddRange(nextCells);
        }

        // Una casilla toca las 4 piezas duales que tienen una esquina en su centro.
        private void RefreshPreviewAround(Vector2Int c)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    var p = new Vector3Int(x, y, 0);
                    Tile tile = lineTiles[Mask(Layer.Preview, x, y)];
                    previewMap.SetTile(p, tile);
                    if (tile == null) continue;
                    bool bad = IsBad(x - 1, y) || IsBad(x, y) || IsBad(x, y - 1) || IsBad(x - 1, y - 1);
                    previewMap.SetColor(p, bad ? invalidColor : previewColor);
                }
        }

        private bool IsBad(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && preview[y * w + x] && previewBad[y * w + x];

        private void OnPlannedChanged(Vector2Int c)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                {
                    var p = new Vector3Int(c.x + dx, c.y + dy, 0);
                    int mask = Mask(Layer.Planned, p.x, p.y);
                    fillMap.SetTile(p, fillTiles[mask]);
                    lineMap.SetTile(p, lineTiles[mask]);
                }
        }

        private void OnCellChanged(Vector2Int c)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                    existingMap.SetTile(new Vector3Int(c.x + dx, c.y + dy, 0), lineTiles[Mask(Layer.Existing, c.x + dx, c.y + dy)]);
        }

        // Pieza dual (x, y): esquinas en las casillas (x-1, y), (x, y), (x, y-1) y (x-1, y-1), como NestRenderer.
        private int Mask(Layer layer, int x, int y) =>
            (Hollow(layer, x - 1, y) ? 8 : 0) | (Hollow(layer, x, y) ? 4 : 0)
            | (Hollow(layer, x, y - 1) ? 2 : 0) | (Hollow(layer, x - 1, y - 1) ? 1 : 0);

        private bool Hollow(Layer layer, int x, int y)
        {
            var c = new Vector2Int(x, y);
            switch (layer)
            {
                case Layer.Existing:
                    CellType t = nest.Get(c);   // la superficie cuenta como cerrada, como en NestRenderer
                    return t == CellType.Dug || t == CellType.Room;
                case Layer.Planned:
                    return planner.IsPlanned(c);
                default:
                    return InGrid(c) && preview[Index(c)];
            }
        }

        // ================= ETIQUETAS =================

        private void RefreshLabels()
        {
            int shown = 0;
            for (int i = 0; i < planner.RoomCount; i++)
            {
                planner.GetRoom(i, out var data, out var rect);
                if (!planner.TryGetRoomState(rect.position, out var state, out float progress) || state == RoomDigState.Done) continue;

                if (shown == labels.Count) labels.Add(NewLabel());
                TextMeshPro label = labels[shown++];
                label.gameObject.SetActive(true);
                label.transform.position = nest.CellToWorld(new Vector2Int(rect.x + rect.width / 2, rect.yMax));
                string text = state == RoomDigState.NoAccess ? "Sin acceso"
                    : ColonyStats.Instance != null && ColonyStats.Instance.FreeWorkers == 0 ? "Sin obreras libres"
                    : $"{data.displayName} · {Mathf.FloorToInt(progress * 100f)} %";
                if (label.text != text) label.text = text;
            }
            for (int i = shown; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
        }

        private TextMeshPro NewLabel()
        {
            var go = new GameObject("Etiqueta de obra");
            go.transform.SetParent(transform, false);
            var label = go.AddComponent<TextMeshPro>();
            label.fontSize = labelFontSize;
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingOrder = labelSortingOrder;
            return label;
        }

        // ================= AUXILIARES =================

        private static Transform NewGrid(string name, Transform parent, Vector3 position, float cellSize)
        {
            var go = new GameObject(name, typeof(Grid));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.GetComponent<Grid>().cellSize = new Vector3(cellSize, cellSize, 0f);
            return go.transform;
        }

        private static Tilemap NewTilemap(Transform parent, string name, int order, out TilemapRenderer renderer)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;
        private int Index(Vector2Int c) => c.y * w + c.x;
    }
}
