using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Myrmutation.Building
{
    /// <summary>
    /// Dibuja el hormiguero con marching squares (diseno-construccion-beta.md §2).
    /// Rejilla dual: el Tilemap va desplazado media casilla y cada pieza tiene sus 4 esquinas
    /// en los centros de 4 casillas. La máscara sigue el nombre del sprite (ms_ + sup-izq, sup-der,
    /// inf-der, inf-izq; 1 = excavada). Las sillas (0101 y 1010) ya vienen dibujadas conectadas.
    /// Debajo pone un fondo de color por estrato (placeholder hasta que llegue el arte) y, entre el fondo y el Tilemap,
    /// el montículo de la entrada (§6).
    /// </summary>
    public class NestRenderer : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Tilemap hijo de un Grid; el renderer ajusta el tamaño y la posición del Grid")]
        [SerializeField] private Tilemap tilemap;
        [Tooltip("Las 15 piezas ms_XXXX en cualquier orden: la forma se lee del nombre")]
        [SerializeField] private Sprite[] pieces;

        [Header("Fondo placeholder")]
        [SerializeField] private Color skyColor = new Color(0.55f, 0.75f, 0.9f);
        [SerializeField] private Color soilColor = new Color(0.45f, 0.3f, 0.18f);
        [SerializeField] private Color clayColor = new Color(0.6f, 0.38f, 0.25f);
        [SerializeField] private Color rockColor = new Color(0.35f, 0.33f, 0.32f);
        [Tooltip("Orden de dibujo del fondo; tiene que quedar por debajo del Tilemap")]
        [SerializeField] private int backgroundSortingOrder = -10;

        [Header("Montículo")]
        [Tooltip("Arte del montículo con su cráter (nest_mound, F11): se estira al ancho de sus casillas, con la base sobre el terreno " +
                 "y centrado en el cráter. Vacío: placeholder generado con el color de la tierra")]
        [SerializeField] private Sprite moundSprite;

        private Tile[] tiles;   // índice = máscara; 0 = sin pieza
        private Sprite whiteSprite, generatedMound;
        private Texture2D moundTexture;

        private void Awake() => tiles = BuildTiles(pieces, this);

        /// <summary>Un Tile por pieza ms_XXXX, en el índice de su máscara (también lo usa BlueprintOverlay).</summary>
        public static Tile[] BuildTiles(Sprite[] pieces, UnityEngine.Object context)
        {
            var result = new Tile[16];
            foreach (var sprite in pieces)
            {
                string n = sprite.name;   // "ms_0101", "ms_0101_bp" o "ms_0101_0" si se importó como Multiple
                if (n.Length < 7 || !n.StartsWith("ms_")) { Debug.LogError($"[NestRenderer] Pieza sin nombre ms_XXXX: {n}", context); continue; }
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                result[Convert.ToInt32(n.Substring(3, 4), 2)] = tile;
            }
            return result;
        }

        private void OnEnable() => nest.CellChanged += OnCellChanged;
        private void OnDisable() => nest.CellChanged -= OnCellChanged;

        private void Start()
        {
            float cs = nest.CellSize;
            var grid = tilemap.GetComponentInParent<Grid>();
            grid.cellSize = new Vector3(cs, cs, 0f);
            grid.transform.position = nest.transform.position - new Vector3(cs, cs, 0f) * 0.5f;

            // Pintado completo una sola vez; luego solo se repinta alrededor de cada cambio.
            int w = nest.Width + 1, h = nest.Height + 1;
            var positions = new Vector3Int[w * h];
            var arr = new TileBase[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    positions[y * w + x] = new Vector3Int(x, y, 0);
                    arr[y * w + x] = TileAt(x, y);
                }
            tilemap.SetTiles(positions, arr);

            BuildBackground();
            BuildMound();
        }

        private void OnDestroy()
        {
            foreach (var t in tiles) if (t != null) Destroy(t);
            if (whiteSprite != null) Destroy(whiteSprite);
            if (generatedMound != null) Destroy(generatedMound);
            if (moundTexture != null) Destroy(moundTexture);
        }

        // Una casilla toca las 4 piezas que tienen una esquina en su centro. También se repintan las de sus vecinas en
        // horizontal, porque si son puerta de sala su hueco depende de esta casilla (ver IsHollow).
        private void OnCellChanged(Vector2Int c)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = -1; dx <= 2; dx++)
                    tilemap.SetTile(new Vector3Int(c.x + dx, c.y + dy, 0), TileAt(c.x + dx, c.y + dy));
        }

        // La pieza dual (x, y) tiene sus esquinas en las casillas (x-1, y), (x, y), (x, y-1) y (x-1, y-1).
        private TileBase TileAt(int x, int y)
        {
            int mask = (IsHollow(x - 1, y) || IsMouth(x - 1, y) ? 8 : 0) | (IsHollow(x, y) || IsMouth(x, y) ? 4 : 0)
                     | (IsHollow(x, y - 1) ? 2 : 0) | (IsHollow(x - 1, y - 1) ? 1 : 0);
            return tiles[mask];
        }

        // Boca del cráter: la superficie justo encima de un túnel cuenta como hueco solo en las esquinas de arriba de la pieza que
        // la une con él. Así el túnel queda abierto, cortado en recto media casilla por encima, en vez de cerrado con una tapa.
        private bool IsMouth(int x, int y) =>
            nest.Get(new Vector2Int(x, y)) == CellType.Surface && nest.Get(new Vector2Int(x, y - 1)) == CellType.Dug;

        // Para el dibujo la superficie cuenta como cerrada: si no, se pintaría túnel sobre el cielo. Las salas las dibuja su
        // prefab (§3); de ellas solo es hueco la casilla de puerta con túnel al lado, para que el túnel llegue hasta la sala.
        // Por el anillo de pared, una casilla Room con Dug en horizontal solo puede ser de puerta.
        private bool IsHollow(int x, int y)
        {
            CellType t = nest.Get(new Vector2Int(x, y));
            return t == CellType.Dug
                || (t == CellType.Room && (nest.Get(new Vector2Int(x - 1, y)) == CellType.Dug || nest.Get(new Vector2Int(x + 1, y)) == CellType.Dug));
        }

        // Una franja por cada tramo de filas seguidas con el mismo fondo (cielo, tierra, arcilla, roca).
        private void BuildBackground()
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.zero, 4f);
            float cs = nest.CellSize;
            int start = 0;
            Color current = RowColor(0);
            for (int y = 1; y <= nest.Height; y++)
            {
                Color c = y < nest.Height ? RowColor(y) : Color.clear;
                if (y < nest.Height && c == current) continue;

                var go = new GameObject("Fondo");
                go.transform.SetParent(transform, false);
                go.transform.position = nest.transform.position + new Vector3(0f, start * cs, 0f);
                go.transform.localScale = new Vector3(nest.Width * cs, (y - start) * cs, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = whiteSprite;
                sr.color = current;
                sr.sortingOrder = backgroundSortingOrder;

                start = y;
                current = c;
            }
        }

        // El sprite va con la base sobre el terreno, centrado en el montículo y estirado a su ancho. El generado sigue
        // GroundHeightAt, que sobresale media casilla por cada lado de las casillas del montículo.
        private void BuildMound()
        {
            int minX = -1, maxX = -1, top = 0;
            for (int x = 0; x < nest.Width; x++)
            {
                int h = nest.MoundHeight(x);
                if (h == 0) continue;
                if (minX < 0) minX = x;
                maxX = x;
                top = Mathf.Max(top, h);
            }
            if (minX < 0) return;

            int widthCells = maxX - minX + 1;
            Sprite sprite = moundSprite;
            if (sprite == null)
            {
                widthCells += 2;
                sprite = generatedMound = BuildMoundSprite(minX - 1, widthCells, top);
            }

            float cs = nest.CellSize;
            Bounds b = sprite.bounds;
            float scale = widthCells * cs / b.size.x;
            var go = new GameObject("Montículo");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = nest.transform.position
                + new Vector3((minX + maxX + 1) * 0.5f * cs - b.center.x * scale, nest.TerrainRows * cs - b.min.y * scale, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = backgroundSortingOrder + 1;
        }

        private Sprite BuildMoundSprite(int x0, int widthCells, int heightCells)
        {
            const int pixelsPerCell = 8;
            int w = widthCells * pixelsPerCell, h = heightCells * pixelsPerCell;
            var pixels = new Color32[w * h];   // transparente
            Color32 soil = soilColor;
            for (int i = 0; i < w; i++)
            {
                float ground = (nest.GroundHeightAt(x0 + (i + 0.5f) / pixelsPerCell) - nest.TerrainRows) * pixelsPerCell;
                for (int j = 0; j < h && j + 0.5f < ground; j++) pixels[j * w + i] = soil;
            }
            moundTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            moundTexture.SetPixels32(pixels);
            moundTexture.Apply();
            return Sprite.Create(moundTexture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0f), pixelsPerCell);
        }

        private Color RowColor(int y)
        {
            // La columna 0 es borde (Blocked); la 1 dice si la fila es superficie.
            if (nest.Get(new Vector2Int(1, y)) == CellType.Surface) return skyColor;
            return StratumColor(nest.StratumAt(new Vector2Int(1, y)));
        }

        /// <summary>Color del terreno de un estrato (lo usan también los terrones de DigChunks).</summary>
        public Color StratumColor(Stratum s) => s == Stratum.Soil ? soilColor : s == Stratum.Clay ? clayColor : rockColor;
    }
}
