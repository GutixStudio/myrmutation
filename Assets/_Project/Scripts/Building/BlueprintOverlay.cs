using System.Collections.Generic;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Myrmutation.Building
{
    /// <summary>
    /// Plano de arquitecto del modo Construcción (diseno-construccion-beta.md §5). Crea sus propios objetos al arrancar.
    /// Dentro del modo, por encima del terreno y las hormigas, en este orden: papel azul marino opaco con ruido, rejilla
    /// por niveles según el zoom (más tenue donde no se puede pintar), lo excavado (túneles, pozo y salas) relleno de azul
    /// más claro que tapa la rejilla y con contorno blanco, casillas en plano como cuadrados azul claro y la vista previa
    /// del pincel o del fantasma (contorno; rojo donde no se puede).
    /// Fuera del modo: los mismos cuadrados de los planos, más tenues, y una etiqueta sobre cada sala en obras.
    /// La rejilla y los cuadrados los dibuja el shader BlueprintGrid con una máscara de una casilla por píxel; lo
    /// excavado es marching squares (como NestRenderer) con las piezas *_bp (relleno) y *_line (contorno).
    /// </summary>
    public class BlueprintOverlay : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Planificador. Si se deja vacío, usa Digging.Current")]
        [SerializeField] private DigPlanner planner;
        [Tooltip("Cámara del juego, para el nivel de la rejilla según el zoom. Si se deja vacío, usa Camera.main")]
        [SerializeField] private Camera cam;

        [Header("Arte (Art/Tiles/Placeholder/Blueprint y Shaders)")]
        [Tooltip("Las 15 piezas de relleno ms_XXXX_bp, en cualquier orden (relleno de lo excavado)")]
        [SerializeField] private Sprite[] fillPieces;
        [Tooltip("Las 15 piezas de contorno ms_XXXX_line, en cualquier orden")]
        [SerializeField] private Sprite[] linePieces;
        [Tooltip("bp_paper_noise: ruido del papel que se repite (Full Rect y Repeat)")]
        [SerializeField] private Sprite noiseSprite;
        [Tooltip("BlueprintGrid.mat (shader Myrmutation/BlueprintGrid): rejilla y casillas en plano")]
        [SerializeField] private Material gridMaterial;

        [Header("Colores")]
        [Tooltip("Papel. Siempre opaco (se ignora el alfa) para tapar terreno, salas y hormigas")]
        [SerializeField] private Color paperColor = new Color(0.118f, 0.247f, 0.451f, 1f);   // #1E3F73
        [SerializeField] private Color noiseColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("Rejilla donde no se puede pintar (superficie, montículo, filas junto a la hierba y anillos de sala)")]
        [SerializeField] private Color dimGridColor = new Color(1f, 1f, 1f, 0.15f);
        [Tooltip("Relleno opaco de lo excavado (túneles, pozo y salas), más claro que el papel; tapa la rejilla")]
        [SerializeField] private Color existingFillColor = new Color(0.180f, 0.353f, 0.604f, 1f);   // #2E5A9A
        [Tooltip("Contorno de lo excavado")]
        [SerializeField] private Color existingColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color planColor = new Color(0.561f, 0.827f, 1f, 1f);   // #8FD3FF
        [Tooltip("Alfa del relleno de las casillas en plano dentro del modo")]
        [SerializeField] private float planFillAlpha = 0.45f;
        [Tooltip("Alfa del borde de cada casilla en plano y del contorno de las salas en plano, dentro del modo")]
        [SerializeField] private float planLineAlpha = 0.8f;
        [Tooltip("Alfa del relleno de los planos fuera del modo (los bordes bajan en la misma proporción)")]
        [SerializeField] private float closedPlanAlpha = 0.25f;
        [SerializeField] private Color brushColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color eraseColor = new Color(0.561f, 0.827f, 1f, 0.9f);
        [SerializeField] private Color validColor = new Color(0.561f, 0.827f, 1f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.42f, 0.42f, 1f);   // #FF6B6B

        [Header("Rejilla por niveles")]
        [Tooltip("Un nivel (9×9, 3×3 o casilla) empieza a verse cuando su cuadrado mide más de estos píxeles en pantalla")]
        [SerializeField] private float levelFadeStartPx = 40f;
        [Tooltip("… y es opaco del todo a partir de estos píxeles")]
        [SerializeField] private float levelFadeEndPx = 80f;
        [Tooltip("Segundos (sin escala) entre recálculos de las zonas tenues y de las huellas de sala")]
        [SerializeField] private float zoneRefreshInterval = 0.3f;

        [Header("Orden de dibujo")]
        [Tooltip("Dentro del modo: el papel; encima, ruido +1, rejilla +2, relleno excavado +3, contorno excavado +4, planos +5 y vista previa +6. Por encima de las hormigas")]
        [SerializeField] private int sortingOrder = 300;
        [Tooltip("Planos fuera del modo (por debajo de las hormigas)")]
        [SerializeField] private int closedSortingOrder = 5;
        [SerializeField] private int labelSortingOrder = 150;

        [Header("Etiquetas de obra")]
        [SerializeField] private float labelFontSize = 3f;
        [Tooltip("Segundos (sin escala) entre refrescos de las etiquetas")]
        [SerializeField] private float labelRefreshInterval = 0.5f;

        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int LevelAlphaId = Shader.PropertyToID("_LevelAlpha");
        private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        private static readonly int DimColorId = Shader.PropertyToID("_DimColor");
        private static readonly int PlanColorId = Shader.PropertyToID("_PlanColor");
        private static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int LineAlphaId = Shader.PropertyToID("_LineAlpha");

        private enum Layer { Existing, Preview }

        private Tile[] fillTiles, lineTiles;
        private Tilemap existingFillMap, existingMap, previewMap;
        private SpriteRenderer planRenderer;
        private GameObject modeRoot;
        private Sprite paperSprite;
        private Material gridMat, planMat;
        private int w, h;
        private bool open;

        // Máscara del shader, una casilla por píxel. R: rejilla tenue; G: en plano; B: huella de sala en plano.
        private Texture2D mask;
        private Color32[] pixels;
        private bool maskDirty, zonesDirty;
        private float zoneTimer, lastPixelsPerCell = -1f;

        // Vista previa: casillas marcadas y cuáles van en rojo.
        private bool[] preview, previewBad;
        private readonly List<Vector2Int> previewCells = new List<Vector2Int>(256);
        private readonly List<Vector2Int> nextCells = new List<Vector2Int>(256);
        private Color previewColor;

        private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        private float labelTimer;

        /// <summary>Lo que se dibuja con contorno blanco en el plano: todo lo excavado, túneles y salas.</summary>
        public static bool IsExisting(CellType type) => type == CellType.Dug || type == CellType.Room;

        /// <summary>Opacidad de cada nivel de la rejilla (x: 9×9, y: 3×3, z: casilla) según los píxeles de pantalla por casilla.</summary>
        public static Vector4 GridLevelAlphas(float pixelsPerCell, float showPx, float solidPx)
        {
            float Alpha(int cells) => Mathf.Clamp01((pixelsPerCell * cells - showPx) / (solidPx - showPx));
            return new Vector4(Alpha(9), Alpha(3), Alpha(1), 0f);
        }

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
            paperSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            BuildMaterials();

            // Los planos se ven dentro y fuera del modo.
            if (planMat != null)
            {
                planRenderer = NewSprite("Planos", transform, paperSprite, Color.white, closedSortingOrder);
                Cover(planRenderer);
                planRenderer.sharedMaterial = planMat;
            }

            modeRoot = new GameObject("Modo Construcción");
            modeRoot.transform.SetParent(transform, false);
            BuildPaper();
            Transform dual = NewGrid("Plano (dual)", modeRoot.transform, dualOrigin, cs);
            existingFillMap = NewTilemap(dual, "Relleno excavado", sortingOrder + 3);
            existingMap = NewTilemap(dual, "Excavado", sortingOrder + 4);
            previewMap = NewTilemap(dual, "Vista previa", sortingOrder + 6);
            existingFillMap.color = existingFillColor;
            existingMap.color = existingColor;

            for (int y = 0; y <= h; y++)
                for (int x = 0; x <= w; x++)
                    SetExistingTile(x, y);
            for (int i = 0; i < pixels.Length; i++) pixels[i].g = Flag(planner.IsPlanned(new Vector2Int(i % w, i / w)));
            zonesDirty = true;

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
            if (gridMat != null) Destroy(gridMat);
            if (planMat != null) Destroy(planMat);
            if (mask != null) Destroy(mask);
        }

        private void Update()
        {
            zoneTimer -= Time.unscaledDeltaTime;
            if (zonesDirty && zoneTimer <= 0f) RefreshZones();
            if (maskDirty)
            {
                mask.SetPixels32(pixels);
                mask.Apply(false);
                maskDirty = false;
            }
            if (open) { UpdateGridLevels(); return; }

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
            if (planMat != null)
            {
                planMat.SetFloat(FillAlphaId, value ? planFillAlpha : closedPlanAlpha);
                planMat.SetFloat(LineAlphaId, value ? planLineAlpha : planLineAlpha * closedPlanAlpha / planFillAlpha);
                planRenderer.sortingOrder = value ? sortingOrder + 5 : closedSortingOrder;
            }
            if (value)
            {
                zonesDirty = true;   // las zonas tenues solo se recalculan con el modo abierto
                zoneTimer = 0f;
                lastPixelsPerCell = -1f;
            }
            else HidePreview();
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
                    if (IsExisting(nest.Get(c))) continue;   // lo excavado no es prohibido: solo no se pinta
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

        // Dos copias del material: rejilla (_Mode 0) y planos (_Mode 1), con la misma máscara.
        private void BuildMaterials()
        {
            mask = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[w * h];
            if (gridMaterial == null)
            {
                // Sin material no hay rejilla ni cuadrados de los planos; el resto del modo sigue funcionando.
                Debug.LogError("BlueprintOverlay: falta gridMaterial; pasa Myrmutation → Crear modo Construcción en la escena", this);
                return;
            }
            Vector3 origin = nest.transform.position;
            gridMat = new Material(gridMaterial);
            planMat = new Material(gridMaterial);
            foreach (var m in new[] { gridMat, planMat })
            {
                m.SetTexture(MaskId, mask);
                m.SetVector(OriginId, new Vector4(origin.x, origin.y, w, h));
                m.SetFloat(CellSizeId, nest.CellSize);
            }
            gridMat.SetFloat(ModeId, 0f);
            gridMat.SetColor(GridColorId, gridColor);
            gridMat.SetColor(DimColorId, dimGridColor);
            planMat.SetFloat(ModeId, 1f);
            planMat.SetColor(PlanColorId, planColor);
        }

        // Papel, ruido y rejilla, del tamaño del mapa.
        private void BuildPaper()
        {
            Color paper = paperColor;
            paper.a = 1f;   // opaco aunque la escena guarde otro alfa: no deben verse los estratos
            Cover(NewSprite("Papel", modeRoot.transform, paperSprite, paper, sortingOrder));

            Bounds b = nest.WorldBounds;
            if (noiseSprite != null)
                Tiled(NewSprite("Ruido", modeRoot.transform, noiseSprite, noiseColor, sortingOrder + 1), nest.transform.position, b.size);
            if (gridMat == null) return;

            var grid = NewSprite("Rejilla", modeRoot.transform, paperSprite, Color.white, sortingOrder + 2);
            Cover(grid);
            grid.sharedMaterial = gridMat;
        }

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // Estira el sprite blanco de 1 × 1 unidad al rectángulo del mapa.
        private void Cover(SpriteRenderer sr)
        {
            Bounds b = nest.WorldBounds;
            sr.transform.position = b.center;
            sr.transform.localScale = new Vector3(b.size.x, b.size.y, 1f);
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

        // Fundido de cada nivel según el tamaño en pantalla de una casilla; solo al cambiar el zoom o el alto de pantalla.
        private void UpdateGridLevels()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || gridMat == null) return;
            float pixelsPerCell = Screen.height / (2f * cam.orthographicSize) * nest.CellSize;
            if (pixelsPerCell == lastPixelsPerCell) return;
            lastPixelsPerCell = pixelsPerCell;
            gridMat.SetVector(LevelAlphaId, GridLevelAlphas(pixelsPerCell, levelFadeStartPx, levelFadeEndPx));
        }

        // Huellas de las salas en plano y, con el modo abierto, zonas donde no se puede pintar (dependen de las vecinas).
        private void RefreshZones()
        {
            zonesDirty = false;
            zoneTimer = zoneRefreshInterval;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].b = 0;
                if (!open) continue;
                var c = new Vector2Int(i % w, i / w);
                pixels[i].r = Flag(!IsExisting(nest.Get(c)) && !planner.CanPaint(c));
            }
            for (int i = 0; i < planner.RoomCount; i++)
            {
                planner.GetRoom(i, out _, out var rect);
                if (!planner.TryGetRoomState(rect.position, out var state, out _) || state == RoomDigState.Done) continue;
                foreach (var c in rect.allPositionsWithin)
                    if (InGrid(c)) pixels[Index(c)].b = 255;
            }
            maskDirty = true;
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

        // El cuadrado se ve al momento; las huellas y zonas tenues (planificar o cancelar una sala) en el siguiente recálculo.
        private void OnPlannedChanged(Vector2Int c)
        {
            if (!InGrid(c)) return;
            pixels[Index(c)].g = Flag(planner.IsPlanned(c));
            maskDirty = true;
            zonesDirty = true;
        }

        private void OnCellChanged(Vector2Int c)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                    SetExistingTile(c.x + dx, c.y + dy);
            zonesDirty = true;
        }

        private void SetExistingTile(int x, int y)
        {
            var p = new Vector3Int(x, y, 0);
            int mask = Mask(Layer.Existing, x, y);
            existingFillMap.SetTile(p, fillTiles[mask]);
            existingMap.SetTile(p, lineTiles[mask]);
        }

        // Pieza dual (x, y): esquinas en las casillas (x-1, y), (x, y), (x, y-1) y (x-1, y-1), como NestRenderer.
        private int Mask(Layer layer, int x, int y) =>
            (Hollow(layer, x - 1, y) ? 8 : 0) | (Hollow(layer, x, y) ? 4 : 0)
            | (Hollow(layer, x, y - 1) ? 2 : 0) | (Hollow(layer, x - 1, y - 1) ? 1 : 0);

        private bool Hollow(Layer layer, int x, int y)
        {
            var c = new Vector2Int(x, y);
            return layer == Layer.Existing
                ? IsExisting(nest.Get(c))   // la superficie cuenta como cerrada, como en NestRenderer
                : InGrid(c) && preview[Index(c)];
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

        private static Tilemap NewTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        private static byte Flag(bool value) => value ? (byte)255 : (byte)0;
        private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;
        private int Index(Vector2Int c) => c.y * w + c.x;
    }
}
