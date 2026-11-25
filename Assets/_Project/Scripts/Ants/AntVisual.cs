using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>
    /// B9 · Hormiga por capas en pixel art. Sustituye a las formas sueltas (círculos y cuadrados) del prefab.
    ///
    /// Capas, de atrás adelante, todas en el MISMO lienzo y con el pivote abajo al centro (se apilan en la misma
    /// posición): legs · abdomen · thorax · head_&lt;casta&gt; · mandible_&lt;casta&gt; · antenna.
    ///   · Obreras: lienzo de 32 × 20 px. Reina: 56 × 28 px, con sus piezas queen_* (la corona va en su cabeza).
    ///   · Se crean como hijos de "Visual" (dentro de un hijo "Layers"): AntActuator sigue volteando y girando "Visual",
    ///     y AntSpread, AntSelection y AntStatusBubble lo siguen encontrando por su nombre.
    ///
    /// Animación (sin interpolar: todo se mueve a píxeles enteros):
    ///   · Andar: 4 fotogramas de patas según la distancia recorrida; el cuerpo sube 1 px en los fotogramas impares.
    ///   · Parada: fotograma de patas quieto.
    ///   · Trabajar: el cuerpo sube y baja 1 px. Comer: la cabeza baja 1 px a ratos.
    ///   · Excavar: cabeceo; cabeza, mandíbula y antenas bajan y avanzan 1 px a golpes.
    ///   El balanceo suave de AntActuator se desactiva solo cuando la hormiga tiene este componente.
    ///
    /// Castas: cambian la cabeza y la mandíbula (no se escala el cuerpo). Nursery.ApplyCasteVisual llama a SetCaste.
    ///
    /// Taras: cambio de PALETA, no tinte. Las piezas se dibujan con 4 grises de referencia (KeyTones); al aplicarlas
    /// se sustituyen por los 4 colores de la paleta (tabla de colores) y la textura resultante se guarda en caché.
    /// La paleta de una tara sale del color de su gen (GeneData.tint): se cambia el tono y se conserva el sombreado.
    ///
    /// API para genes (C10): SetPart(slot, sprite) cambia una pieza; SetPalette / ClearPalette fijan la paleta.
    /// Además escucha AntMutated y aplica sola las piezas (GeneData.bodyPart) y las taras de sus genes.
    ///
    /// Escala: PixelsPerUnit. Hasta que A13 fije el contrato de escala, 32 px = 1 unidad (la obrera mide ~1 u).
    /// Las piezas se importan a ese mismo valor; si cambia, hay que cambiar los dos.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntVisual : MonoBehaviour
    {
        /// <summary>Píxeles por unidad de las piezas (pendiente de A13: contrato de escala).</summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>Los 4 grises de referencia con los que se dibujan las piezas (oscuro → claro).</summary>
        public static readonly Color32[] KeyTones =
        {
            new Color32(32, 32, 32, 255), new Color32(96, 96, 96, 255),
            new Color32(160, 160, 160, 255), new Color32(224, 224, 224, 255),
        };

        private enum Layer { Legs, Abdomen, Thorax, Head, Mandible, Antenna }
        private const int LayerCount = 6;

        [Serializable]
        public class BodySet
        {
            [Tooltip("Patas: 4 fotogramas al andar (0-3) y 1 parada (4)")]
            public Sprite[] legs = new Sprite[5];
            public Sprite abdomen;
            public Sprite thorax;
            public Sprite antenna;
        }

        [Serializable]
        public class CasteLook
        {
            public CasteType caste;
            public Sprite head;
            public Sprite mandible;
        }

        [Header("Referencias")]
        [Tooltip("Hijo Visual del prefab (lo voltea y gira AntActuator)")]
        [SerializeField] private Transform visual;
        [Tooltip("Material de los sprites (vacío = el de por defecto del proyecto)")]
        [SerializeField] private Material spriteMaterial;

        [Header("Obrera (lienzo 32 × 20)")]
        [SerializeField] private BodySet worker = new BodySet();
        [SerializeField] private CasteLook[] castes = Array.Empty<CasteLook>();
        [Tooltip("Paleta normal de las obreras: los 4 colores que sustituyen a los grises de referencia (oscuro → claro)")]
        [SerializeField] private Color[] workerPalette =
        {
            new Color32(42, 22, 13, 255), new Color32(110, 58, 32, 255),
            new Color32(196, 103, 63, 255), new Color32(232, 154, 106, 255),
        };

        [Header("Reina (lienzo 56 × 28)")]
        [SerializeField] private BodySet queen = new BodySet();
        [SerializeField] private Sprite queenHead;
        [SerializeField] private Sprite queenMandible;
        [SerializeField] private Color[] queenPalette =
        {
            new Color32(40, 14, 14, 255), new Color32(104, 36, 30, 255),
            new Color32(170, 66, 50, 255), new Color32(226, 128, 100, 255),
        };

        [Header("Taras")]
        [Tooltip("Color de la tara si su gen tiene el tinte en blanco")]
        [SerializeField] private Color defaultFlawColor = new Color(0.55f, 0.85f, 0.35f);
        [Tooltip("Saturación mínima de la paleta de una tara (para que se note aunque el color del gen sea pálido)")]
        [Range(0f, 1f)][SerializeField] private float minFlawSaturation = 0.35f;

        [Header("Colocación")]
        [Tooltip("Altura (u) del borde inferior del lienzo respecto al punto de la hormiga: los pies quedan en el suelo del túnel")]
        [SerializeField] private float groundOffset = -0.19f;
        [Tooltip("Mantiene el tamaño en píxeles aunque la raíz de la hormiga esté escalada (la reina de las escenas está a 1,5)")]
        [SerializeField] private bool keepPixelSize = true;

        [Header("Animación")]
        [Tooltip("Distancia (u) que recorre por cada fotograma de patas")]
        [SerializeField] private float strideLength = 0.09f;
        [Tooltip("Cambios por segundo del balanceo al trabajar")]
        [SerializeField] private float workRate = 4f;
        [Tooltip("Cambios por segundo de la cabeza al comer")]
        [SerializeField] private float eatRate = 3f;
        [Tooltip("Golpes por segundo del cabeceo al excavar")]
        [SerializeField] private float digRate = 7f;

        private Ant ant;
        private AntActuator actuator;
        private Transform layersRoot;
        private readonly SpriteRenderer[] renderers = new SpriteRenderer[LayerCount];
        private readonly Sprite[] partOverride = new Sprite[LayerCount];
        private Sprite[] legFrames = new Sprite[5];
        private Color[] paletteOverride;
        private CasteData casteOverride;
        private bool built;

        private Vector3 lastPos;
        private float walked;
        private float animTime;
        private int legFrame = -1;
        private Vector2Int bodyOffset, headOffset;

        private static readonly Dictionary<long, Sprite> recolorCache = new Dictionary<long, Sprite>();
        private static bool warnedNotReadable;

        /// <summary>Lienzo actual en píxeles (32 × 20 la obrera, 56 × 28 la reina).</summary>
        public Vector2Int CanvasSize => IsQueen ? new Vector2Int(56, 28) : new Vector2Int(32, 20);

        private bool IsQueen => ant != null && ant.IsQueen;

        // ================= CICLO =================

        private void Awake()
        {
            ant = GetComponent<Ant>();
            actuator = GetComponent<AntActuator>();
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null) visual = transform;
            BuildLayers();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AntMutated>(OnMutated);
            lastPos = transform.position;
        }

        private void OnDisable() => EventBus.Unsubscribe<AntMutated>(OnMutated);

        private void Start() => Refresh();

        private void LateUpdate()
        {
            if (!built) return;
            if (keepPixelSize) KeepPixelSize();
            Animate();
        }

        // ================= API =================

        /// <summary>Cambia la casta (cabeza y mandíbula). La llama Nursery.ApplyCasteVisual al nacer.</summary>
        public void SetCaste(CasteData caste)
        {
            casteOverride = caste;
            if (built) Refresh();
        }

        /// <summary>
        /// Sustituye la pieza de un hueco del cuerpo por otro sprite del mismo lienzo (null = vuelve a la de su casta).
        /// La usará C10 para las piezas de los genes. Legs con un solo sprite deja las patas sin animar.
        /// </summary>
        public void SetPart(BodySlot slot, Sprite sprite)
        {
            int i = SlotToLayer(slot);
            if (i < 0) return;
            partOverride[i] = sprite;
            if (built) ApplySprites();
        }

        /// <summary>Paleta propia (4 colores, oscuro → claro) en vez de la de su casta y sus taras. null = ClearPalette.</summary>
        public void SetPalette(Color[] palette)
        {
            paletteOverride = palette != null && palette.Length >= KeyTones.Length ? palette : null;
            if (built) ApplySprites();
        }

        public void ClearPalette() => SetPalette(null);

        /// <summary>Vuelve a leer casta y genes y redibuja todas las capas.</summary>
        public void Refresh()
        {
            if (!built) return;
            ApplyGenes();
            ApplySprites();
        }

        // ================= CONSTRUCCIÓN =================

        private void BuildLayers()
        {
            var old = visual.Find("Layers");
            layersRoot = old != null ? old : new GameObject("Layers").transform;
            layersRoot.SetParent(visual, false);
            layersRoot.localPosition = new Vector3(0f, groundOffset, 0f);
            layersRoot.localRotation = Quaternion.identity;
            layersRoot.localScale = Vector3.one;

            for (int i = 0; i < LayerCount; i++)
            {
                string n = ((Layer)i).ToString();
                var t = layersRoot.Find(n);
                var go = t != null ? t.gameObject : new GameObject(n);
                go.transform.SetParent(layersRoot, false);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
                sr.sortingOrder = i;   // dentro del SortingGroup de Visual (AntSpread): el orden entre capas
                renderers[i] = sr;
            }
            built = true;
        }

        private void ApplyGenes()
        {
            for (int i = 0; i < LayerCount; i++) partOverride[i] = null;
            if (ant == null || ant.Genes == null) return;
            foreach (var g in ant.Genes)
            {
                if (g == null || g.bodyPart == null) continue;
                int i = SlotToLayer(g.slot);
                if (i >= 0) partOverride[i] = g.bodyPart;
            }
        }

        private void ApplySprites()
        {
            var set = IsQueen ? queen : worker;
            Sprite head, mandible;
            if (IsQueen) { head = queenHead; mandible = queenMandible; }
            else
            {
                var look = LookFor(casteOverride != null ? casteOverride : ant != null ? ant.Caste : null);
                head = look?.head;
                mandible = look?.mandible;
            }

            var palette = CurrentPalette();
            legFrames = new Sprite[5];
            for (int f = 0; f < 5; f++)
            {
                var s = partOverride[(int)Layer.Legs] != null ? partOverride[(int)Layer.Legs]
                      : set.legs != null && f < set.legs.Length ? set.legs[f] : null;
                legFrames[f] = Recolor(s, palette);
            }
            Set(Layer.Abdomen, set.abdomen, palette);
            Set(Layer.Thorax, set.thorax, palette);
            Set(Layer.Head, head, palette);
            Set(Layer.Mandible, mandible, palette);
            Set(Layer.Antenna, set.antenna, palette);
            legFrame = -1;   // que Animate vuelva a poner las patas
            renderers[(int)Layer.Legs].sprite = legFrames[4] != null ? legFrames[4] : legFrames[0];
        }

        private void Set(Layer layer, Sprite basePart, Color[] palette)
        {
            var s = partOverride[(int)layer] != null ? partOverride[(int)layer] : basePart;
            renderers[(int)layer].sprite = Recolor(s, palette);
        }

        private CasteLook LookFor(CasteData caste)
        {
            if (castes == null || castes.Length == 0) return null;
            if (caste != null) foreach (var c in castes) if (c != null && c.caste == caste.caste) return c;
            foreach (var c in castes) if (c != null && c.caste == CasteType.Media) return c;
            return castes[0];
        }

        // ================= PALETA =================

        private Color[] CurrentPalette()
        {
            if (paletteOverride != null) return paletteOverride;
            var basePalette = IsQueen ? queenPalette : workerPalette;
            GeneData flaw = null;
            if (ant != null && ant.Genes != null)
                foreach (var g in ant.Genes) if (g != null && g.isFlaw) flaw = g;   // la última tara manda
            return flaw == null ? basePalette : FlawPalette(basePalette, flaw.tint != Color.white ? flaw.tint : defaultFlawColor);
        }

        /// <summary>Misma luminosidad que la paleta base (conserva el sombreado), con el tono y la saturación de la tara.</summary>
        private Color[] FlawPalette(Color[] basePalette, Color flawColor)
        {
            Color.RGBToHSV(flawColor, out float h, out float s, out _);
            s = Mathf.Max(s, minFlawSaturation);
            var result = new Color[basePalette.Length];
            for (int i = 0; i < basePalette.Length; i++)
            {
                Color.RGBToHSV(basePalette[i], out _, out float bs, out float bv);
                result[i] = Color.HSVToRGB(h, Mathf.Clamp01(s * (0.6f + 0.6f * bs)), bv);
            }
            return result;
        }

        /// <summary>
        /// Devuelve el sprite con los grises de referencia cambiados por la paleta (los demás colores, como los ojos o
        /// la corona, se quedan igual). Necesita la textura con Read/Write activado; si no, usa el original.
        /// </summary>
        private static Sprite Recolor(Sprite source, Color[] palette)
        {
            if (source == null || palette == null) return source;
            long key = ((long)source.GetInstanceID() << 32) ^ (uint)PaletteHash(palette);
            if (recolorCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = source.texture;
            if (!tex.isReadable)
            {
                if (!warnedNotReadable)
                    Debug.LogWarning($"[AntVisual] La textura {tex.name} no tiene Read/Write activado: no se puede cambiar su paleta.", tex);
                warnedNotReadable = true;
                return source;
            }

            var r = source.textureRect;
            var pixels = tex.GetPixels32();
            int texW = tex.width;
            int w = Mathf.RoundToInt(r.width), hgt = Mathf.RoundToInt(r.height);
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
            var outPixels = new Color32[w * hgt];
            var pal32 = new Color32[KeyTones.Length];
            for (int i = 0; i < pal32.Length; i++) pal32[i] = palette[i];

            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = pixels[(y0 + y) * texW + x0 + x];
                    int k = KeyIndex(c);
                    if (k >= 0) { var p = pal32[k]; p.a = c.a; c = p; }
                    outPixels[y * w + x] = c;
                }

            var copy = new Texture2D(w, hgt, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = tex.name + "_pal",
            };
            copy.SetPixels32(outPixels);
            copy.Apply(false, true);
            var pivot = new Vector2(source.pivot.x / r.width, source.pivot.y / r.height);
            var sprite = Sprite.Create(copy, new Rect(0, 0, w, hgt), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = source.name;
            recolorCache[key] = sprite;
            return sprite;
        }

        private static int KeyIndex(Color32 c)
        {
            if (c.a == 0 || c.r != c.g || c.g != c.b) return -1;
            for (int i = 0; i < KeyTones.Length; i++) if (KeyTones[i].r == c.r) return i;
            return -1;
        }

        private static int PaletteHash(Color[] palette)
        {
            unchecked
            {
                int h = 17;
                foreach (var c in palette) h = h * 31 + ((Color32)c).GetHashCode();
                return h;
            }
        }

        // ================= ANIMACIÓN =================

        private void Animate()
        {
            Vector3 pos = transform.position;
            float moved = (pos - lastPos).magnitude;
            lastPos = pos;
            var action = actuator != null ? actuator.Current : AntAction.Idle;
            animTime += Time.deltaTime;

            int frame = 4;   // patas quietas
            Vector2Int body = Vector2Int.zero, head = Vector2Int.zero;
            switch (action)
            {
                case AntAction.Moving:
                    walked += moved;
                    frame = Mathf.FloorToInt(walked / Mathf.Max(0.001f, strideLength)) % 4;
                    if (frame % 2 == 1) body = Vector2Int.up;
                    break;
                case AntAction.Working:
                    if (Tick(workRate)) body = Vector2Int.up;
                    break;
                case AntAction.Eating:
                    if (Tick(eatRate)) head = Vector2Int.down;
                    break;
                case AntAction.Digging:
                    if (Tick(digRate)) head = new Vector2Int(1, -1);   // cabeceo: abajo y hacia delante
                    break;
                default:
                    walked = 0f;
                    break;
            }

            var legsSprite = partOverride[(int)Layer.Legs] != null ? legFrames[0] : legFrames[frame];
            if (frame != legFrame && legsSprite != null)
            {
                renderers[(int)Layer.Legs].sprite = legsSprite;
                legFrame = frame;
            }
            if (body != bodyOffset || head != headOffset)
            {
                bodyOffset = body;
                headOffset = head;
                Place(Layer.Abdomen, body);
                Place(Layer.Thorax, body);
                Place(Layer.Head, body + head);
                Place(Layer.Mandible, body + head);
                Place(Layer.Antenna, body + head);
            }
        }

        private bool Tick(float rate) => Mathf.FloorToInt(animTime * Mathf.Max(0.01f, rate)) % 2 == 1;

        private void Place(Layer layer, Vector2Int px) =>
            renderers[(int)layer].transform.localPosition = new Vector3(px.x, px.y, 0f) / PixelsPerUnit;

        /// <summary>Compensa la escala de la raíz (no la del Visual, que voltea) para que un píxel mida siempre lo mismo.</summary>
        private void KeepPixelSize()
        {
            float s = Mathf.Abs(transform.lossyScale.x);
            float k = s > 0.0001f ? 1f / s : 1f;
            if (!Mathf.Approximately(layersRoot.localScale.x, k)) layersRoot.localScale = new Vector3(k, k, 1f);
            var p = new Vector3(0f, groundOffset * k, 0f);
            if (layersRoot.localPosition != p) layersRoot.localPosition = p;
        }

        // ================= AUXILIARES =================

        private void OnMutated(AntMutated e)
        {
            if (e.Ant == ant) Refresh();
        }

        private static int SlotToLayer(BodySlot slot) => slot switch
        {
            BodySlot.Head => (int)Layer.Head,
            BodySlot.Antennae => (int)Layer.Antenna,
            BodySlot.Thorax => (int)Layer.Thorax,
            BodySlot.Abdomen => (int)Layer.Abdomen,
            BodySlot.Legs => (int)Layer.Legs,
            _ => -1,
        };
    }
}
