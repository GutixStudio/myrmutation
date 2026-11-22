using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Terrones al excavar (A8 / F13): cuando una obrera vacía una casilla (DigPlanner.CellExcavated) saltan 2–3 trozos de
    /// tierra desde su centro, con el color de su estrato, que suben, caen con gravedad, giran y se funden en ~0,6 s.
    /// Integración simple sin física de Unity. Pool reservado al arrancar en anillo: si se llena, se reutiliza el más viejo.
    /// Encima de la grieta (DigCracks), debajo de las hormigas. Con el modo Construcción abierto no se lanzan y los que
    /// haya se quitan. Sin sprites asignados, genera un placeholder (3 trozos irregulares de 32 × 32 px).
    /// </summary>
    public class DigChunks : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Planificador. Si se deja vacío, usa Digging.Current")]
        [SerializeField] private DigPlanner planner;
        [Tooltip("Modo Construcción: con el modo abierto no hay terrones (opcional)")]
        [SerializeField] private BuildModeController buildMode;
        [Tooltip("Dibujo del terreno: los terrones toman de aquí el color de cada estrato (opcional)")]
        [SerializeField] private NestRenderer nestRenderer;

        [Header("Sprites (dig_chunk_1..3; vacío = placeholder)")]
        [SerializeField] private Sprite[] chunkSprites;

        [Header("Colores de respaldo (si no hay NestRenderer)")]
        [SerializeField] private Color soilColor = new Color(0.45f, 0.3f, 0.18f);
        [SerializeField] private Color clayColor = new Color(0.6f, 0.38f, 0.25f);
        [SerializeField] private Color rockColor = new Color(0.35f, 0.33f, 0.32f);

        [Header("Lanzamiento (en casillas)")]
        [Tooltip("Terrones por casilla excavada: entre min y max, ambos incluidos")]
        [SerializeField] private Vector2Int countRange = new Vector2Int(2, 3);
        [Tooltip("Velocidad inicial hacia arriba, en casillas/s")]
        [SerializeField] private Vector2 upSpeed = new Vector2(4f, 7f);
        [Tooltip("Velocidad lateral máxima (a izquierda o derecha), en casillas/s")]
        [SerializeField] private float sideSpeed = 3f;
        [Tooltip("Gravedad, en casillas/s²")]
        [SerializeField] private float gravity = 30f;
        [Tooltip("Giro, en grados/s (a un lado o al otro)")]
        [SerializeField] private Vector2 spinSpeed = new Vector2(90f, 270f);
        [Tooltip("Tamaño de cada terrón, en casillas")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.25f, 1f / 3f);
        [Tooltip("Segundos hasta fundirse del todo")]
        [SerializeField] private float lifetime = 0.6f;

        [Header("Pool")]
        [Tooltip("Terrones a la vez; si se llena, se reutiliza el más viejo")]
        [SerializeField] private int poolSize = 64;
        [Tooltip("Por encima de la grieta (8), por debajo de las hormigas (10)")]
        [SerializeField] private int sortingOrder = 9;
        [Tooltip("Semilla de la aleatoriedad; 0 = distinta cada partida")]
        [SerializeField] private int seed;

        private const int PlaceholderPx = 32;

        private struct Chunk
        {
            public bool Alive;
            public Vector2 Velocity;
            public float Spin, Age;
            public Color Color;
        }

        private Chunk[] chunks;
        private SpriteRenderer[] renderers;
        private Sprite[] sprites, generatedSprites;
        private Texture2D generatedTexture;
        private System.Random rng;
        private int next;   // anillo: el siguiente hueco es siempre el más viejo
        private bool wasOpen;

        private void Start()
        {
            if (nest == null) nest = NestGrid.Instance;
            if (planner == null) planner = Digging.Current as DigPlanner;
            rng = seed != 0 ? new System.Random(seed) : new System.Random();
            sprites = chunkSprites != null && chunkSprites.Length > 0 ? chunkSprites : BuildPlaceholder();

            chunks = new Chunk[poolSize];
            renderers = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var sr = new GameObject("Terrón").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sortingOrder = sortingOrder;
                sr.gameObject.SetActive(false);
                renderers[i] = sr;
            }
            if (planner != null) planner.CellExcavated += Burst;
        }

        private void OnDestroy()
        {
            if (planner != null) planner.CellExcavated -= Burst;
            ReleaseSprites();
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>Lanza 2–3 terrones desde el centro de 'cell' con el color de su estrato. Nada con el modo abierto.</summary>
        public void Burst(Vector2Int cell)
        {
            if (chunks == null || IsModeOpen()) return;
            Color color = ColorOf(nest.StratumAt(cell));
            Vector3 center = nest.CellToWorld(cell);
            float cs = nest.CellSize;
            int count = rng.Next(countRange.x, countRange.y + 1);
            for (int n = 0; n < count; n++)
            {
                int i = next;
                next = (next + 1) % chunks.Length;
                chunks[i] = new Chunk
                {
                    Alive = true,
                    Velocity = new Vector2(Range(-sideSpeed, sideSpeed), Range(upSpeed.x, upSpeed.y)) * cs,
                    Spin = Range(spinSpeed.x, spinSpeed.y) * (rng.Next(2) == 0 ? -1f : 1f),
                    Color = color,
                };
                SpriteRenderer sr = renderers[i];
                sr.sprite = sprites[rng.Next(sprites.Length)];
                sr.color = color;
                Transform t = sr.transform;
                t.position = center;
                t.rotation = Quaternion.Euler(0f, 0f, Range(0f, 360f));
                float size = sr.sprite.bounds.size.x;
                t.localScale = Vector3.one * (Range(sizeRange.x, sizeRange.y) * cs / (size > 0f ? size : cs));
                sr.gameObject.SetActive(true);
            }
        }

        /// <summary>Destruye el placeholder generado. Fuera de Play usa DestroyImmediate (lo llaman los tests).</summary>
        public void ReleaseSprites()
        {
            if (generatedSprites != null)
                foreach (var s in generatedSprites) Release(s);
            generatedSprites = null;
            Release(generatedTexture);
            generatedTexture = null;
        }

        private void Advance(float dt)
        {
            bool open = IsModeOpen();
            if (open && !wasOpen) Clear();   // al abrir el modo se quitan los que haya
            wasOpen = open;
            if (open || dt <= 0f) return;

            float g = gravity * nest.CellSize;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (!chunks[i].Alive) continue;
                ref Chunk c = ref chunks[i];
                c.Age += dt;
                if (c.Age >= lifetime) { Kill(i); continue; }
                c.Velocity.y -= g * dt;
                Transform t = renderers[i].transform;
                t.position += (Vector3)(c.Velocity * dt);
                t.Rotate(0f, 0f, c.Spin * dt);
                Color color = c.Color;
                color.a *= 1f - c.Age / lifetime;
                renderers[i].color = color;
            }
        }

        private void Clear()
        {
            for (int i = 0; i < chunks.Length; i++) if (chunks[i].Alive) Kill(i);
        }

        private void Kill(int i)
        {
            chunks[i].Alive = false;
            renderers[i].gameObject.SetActive(false);
        }

        private bool IsModeOpen() => buildMode != null && buildMode.IsOpen;

        private Color ColorOf(Stratum s) =>
            nestRenderer != null ? nestRenderer.StratumColor(s) : s == Stratum.Soil ? soilColor : s == Stratum.Clay ? clayColor : rockColor;

        private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // ================= PLACEHOLDER =================

        // Radio relativo (0–1) de cada trozo cada 45°, para que salgan irregulares y siempre iguales.
        private static readonly float[][] Shapes =
        {
            new[] { 0.95f, 0.7f, 0.85f, 0.6f, 0.9f, 0.75f, 0.65f, 0.8f },
            new[] { 0.7f, 0.9f, 0.6f, 0.8f, 0.55f, 0.95f, 0.75f, 0.6f },
            new[] { 0.85f, 0.6f, 0.95f, 0.7f, 0.8f, 0.55f, 0.9f, 0.65f },
        };

        // Una textura de 96 × 32 con los 3 trozos en blanco (el estrato los tiñe) y un sprite por trozo.
        private Sprite[] BuildPlaceholder()
        {
            int w = PlaceholderPx * Shapes.Length;
            var pixels = new Color32[w * PlaceholderPx];
            float r = PlaceholderPx * 0.5f;
            for (int s = 0; s < Shapes.Length; s++)
                for (int y = 0; y < PlaceholderPx; y++)
                    for (int x = 0; x < PlaceholderPx; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float a = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f / 45f;
                        int k = (int)a;
                        float edge = Mathf.Lerp(Shapes[s][k % 8], Shapes[s][(k + 1) % 8], a - k) * r;
                        if (Mathf.Sqrt(dx * dx + dy * dy) <= edge) pixels[y * w + s * PlaceholderPx + x] = new Color32(255, 255, 255, 255);
                    }

            generatedTexture = new Texture2D(w, PlaceholderPx, TextureFormat.RGBA32, false)
                { name = "dig_chunk (placeholder)", wrapMode = TextureWrapMode.Clamp };
            generatedTexture.SetPixels32(pixels);
            generatedTexture.Apply(false, true);
            generatedSprites = new Sprite[Shapes.Length];
            for (int s = 0; s < Shapes.Length; s++)
                generatedSprites[s] = Sprite.Create(generatedTexture, new Rect(s * PlaceholderPx, 0f, PlaceholderPx, PlaceholderPx),
                    new Vector2(0.5f, 0.5f), PlaceholderPx / nest.CellSize);
            return generatedSprites;
        }
    }
}
