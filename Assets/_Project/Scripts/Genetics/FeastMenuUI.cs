using System;
using System.Collections.Generic;
using Myrmutation.Ants;
using Myrmutation.Building;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Expedition;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Myrmutation.Genetics
{
    /// <summary>
    /// Menú del Festín (C9). Se abre al tocar la Sala del Festín (construida).
    ///
    ///  Paso 1 · Elegir presa (las guardadas por la expedición: sprite de su especie, nombre y gen).
    ///           El gen sale como "?" hasta que se devora la primera presa de esa especie.
    ///  Paso 2 · Elegir quién la devora (lista de hormigas; la reina NO come presas).
    ///  Paso 3 · Confirmar. Se aplican las reglas del Festín (FeastSystem.DevourPrey: biomasa, dado de mutación).
    ///
    /// Al devorar, la especie queda descubierta (SpeciesRegistry) y se avisa en el panel y en el log.
    /// Va en el prefab FeastPanel (menú Myrmutation → C9 → Crear prefab FeastPanel), en el lado
    /// contrario al panel de expedición. Usa tiempo sin escalar (sigue vivo en pausa).
    /// </summary>
    public class FeastMenuUI : MonoBehaviour
    {
        private enum Step { Prey, Eater, Confirm }

        [Header("Panel")]
        [Tooltip("Parte visible del menú (se muestra y se oculta). Este objeto, la raíz, debe quedar siempre activo")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text resultText;

        [Header("Lista")]
        [SerializeField] private Transform listContainer;
        [Tooltip("Fila plantilla (desactivada) con hijos SpeciesIcon, Label y GeneIcon")]
        [SerializeField] private Button rowTemplate;

        [Header("Botones")]
        [Tooltip("Paso 1: Cerrar · Pasos 2 y 3: Atrás")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonText;
        [SerializeField] private Button confirmButton;

        [Header("Opciones")]
        [Tooltip("Abrir el menú al tocar la Sala del Festín construida")]
        [SerializeField] private bool openOnRoomTap = true;
        [SerializeField] private float resultSeconds = 6f;

        [Header("Pruebas (menú ⋮ → Añadir presas de prueba)")]
        [SerializeField] private List<Prey> testPrey = new List<Prey>();

        private static readonly Color PlaceholderColor = new Color(0.35f, 0.40f, 0.50f, 1f);

        private readonly List<GameObject> rows = new List<GameObject>();
        private Step step;
        private Prey selectedPrey;
        private Ant selectedEater;
        private bool busy;           // true mientras se confirma (ignora los eventos que provoca el propio Festín)
        private float resultTimer;

        public bool IsOpen => panel != null && panel.activeSelf;

        // ================= CICLO =================

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (resultText != null) resultText.gameObject.SetActive(false);

            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        }

        private void OnEnable()
        {
            CameraController.Tapped += OnTap;
            EventBus.Subscribe<PreyStockChanged>(OnStockChanged);
            EventBus.Subscribe<AntDied>(OnAntDied);
        }

        private void OnDisable()
        {
            CameraController.Tapped -= OnTap;
            EventBus.Unsubscribe<PreyStockChanged>(OnStockChanged);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
        }

        private void Update()
        {
            if (resultTimer <= 0f) return;
            resultTimer -= Time.unscaledDeltaTime;
            if (resultTimer <= 0f && resultText != null) resultText.gameObject.SetActive(false);
        }

        // ================= ABRIR / CERRAR =================

        public void Open()
        {
            if (panel == null) return;
            selectedPrey = null;
            selectedEater = null;
            panel.SetActive(true);
            ShowStep(Step.Prey);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            selectedPrey = null;
            selectedEater = null;
        }

        /// <summary>Tocar una Sala del Festín construida abre el menú.</summary>
        private void OnTap(Vector2 world)
        {
            if (!openOnRoomTap || panel == null) return;
            if (AntSelection.Instance != null && AntSelection.Instance.IsPickingRoom) return;   // eligiendo sala para una hormiga

            var room = RoomSpawner.RoomAt(world);   // la sala bajo el toque (la misma que usa AntSelection)
            if (room == null || !room.IsBuilt || room.Data == null || room.Data.type != RoomType.Feast) return;

            if (!IsOpen) Open();
        }

        // ================= PASOS =================

        private void ShowStep(Step next)
        {
            step = next;
            ClearRows();

            if (summaryText != null) summaryText.gameObject.SetActive(step == Step.Confirm);
            if (confirmButton != null) confirmButton.gameObject.SetActive(step == Step.Confirm);
            if (backButtonText != null) backButtonText.text = step == Step.Prey ? "Cerrar" : "Atrás";

            switch (step)
            {
                case Step.Prey: BuildPreyRows(); break;
                case Step.Eater: BuildEaterRows(); break;
                case Step.Confirm: BuildSummary(); break;
            }
            ScrollToTop();
        }

        private void BuildPreyRows()
        {
            var exp = ExpeditionSystem.Instance;
            int count = exp != null ? exp.StoredPrey.Count : 0;

            if (stepText != null)
                stepText.text = count > 0 ? "1/3 · Elige una presa" : "No hay presas guardadas";
            if (exp == null) return;

            foreach (var prey in exp.StoredPrey)
            {
                if (prey == null) continue;
                bool known = IsDiscovered(prey);
                var captured = prey;

                AddRow($"{prey.displayName}\n<size=80%>{GeneLine(prey)}</size>",
                       speciesSprite: prey.sprite, showSpecies: true,
                       geneSprite: known && prey.gene != null ? prey.gene.icon : null, showGene: known && prey.gene != null,
                       onClick: () => { selectedPrey = captured; ShowStep(Step.Eater); });
            }
        }

        private void BuildEaterRows()
        {
            if (stepText != null) stepText.text = "2/3 · ¿Quién la devora?";

            var ants = new List<Ant>();
            foreach (var a in Ant.All)
                if (a != null && a.IsAlive && !a.IsQueen) ants.Add(a);   // la reina no come presas
            ants.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));

            if (ants.Count == 0 && stepText != null) stepText.text = "No hay hormigas disponibles";

            foreach (var ant in ants)
            {
                var captured = ant;
                string caste = ant.Caste != null ? $"Casta {ant.Caste.displayName}" : "Sin casta";
                AddRow($"{ant.Name}\n<size=80%>{caste}</size>",
                       null, false, null, false,
                       () => { selectedEater = captured; ShowStep(Step.Confirm); });
            }
        }

        private void BuildSummary()
        {
            if (stepText != null) stepText.text = "3/3 · Confirmar";
            if (summaryText == null || selectedPrey == null || selectedEater == null) return;
            summaryText.text = $"{selectedPrey.displayName} ({GeneLine(selectedPrey)})\ndevorada por {selectedEater.Name}";
        }

        // ================= BOTONES =================

        private void OnBack()
        {
            switch (step)
            {
                case Step.Prey: Close(); break;
                case Step.Eater: selectedEater = null; ShowStep(Step.Prey); break;
                default: ShowStep(Step.Eater); break;
            }
        }

        private void OnConfirm()
        {
            var exp = ExpeditionSystem.Instance;
            var feast = FeastSystem.Instance;
            if (exp == null || feast == null || selectedPrey == null || selectedEater == null || !selectedEater.IsAlive)
            {
                ShowResult("No se puede devorar ahora");
                ShowStep(Step.Prey);
                return;
            }

            // La presa elegida tiene que seguir en el almacén (puede haber cambiado mientras elegías).
            int index = IndexOfStored(exp, selectedPrey);
            if (index < 0)
            {
                ShowResult("Esa presa ya no está disponible");
                ShowStep(Step.Prey);
                return;
            }

            busy = true;
            try
            {
                if (!exp.TryTakePrey(out var prey, index))
                {
                    ShowResult("Esa presa ya no está disponible");
                    ShowStep(Step.Prey);
                    return;
                }

                // Si el Festín no se puede hacer, la presa vuelve al almacén en vez de perderse.
                if (!feast.DevourPrey(selectedEater, prey.gene, out var outcome, out var gained))
                {
                    exp.AddPrey(prey);
                    ShowResult("No se puede celebrar el Festín ahora");
                    ShowStep(Step.Prey);
                    return;
                }

                bool first = SpeciesRegistry.Instance != null && SpeciesRegistry.Instance.Discover(prey);
                ShowResult(BuildResult(selectedEater, prey, first, outcome, gained));

                selectedPrey = null;
                selectedEater = null;
                ShowStep(Step.Prey);
            }
            finally
            {
                busy = false;
            }
        }

        // ================= EVENTOS =================

        private void OnStockChanged(PreyStockChanged e)
        {
            if (busy || !IsOpen) return;

            // Si la presa elegida desapareció, se vuelve al paso 1.
            if (step != Step.Prey && selectedPrey != null &&
                (ExpeditionSystem.Instance == null || IndexOfStored(ExpeditionSystem.Instance, selectedPrey) < 0))
            {
                selectedPrey = null;
                selectedEater = null;
                ShowResult("Esa presa ya no está disponible");
                ShowStep(Step.Prey);
                return;
            }

            if (step == Step.Prey) ShowStep(Step.Prey);   // llegó una presa nueva: refresca la lista
        }

        private void OnAntDied(AntDied e)
        {
            if (busy || !IsOpen) return;

            if (e.Ant == selectedEater)
            {
                selectedEater = null;
                if (step == Step.Confirm) ShowStep(Step.Eater);
            }
            else if (step == Step.Eater) ShowStep(Step.Eater);
        }

        // ================= TEXTOS =================

        private bool IsDiscovered(Prey prey) =>
            SpeciesRegistry.Instance != null && SpeciesRegistry.Instance.IsDiscovered(prey);

        private string GeneLine(Prey prey)
        {
            if (!IsDiscovered(prey)) return "Gen: ?";
            return prey.gene != null ? $"Gen: {prey.gene.displayName}" : "Sin gen";
        }

        private static string BuildResult(Ant eater, Prey prey, bool first, FeastOutcome outcome, GeneData gained)
        {
            string text;

            if (prey.gene == null)
            {
                text = first ? $"¡Descubierto! {prey.displayName} → no tiene gen" : "Esa presa no tiene gen";
            }
            else if (outcome == FeastOutcome.Gene)
            {
                text = first ? $"¡Descubierto! {prey.displayName} → {prey.gene.displayName}"
                             : $"{eater.Name} ha ganado {prey.gene.displayName}";
            }
            else
            {
                string notPassed = $"{prey.gene.displayName} (esta vez no se transmitió)";
                text = first ? $"¡Descubierto! {prey.displayName} → {notPassed}" : notPassed;
            }

            if (outcome == FeastOutcome.Flaw && gained != null)
                text += $"\n{eater.Name} ha salido {gained.displayName}";
            return text;
        }

        private void ShowResult(string message)
        {
            if (resultText == null) return;
            resultText.text = message;
            resultText.gameObject.SetActive(true);
            resultTimer = resultSeconds;
        }

        // ================= FILAS =================

        private void AddRow(string label, Sprite speciesSprite, bool showSpecies, Sprite geneSprite, bool showGene, UnityAction onClick)
        {
            if (rowTemplate == null || listContainer == null) return;

            var b = Instantiate(rowTemplate, listContainer);
            b.gameObject.SetActive(true);

            var text = b.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;

            // Una presa sin sprite de especie NO usa el icono del gen (revelaría el gen antes de descubrirlo).
            SetIcon(b.transform.Find("SpeciesIcon"), speciesSprite, showSpecies);
            SetIcon(b.transform.Find("GeneIcon"), geneSprite, showGene);

            b.onClick.AddListener(onClick);
            rows.Add(b.gameObject);
        }

        private static void SetIcon(Transform icon, Sprite sprite, bool visible)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(visible);
            if (!visible) return;

            var img = icon.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : PlaceholderColor;
        }

        private void ClearRows()
        {
            foreach (var go in rows)
            {
                if (go == null) continue;
                go.SetActive(false);
                Destroy(go);
            }
            rows.Clear();
        }

        private void ScrollToTop()
        {
            if (listContainer == null) return;
            var scroll = listContainer.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        private static int IndexOfStored(ExpeditionSystem exp, Prey prey)
        {
            var stored = exp.StoredPrey;
            for (int i = 0; i < stored.Count; i++)
                if (stored[i] == prey) return i;
            return -1;
        }

        // ================= PRUEBAS =================

        [ContextMenu("Abrir panel")]
        private void OpenFromMenu() => Open();

        [ContextMenu("Añadir presas de prueba")]
        private void AddTestPrey()
        {
            var exp = ExpeditionSystem.Instance;
            if (exp == null)
            {
                Debug.LogWarning("[FeastMenu] No hay ExpeditionSystem en la escena.", this);
                return;
            }
            foreach (var prey in testPrey) exp.AddPrey(prey);
        }
    }
}