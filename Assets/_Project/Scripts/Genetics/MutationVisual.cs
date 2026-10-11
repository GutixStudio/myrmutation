using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Myrmutation.Ants;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using TMPro;
using UnityEngine;

namespace Myrmutation.Genetics
{
   
    [DisallowMultipleComponent]
    public class MutationVisual : MonoBehaviour
    {
        public static MutationVisual Instance { get; private set; }

        [Header("Duracion")]
        [SerializeField] private float duration = 2f;
        [SerializeField] private float flashDuration = 0.4f;

        [Header("Tinte")]
        [Tooltip("Cuanto se mezcla el color del gen con el de la hormiga (0 = nada, 1 = color completo)")]
        [Range(0f, 1f)][SerializeField] private float tintStrength = 0.5f;
        [Tooltip("Color si el gen tiene el tinte en blanco (GeneData.tint)")]
        [SerializeField] private Color geneColor = new Color(1f, 0.85f, 0.35f);
        [Tooltip("Color si es una tara y su tinte esta en blanco")]
        [SerializeField] private Color flawColor = new Color(0.55f, 0.85f, 0.35f);
        [SerializeField] private Color flashColor = Color.white;

        [Header("Icono, texto y destello")]
        [Tooltip("Tamano del icono en unidades del mundo")]
        [SerializeField] private float iconSize = 0.45f;
        [Tooltip("Tamano de letra del texto flotante (si sale enorme o diminuto, ajustar aqui)")]
        [SerializeField] private float textSize = 3f;
        [SerializeField] private float riseDistance = 0.6f;
        [SerializeField] private float glowMaxScale = 2.2f;
        [Tooltip("Orden de dibujado de los efectos (que queden por encima de las hormigas)")]
        [SerializeField] private int sortingOrder = 100;

        [Header("Colores del texto")]
        [SerializeField] private Color goodColor = new Color(0.49f, 0.99f, 0.54f);
        [SerializeField] private Color badColor = new Color(1f, 0.48f, 0.42f);

        [Header("Pruebas")]
        [Tooltip("Gen que se usa en el menu ⋮ -> 'Probar con la hormiga seleccionada'")]
        [SerializeField] private GeneData testGene;

        private Sprite glowSprite;
        private readonly HashSet<GameObject> live = new HashSet<GameObject>();


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Hay mas de un MutationVisual en la escena; se ignora este.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            EventBus.Subscribe<AntMutated>(OnAntMutated);
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            EventBus.Unsubscribe<AntMutated>(OnAntMutated);
            StopAllCoroutines();
            foreach (var go in live) if (go != null) Destroy(go);
            live.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnAntMutated(AntMutated e)
        {
            if (e.Ant == null || e.Gene == null) return;
            StartCoroutine(Play(e.Ant, e.Gene));
        }


        private IEnumerator Play(Ant ant, GeneData gene)
        {
            var renderers = ant.GetComponentsInChildren<SpriteRenderer>(true);
            Color tint = PickTint(gene);

            var tinted = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                tinted[i] = Color.Lerp(renderers[i].color, tint, tintStrength);
                renderers[i].color = tinted[i];
            }

            Bounds bounds = GetBounds(renderers, ant.transform.position);

            var glow = Spawn("MutationGlow").AddComponent<SpriteRenderer>();
            glow.sprite = GetGlowSprite();
            glow.sortingOrder = sortingOrder - 1;

            SpriteRenderer icon = null;
            if (gene.icon != null)
            {
                icon = Spawn("MutationIcon").AddComponent<SpriteRenderer>();
                icon.sprite = gene.icon;
                icon.sortingOrder = sortingOrder;
            }

            var text = Spawn("MutationText").AddComponent<TextMeshPro>();
            text.text = BuildText(gene);
            text.fontSize = textSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.sortingOrder = sortingOrder;

            float iconScale = 1f;
            if (icon != null)
            {
                var s = gene.icon.bounds.size;
                iconScale = iconSize / Mathf.Max(0.0001f, Mathf.Max(s.x, s.y));
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float fade = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);   

                if (ant != null) bounds = GetBounds(renderers, ant.transform.position);
                Vector3 center = bounds.center;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y, center.z);

