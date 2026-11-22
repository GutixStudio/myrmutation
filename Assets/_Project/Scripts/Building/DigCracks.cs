using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Grieta de excavación en 3 fases (A8, diseno-construccion-beta.md §4) sobre cada bloque 3×3 con un DigJob empezado:
    /// fase 1 con Progress &lt; 0,33, fase 2 &lt; 0,66 y fase 3 el resto; al terminar o cancelarse el trabajo, desaparece.
    /// Cada fase se corta en 3×3 trozos (uno por casilla) y cada trozo solo se ve mientras su casilla sigue siendo Solid,
    /// así no se pinta sobre lo ya vaciado. Encima del terreno y de los planos, debajo de las hormigas; no se dibuja con
    /// el modo Construcción abierto. Un bloque empezado que nadie excava (soltado a medias) se ve más tenue (abandonedAlpha).
    /// Sin sprites asignados, genera un placeholder (240 × 240 px a PPU 320 = un bloque).
    /// Pool de renderers reservado al arrancar; los sprites solo cambian al cambiar de fase o de casilla.
    /// </summary>
    public class DigCracks : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid nest;
        [Tooltip("Planificador. Si se deja vacío, usa Digging.Current")]
        [SerializeField] private DigPlanner planner;
        [Tooltip("Modo Construcción: con el modo abierto no se dibujan las grietas (opcional)")]
        [SerializeField] private BuildModeController buildMode;

        [Header("Sprites (dig_crack_1..3, 240 × 240 = un bloque; vacíos = placeholder)")]
        [SerializeField] private Sprite crack1;
        [SerializeField] private Sprite crack2;
        [SerializeField] private Sprite crack3;

        [Header("Dibujo")]
        [Tooltip("Bloques con grieta a la vez (cada uno usa 9 renderers)")]
        [SerializeField] private int poolSize = 32;
        [Tooltip("Por encima del terreno (−10) y de los planos (5), por debajo de las hormigas (10)")]
        [SerializeField] private int sortingOrder = 8;
        [Tooltip("Color de las líneas del placeholder")]
        [SerializeField] private Color placeholderColor = new Color(0.12f, 0.08f, 0.05f, 0.85f);
        [Tooltip("Alfa de la grieta de un bloque empezado que nadie está excavando (abandonado a medias)")]
        [Range(0f, 1f)] [SerializeField] private float abandonedAlpha = 0.4f;
        [Tooltip("Segundos (sin escala) sin que ninguna obrera llame a Work tras los que el bloque cuenta como abandonado")]
        [SerializeField] private float diggingWindow = 0.5f;

        private const int Size = NestGrid.ChunkSize;
        private const int CellsPerBlock = Size * Size;
        private const int PlaceholderPx = 240;

        private sealed class Slot
        {
            public DigJob Job;
            public int Phase;
            public bool Digging;
            public Vector2Int Origin;   // casilla inferior izquierda del bloque
            public GameObject Root;
            public readonly SpriteRenderer[] Renderers = new SpriteRenderer[CellsPerBlock];
        }

        // pieces[fase - 1][y * 3 + x]
        private readonly Sprite[][] pieces = new Sprite[3][];
        private readonly Texture2D[] generated = new Texture2D[3];
        private Slot[] slots;
        private GameObject poolRoot;
        private bool warnedFull;

        /// <summary>Fase de la grieta de un trabajo: 0 sin grieta (sin empezar, terminado o cancelado), 1, 2 o 3.</summary>
        public static int Phase(DigJob job)
        {
            if (job == null || job.IsDone || job.Progress <= 0f) return 0;
            return job.Progress < 0.33f ? 1 : job.Progress < 0.66f ? 2 : 3;
        }

        private void Start()
        {
            if (nest == null) nest = NestGrid.Instance;
            if (planner == null) planner = Digging.Current as DigPlanner;
            BuildPieces();

            poolRoot = new GameObject("Grietas");
            poolRoot.transform.SetParent(transform, false);
            float cs = nest.CellSize;
            slots = new Slot[poolSize];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = new Slot { Root = new GameObject("Grieta") };
                slot.Root.transform.SetParent(poolRoot.transform, false);
                for (int k = 0; k < CellsPerBlock; k++)
                {
                    var sr = new GameObject($"Trozo {k % Size},{k / Size}").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(slot.Root.transform, false);
                    sr.transform.localPosition = new Vector3(k % Size * cs, k / Size * cs, 0f);
                    sr.sortingOrder = sortingOrder;
                    slot.Renderers[k] = sr;
                }
                slot.Root.SetActive(false);
            }
            nest.CellChanged += OnCellChanged;
        }

        private void OnDestroy()
        {
            if (nest != null) nest.CellChanged -= OnCellChanged;
            ReleaseSprites();
        }

        /// <summary>Destruye los trozos y las texturas del placeholder. Fuera de Play usa DestroyImmediate (lo llaman los tests).</summary>
        public void ReleaseSprites()
        {
            for (int p = 0; p < pieces.Length; p++)
            {
                if (pieces[p] != null) foreach (var s in pieces[p]) Release(s);
                pieces[p] = null;
            }
            for (int i = 0; i < generated.Length; i++)
            {
                Release(generated[i]);
                generated[i] = null;
            }
        }

        private static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void Update()
        {
            bool show = buildMode == null || !buildMode.IsOpen;
            if (poolRoot.activeSelf != show) poolRoot.SetActive(show);
            if (!show) return;

            foreach (var slot in slots)
            {
                if (slot.Job == null) continue;
                int phase = Phase(slot.Job);
                if (phase == 0) { Free(slot); continue; }
                if (phase != slot.Phase) SetPhase(slot, phase);
                // En pausa nadie llama a Work: se deja como estaba para que no se apaguen todas.
                if (Time.timeScale > 0f && IsDigging(slot.Job) != slot.Digging) SetDigging(slot, !slot.Digging);
            }

            for (int i = 0; i < planner.ActiveJobCount; i++)
            {
                DigJob job = planner.GetActiveJob(i);
                int phase = Phase(job);
                if (phase == 0 || SlotOf(job) != null) continue;
                Slot slot = SlotOf(null);
                if (slot == null)
                {
                    if (!warnedFull) Debug.LogWarning($"[DigCracks] Más de {poolSize} bloques excavándose a la vez: sube poolSize", this);
                    warnedFull = true;
                    continue;
                }
                slot.Job = job;
                slot.Origin = job.Chunk * Size;
                slot.Root.transform.position = nest.CellToWorld(slot.Origin);
                slot.Root.SetActive(true);
                SetPhase(slot, phase);
                SetDigging(slot, IsDigging(job));
            }
        }

        /// <summary>Alguna obrera del bloque ha llamado a Work(ant, …) en los últimos diggingWindow segundos (sin escala).</summary>
        private bool IsDigging(DigJob job)
        {
            float since = Time.unscaledTime - diggingWindow;
            return job.LastWorked(job.WorkerAt(0)) >= since || job.LastWorked(job.WorkerAt(1)) >= since;
        }

        private void SetDigging(Slot slot, bool digging)
        {
            slot.Digging = digging;
            var color = new Color(1f, 1f, 1f, digging ? 1f : abandonedAlpha);
            foreach (var sr in slot.Renderers) sr.color = color;
        }

        private void SetPhase(Slot slot, int phase)
        {
            slot.Phase = phase;
            for (int k = 0; k < CellsPerBlock; k++)
            {
                slot.Renderers[k].sprite = pieces[phase - 1][k];
                RefreshPiece(slot, k);
            }
        }

        private void Free(Slot slot)
        {
            slot.Job = null;
            slot.Phase = 0;
            slot.Root.SetActive(false);
        }

        // El trozo se ve mientras su casilla no se haya vaciado.
        private void RefreshPiece(Slot slot, int k) =>
            slot.Renderers[k].enabled = nest.Get(slot.Origin + new Vector2Int(k % Size, k / Size)) == CellType.Solid;

        private void OnCellChanged(Vector2Int c)
        {
            foreach (var slot in slots)
            {
                if (slot.Job == null) continue;
                Vector2Int d = c - slot.Origin;
                if (d.x >= 0 && d.y >= 0 && d.x < Size && d.y < Size) RefreshPiece(slot, d.y * Size + d.x);
            }
        }

        private Slot SlotOf(DigJob job)
        {
            foreach (var slot in slots) if (slot.Job == job) return slot;
            return null;
        }

        // ================= SPRITES =================

        // Cada fase (la de Naroa o el placeholder) en 3×3 trozos de una casilla, sobre la misma textura.
        private void BuildPieces()
        {
            var assigned = new[] { crack1, crack2, crack3 };
            Color32[] pixels = null;
            for (int p = 0; p < 3; p++)
            {
                Texture2D texture;
                Rect rect;
                if (assigned[p] != null)
                {
                    texture = assigned[p].texture;
                    rect = assigned[p].textureRect;
                }
                else
                {
                    pixels ??= new Color32[PlaceholderPx * PlaceholderPx];
                    DrawPlaceholderPhase(pixels, p);   // cada fase añade líneas a la anterior
                    texture = generated[p] = new Texture2D(PlaceholderPx, PlaceholderPx, TextureFormat.RGBA32, false)
                        { name = $"dig_crack_{p + 1} (placeholder)", wrapMode = TextureWrapMode.Clamp };
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    rect = new Rect(0f, 0f, PlaceholderPx, PlaceholderPx);
                }

                float piece = rect.width / Size;
                float ppu = piece / nest.CellSize;   // un trozo = una casilla, sea cual sea la importación
                pieces[p] = new Sprite[CellsPerBlock];
                for (int k = 0; k < CellsPerBlock; k++)
                    pieces[p][k] = Sprite.Create(texture,
                        new Rect(rect.x + k % Size * piece, rect.y + k / Size * piece, piece, rect.height / Size),
                        new Vector2(0.5f, 0.5f), ppu);
            }
        }

        // Grietas del placeholder (en píxeles, desde el centro): las de cada fase alargan y ramifican las anteriores.
        private static readonly (Vector2Int a, Vector2Int b, int width)[][] Segments =
        {
            new[] { (V(120, 120), V(100, 150), 6), (V(100, 150), V(108, 175), 5), (V(120, 120), V(140, 95), 6) },
            new[] { (V(108, 175), V(85, 205), 4), (V(140, 95), V(150, 60), 4), (V(100, 150), V(65, 140), 3), (V(140, 95), V(175, 110), 3) },
            new[] { (V(85, 205), V(70, 232), 3), (V(150, 60), V(165, 20), 3), (V(65, 140), V(25, 125), 2),
                    (V(175, 110), V(215, 120), 2), (V(175, 110), V(195, 150), 2), (V(65, 140), V(50, 165), 2) },
        };

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        private void DrawPlaceholderPhase(Color32[] pixels, int phase)
        {
            Color32 color = placeholderColor;
            foreach (var (a, b, width) in Segments[phase])
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    for (int dy = -width / 2; dy <= width / 2; dy++)
                        for (int dx = -width / 2; dx <= width / 2; dx++)
                        {
                            int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                            if (x >= 0 && y >= 0 && x < PlaceholderPx && y < PlaceholderPx) pixels[y * PlaceholderPx + x] = color;
                        }
                }
            }
        }
    }
}
