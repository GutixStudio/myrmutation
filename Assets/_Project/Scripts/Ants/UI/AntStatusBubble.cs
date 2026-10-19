using System;
using System.Collections.Generic;
using Myrmutation.Core;
using TMPro;
using UnityEngine;

namespace Myrmutation.Ants
{
    /// <summary>Lo que enseña la burbuja. Las alertas van primero y tienen prioridad en ese orden.</summary>
    public enum AntStatus
    {
        None,
        // Alertas: siempre visibles
        Starving, Exhausted, NoPath,
        // Actividad: solo con la cámara cerca o la hormiga seleccionada
        Digging, Working, Eating, Resting, Carrying
    }

    /// <summary>
    /// B7 · Burbuja con icono sobre la cabeza de la hormiga.
    /// Regla de la beta: PROBLEMAS SIEMPRE, INFORMACIÓN SOLO AL ACERCARSE O SELECCIONAR.
    ///
    ///  ALERTAS (siempre visibles, laten suave) · prioridad: hambre crítica > agotada > sin camino
    ///    · Hambre crítica: Hunger01 ≥ criticalHunger (0,85)
    ///    · Agotada:        IAntNeeds.IsExhausted
    ///    · Sin camino:     IAntBrain.HasNoPath
    ///  ACTIVIDAD (solo si orthographicSize ≤ closeZoom o está seleccionada)
    ///    · Excavando / Trabajando / Comiendo / Cargando: según AntActuator.Current
    ///    · Descansando:    IAntNeeds.IsResting
    ///    Andando no enseña nada (si no, todo el hormiguero estaría lleno de burbujas).
    ///
    /// Se refresca al instante con IAntBrain.StateChanged y, además, cada 'pollInterval' segundos
    /// (el hambre y la energía cambian solas, sin evento).
    ///
    /// "Excavando" y "Cargando" se leen por el NOMBRE de la acción del actuador ("Digging", "Carrying"),
    /// así este script compila antes de que existan: en cuanto B6 añada AntAction.Digging, se enciende solo.
    ///
    /// La burbuja es un objeto aparte (no hijo de la hormiga) para que no herede el volteo, el giro
    /// ni la escala del prefab; sigue al hijo "Visual" en LateUpdate y se destruye con la hormiga.
    /// Iconos: placeholder (letra sobre círculo de color) hasta F12; asigna 'sprite' en la tabla para usar arte.
    /// </summary>
    [RequireComponent(typeof(Ant))]
    public class AntStatusBubble : MonoBehaviour
    {
        [Serializable]
        public class StatusIcon
        {
            public AntStatus status;
            [Tooltip("Icono definitivo (F12). Vacío = se usa la letra")]
            public Sprite sprite;
            [Tooltip("Placeholder hasta F12")]
            public string letter;
            [Tooltip("Color del círculo de fondo")]
            public Color color;

            public StatusIcon(AntStatus status, string letter, Color color)
            {
                this.status = status;
                this.letter = letter;
                this.color = color;
            }
        }

        [Header("Cuándo se ve")]
        [Tooltip("Hambre (0-1) a partir de la cual es crítica")]
        [Range(0f, 1f)] [SerializeField] private float criticalHunger = 0.85f;
        [Tooltip("La actividad solo se ve con el zoom (tamaño ortográfico) por debajo de esto. La cámara va de 3 a 12")]
        [SerializeField] private float closeZoom = 5f;
        [Tooltip("Segundos entre comprobaciones de hambre y energía (tiempo real: funciona en pausa)")]
        [SerializeField] private float pollInterval = 0.2f;

        [Header("Tamaño y posición")]
        [Tooltip("Diámetro de la burbuja (unidades del mundo) con el zoom de referencia")]
        [SerializeField] private float diameter = 0.45f;
        [Tooltip("Zoom (tamaño ortográfico) en el que la burbuja mide 'diameter'. Con otro zoom se escala para verse igual en pantalla")]
        [SerializeField] private float referenceZoom = 5f;
        [Tooltip("Altura sobre el centro de la hormiga donde empieza la burbuja (unidades del mundo, no se escala)")]
        [SerializeField] private float headHeight = 0.3f;
        [Tooltip("Orden de dibujado (las hormigas usan 10-60)")]
        [SerializeField] private int sortingOrder = 200;