                if (ant != null)
                {
                    float f = t < flashDuration ? Mathf.Sin(t / flashDuration * Mathf.PI) : 0f;
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) renderers[i].color = Color.Lerp(tinted[i], flashColor, f);
                }

                float gk = Mathf.Clamp01(t / (flashDuration * 1.5f));
                glow.transform.position = center;
                glow.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, glowMaxScale, gk);
                glow.color = new Color(tint.r, tint.g, tint.b, (1f - gk) * 0.8f);

                if (icon != null)
                {
                    float pop = Mathf.Clamp01(t / 0.2f);
                    icon.transform.position = top + Vector3.up * (0.3f + riseDistance * 0.5f * k);
                    icon.transform.localScale = Vector3.one * iconScale * Mathf.Lerp(0.5f, 1f, pop);
                    icon.color = new Color(1f, 1f, 1f, fade);
                }

                text.transform.position = top + Vector3.up * (0.75f + riseDistance * k);
                text.alpha = fade;

                yield return null;
            }

            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = tinted[i];

            Despawn(glow.gameObject);
            if (icon != null) Despawn(icon.gameObject);
            Despawn(text.gameObject);
        }


        private string BuildText(GeneData gene)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(gene.displayName) ? gene.name : gene.displayName);
            if (gene.isFlaw) sb.Append(" (tara)");

            bool anyLine = false;
            foreach (var m in gene.modifiers)
            {
                AppendLine(sb, m.stat, m.percent, true, ref anyLine);
                AppendLine(sb, m.stat, m.additive, false, ref anyLine);
            }

            if (!anyLine && !string.IsNullOrEmpty(gene.behaviorTag))
                sb.Append('\n').Append(gene.behaviorTag);

            return sb.ToString();
        }

        private void AppendLine(StringBuilder sb, StatType stat, float value, bool isPercent, ref bool anyLine)
        {
            if (Mathf.Approximately(value, 0f)) return;

            bool lowerIsBetter = stat == StatType.HungerRate || stat == StatType.EnergyRate;
            bool good = (value > 0f) != lowerIsBetter;
            string hex = ColorUtility.ToHtmlStringRGB(good ? goodColor : badColor);

            string number = value.ToString("+0.##;-0.##", CultureInfo.InvariantCulture);
            sb.Append('\n')
              .Append("<color=#").Append(hex).Append('>')
              .Append(number).Append(isPercent ? " % " : " ").Append(StatName(stat))
              .Append("</color>");
            anyLine = true;
        }

        private static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "fuerza";
                case StatType.Speed: return "velocidad";
                case StatType.HungerRate: return "hambre";
                case StatType.EnergyRate: return "cansancio";
                case StatType.CarryCapacity: return "carga";
                case StatType.WorkRate: return "trabajo";
                default: return stat.ToString();
            }
        }


        private Color PickTint(GeneData gene)
        {
            if (gene.tint != Color.white) return gene.tint;
            return gene.isFlaw ? flawColor : geneColor;
        }

        private static Bounds GetBounds(SpriteRenderer[] renderers, Vector3 fallbackCenter)
        {
            bool has = false;
            Bounds b = new Bounds(fallbackCenter, new Vector3(0.5f, 0.5f, 0f));
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                if (!has) { b = sr.bounds; has = true; }
                else b.Encapsulate(sr.bounds);
            }
            return b;
        }

        private GameObject Spawn(string objName)
        {
            var go = new GameObject(objName);
            live.Add(go);
            return go;
        }

        private void Despawn(GameObject go)
        {
            live.Remove(go);
            if (go != null) Destroy(go);
        }

        private Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return glowSprite;
        }


        [ContextMenu("Probar con la hormiga seleccionada")]
        private void TestOnSelected()
        {
            var ant = AntSelection.Current;
            if (ant == null || testGene == null)
            {
                Debug.LogWarning("[MutationVisual] Selecciona una hormiga en Play y arrastra un gen a 'Test Gene'.", this);
                return;
            }
            EventBus.Publish(new AntMutated(ant, testGene));
        }
    }
}