        [Header("Latido de las alertas")]
        [Tooltip("Cuánto crece al latir (0,15 = +15 %)")]
        [SerializeField] private float pulseAmount = 0.15f;
        [Tooltip("Latidos por segundo")]
        [SerializeField] private float pulseSpeed = 1.2f;

        [Header("Iconos")]
        [Tooltip("Hijo con el dibujo de la hormiga (la burbuja sigue su posición). Vacío = busca 'Visual'")]
        [SerializeField] private Transform visual;
        [SerializeField] private Color letterColor = Color.white;
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private List<StatusIcon> icons = new List<StatusIcon>
        {
            new StatusIcon(AntStatus.Starving,  "H", new Color(0.85f, 0.15f, 0.15f)),
            new StatusIcon(AntStatus.Exhausted, "Z", new Color(0.95f, 0.50f, 0.10f)),
            new StatusIcon(AntStatus.NoPath,    "?", new Color(0.80f, 0.20f, 0.70f)),
            new StatusIcon(AntStatus.Digging,   "E", new Color(0.55f, 0.38f, 0.22f)),
            new StatusIcon(AntStatus.Working,   "T", new Color(0.25f, 0.45f, 0.75f)),
            new StatusIcon(AntStatus.Eating,    "C", new Color(0.30f, 0.65f, 0.30f)),
            new StatusIcon(AntStatus.Resting,   "z", new Color(0.50f, 0.50f, 0.70f)),
            new StatusIcon(AntStatus.Carrying,  "L", new Color(0.70f, 0.60f, 0.20f)),
        };

        /// <summary>Lo que enseñaría ahora (aunque esté oculto por el zoom).</summary>
        public AntStatus Status { get; private set; }

        /// <summary>Se está viendo.</summary>
        public bool IsShown => root != null && root.activeSelf;

        public static bool IsAlert(AntStatus s) => s == AntStatus.Starving || s == AntStatus.Exhausted || s == AntStatus.NoPath;

        private Ant ant;
        private IAntBrain brain;
        private IAntNeeds needs;
        private AntActuator actuator;
        private Camera cam;

        private GameObject root;
        private SpriteRenderer outline, background, iconRenderer;
        private TextMeshPro label;
        private AntStatus shownStatus = (AntStatus)(-1);
        private float pollTimer;
        private bool dirty = true;

        private static Sprite disc;
        private static Dictionary<AntAction, AntStatus> actionToStatus;

        // ================= CICLO =================

        private void Awake()
        {
            ant = GetComponent<Ant>();
            brain = GetComponent<IAntBrain>();
            needs = GetComponent<IAntNeeds>();
            actuator = GetComponent<AntActuator>();
            if (visual == null) visual = transform.Find("Visual");
            BuildActionMap();
            CreateBubble();
            pollTimer = UnityEngine.Random.Range(0f, pollInterval);   // escalonado entre hormigas
        }

        private void OnEnable()
        {
            if (brain != null) brain.StateChanged += OnStateChanged;
            dirty = true;
        }

        private void OnDisable()
        {
            if (brain != null) brain.StateChanged -= OnStateChanged;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }

        private void OnStateChanged(BrainGoal goal, BrainStep step) => dirty = true;

        private void Update()
        {
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer <= 0f) { pollTimer = pollInterval; dirty = true; }
            if (!dirty) return;
            dirty = false;
            Status = Evaluate();
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;

            bool show = Status != AntStatus.None && (IsAlert(Status) || IsCloseOrSelected());
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            if (Status != shownStatus) ApplyIcon(Status);
            Place();
        }

        // ================= ESTADO =================

        private AntStatus Evaluate()
        {
            if (!ant.IsAlive) return AntStatus.None;
            if (brain != null && brain.IsSuspended) return AntStatus.None;   // expedición, Festín…

            // Alertas
            if (needs != null && needs.Hunger01 >= criticalHunger) return AntStatus.Starving;
            if (needs != null && needs.IsExhausted) return AntStatus.Exhausted;
            if (brain != null && brain.HasNoPath) return AntStatus.NoPath;

            // Actividad
            if (needs != null && needs.IsResting) return AntStatus.Resting;
            if (actuator != null && actionToStatus.TryGetValue(actuator.Current, out var s)) return s;
            return AntStatus.None;
        }

        private bool IsCloseOrSelected()
        {
            if (AntSelection.Current == ant) return true;
            return cam != null && cam.orthographicSize <= closeZoom;
        }

        /// <summary>
        /// Acción del actuador → actividad. Por nombre, para no depender de valores que aún no existen
        /// (Digging llega con B6; Carrying, con el transporte). Moving e Idle no enseñan nada.
        /// </summary>
        private static void BuildActionMap()
        {
            if (actionToStatus != null) return;
            actionToStatus = new Dictionary<AntAction, AntStatus>();
            foreach (AntAction a in Enum.GetValues(typeof(AntAction)))
            {
                switch (a.ToString())
                {
                    case "Working": actionToStatus[a] = AntStatus.Working; break;
                    case "Eating": actionToStatus[a] = AntStatus.Eating; break;
                    case "Digging": actionToStatus[a] = AntStatus.Digging; break;
                    case "Carrying": actionToStatus[a] = AntStatus.Carrying; break;
                }
            }
        }

        // ================= DIBUJO =================

        private void CreateBubble()
        {
            root = new GameObject($"StatusBubble ({ant.Name})");
            root.SetActive(false);

            outline = NewDisc("Outline", root.transform, sortingOrder, 1.15f);
            outline.color = outlineColor;
            background = NewDisc("Background", root.transform, sortingOrder + 1, 1f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            iconRenderer = iconGo.AddComponent<SpriteRenderer>();
            iconRenderer.sortingOrder = sortingOrder + 2;

            var labelGo = new GameObject("Letter");
            labelGo.transform.SetParent(root.transform, false);
            label = labelGo.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = Vector2.one;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 6f;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = letterColor;
            label.sortingOrder = sortingOrder + 3;
        }

        private static SpriteRenderer NewDisc(string name, Transform parent, int order, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Disc();
            sr.sortingOrder = order;
            return sr;
        }

        private void ApplyIcon(AntStatus status)
        {
            shownStatus = status;
            var entry = icons.Find(i => i.status == status);
            Color color = entry != null ? entry.color : Color.gray;
            background.color = color;

            bool hasSprite = entry != null && entry.sprite != null;
            iconRenderer.sprite = hasSprite ? entry.sprite : null;
            iconRenderer.enabled = hasSprite;
            if (hasSprite)
            {
                // El sprite cabe dentro del círculo (70 % del diámetro).
                var size = entry.sprite.bounds.size;
                float s = 0.7f / Mathf.Max(size.x, size.y, 0.0001f);
                iconRenderer.transform.localScale = Vector3.one * s;
            }

            label.enabled = !hasSprite;
            label.text = entry != null ? entry.letter : "";
        }

        private void Place()
        {
            // Tamaño constante en pantalla: escala proporcional al zoom.
            float zoomFactor = cam != null && referenceZoom > 0f ? cam.orthographicSize / referenceZoom : 1f;
            float scale = diameter * zoomFactor;

            float pulse = 1f;
            if (IsAlert(Status))
                pulse += pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));

            // Sobre la cabeza: la base de la burbuja queda en headHeight, crezca lo que crezca.
            Vector3 anchor = visual != null ? visual.position : transform.position;
            anchor.y = transform.position.y + headHeight + scale * 0.5f;
            anchor.z = transform.position.z;

            root.transform.position = anchor;
            root.transform.localScale = Vector3.one * (scale * pulse);
        }

        /// <summary>Círculo blanco suavizado, creado una vez y compartido (1 unidad de diámetro).</summary>
        private static Sprite Disc()
        {
            if (disc != null) return disc;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "AntStatusBubbleDisc"
            };
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            disc.name = "AntStatusBubbleDisc";
            return disc;
        }
    }
}
