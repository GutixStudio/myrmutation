using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.Ants
{
    /// <summary>
    /// Panel de la hormiga seleccionada (B4). Se abre al recibir AntSelected y se cierra con AntSelected(null).
    ///
    ///  · Nombre, casta, estado ("Trabajando en Hongos"…), iconos de genes y barras de hambre/energía.
    ///  · "Asignar a sala": abre una lista de salas con el rendimiento de ESTA hormiga en cada una
    ///    (ColonyProduction.GetEfficiency) y su ocupación. Mientras la lista está abierta, también
    ///    se puede tocar la sala directamente en el mapa (AntSelection.BeginRoomPick).
    ///  · Hueco de acciones: botones que añaden otros sistemas con AntPanelAction.Register (C: Devorar).
    ///
    /// Los botones de genes, salas y acciones se crean duplicando una plantilla desactivada,
    /// igual que BuildMenuUI.
    /// </summary>
    public class AntPanelUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;

        [Header("Datos")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text casteText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text roomText;

        [Header("Genes")]
        [Tooltip("Contenedor de los iconos (con Horizontal Layout Group)")]
        [SerializeField] private Transform geneContainer;
        [Tooltip("Image plantilla (desactivada) que se duplica por cada gen")]
        [SerializeField] private Image geneIconTemplate;
        [Tooltip("Opcional: texto que se muestra si no tiene genes")]
        [SerializeField] private GameObject noGenesLabel;

        [Header("Necesidades (Image con Image Type = Filled)")]
        [SerializeField] private Image hungerFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Color hungerColor = new Color(0.55f, 0.75f, 0.35f);
        [SerializeField] private Color hungryColor = new Color(0.9f, 0.3f, 0.2f);
        [SerializeField] private Color energyColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color tiredColor = new Color(0.6f, 0.45f, 0.75f);

        [Header("Asignar a sala")]
        [SerializeField] private Button assignButton;
        [SerializeField] private GameObject roomListPanel;
        [Tooltip("Contenedor de los botones de sala (con Vertical Layout Group)")]
        [SerializeField] private Transform roomListContainer;
        [Tooltip("Botón plantilla (desactivado) que se duplica por cada sala")]
        [SerializeField] private Button roomButtonTemplate;
        [SerializeField] private Button roomListCloseButton;

        [Header("Acciones de otros sistemas")]
        [Tooltip("Contenedor de los botones de acción (con Horizontal o Vertical Layout Group)")]
        [SerializeField] private Transform actionContainer;
        [Tooltip("Botón plantilla (desactivado) que se duplica por cada acción registrada")]
        [SerializeField] private Button actionButtonTemplate;

        private Ant ant;
        private AntNeeds needs;
        private AntBrain brain;
        private AntActuator actuator;

        private readonly List<GameObject> geneIcons = new List<GameObject>();
        private readonly List<GameObject> roomButtons = new List<GameObject>();
        private readonly Dictionary<AntPanelAction.Entry, Button> actionButtons = new Dictionary<AntPanelAction.Entry, Button>();
        private readonly List<GeneData> shownGenes = new List<GeneData>();
        private float slowTimer;

        // ================= CICLO =================

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (roomListPanel != null) roomListPanel.SetActive(false);
            if (geneIconTemplate != null) geneIconTemplate.gameObject.SetActive(false);
            if (roomButtonTemplate != null) roomButtonTemplate.gameObject.SetActive(false);
            if (actionButtonTemplate != null) actionButtonTemplate.gameObject.SetActive(false);

            if (closeButton != null) closeButton.onClick.AddListener(() => AntSelection.Instance?.Deselect());
            if (assignButton != null) assignButton.onClick.AddListener(ToggleRoomList);
            if (roomListCloseButton != null) roomListCloseButton.onClick.AddListener(CloseRoomList);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AntSelected>(OnAntSelected);
            AntPanelAction.Changed += RebuildActions;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AntSelected>(OnAntSelected);
            AntPanelAction.Changed -= RebuildActions;
        }

        private void Update()
        {
            if (ant == null) { if (panel != null && panel.activeSelf) Close(); return; }

            RefreshNeeds();
            RefreshState();

            // Lo que cambia poco, dos veces por segundo.
            slowTimer -= Time.unscaledDeltaTime;
            if (slowTimer > 0f) return;
            slowTimer = 0.5f;
            RefreshGenesIfChanged();
            RefreshActionVisibility();
        }

        // ================= ABRIR / CERRAR =================

        private void OnAntSelected(AntSelected e)
        {
            if (e.Ant == null) { Close(); return; }
            Open(e.Ant);
        }

        private void Open(Ant target)
        {
            ant = target;
            needs = ant.GetComponent<AntNeeds>();
            brain = ant.GetComponent<AntBrain>();
            actuator = ant.GetComponent<AntActuator>();

            CloseRoomList();
            if (nameText != null) nameText.text = ant.Name;
            if (casteText != null) casteText.text = ant.IsQueen ? "Reina" : ant.Caste != null ? $"Casta {ant.Caste.displayName}" : "Sin casta";
            if (assignButton != null) assignButton.gameObject.SetActive(!ant.IsQueen);

            shownGenes.Clear();
            RebuildGenes();
            RebuildActions();
            RefreshNeeds();
            RefreshState();
            slowTimer = 0.5f;

            if (panel != null) panel.SetActive(true);
        }

        private void Close()
        {
            CloseRoomList();
            ant = null; needs = null; brain = null; actuator = null;
            if (panel != null) panel.SetActive(false);
        }

        // ================= DATOS =================

        private void RefreshNeeds()
        {
            if (needs == null) return;
            if (hungerFill != null)
            {
                hungerFill.fillAmount = needs.Hunger01;
                hungerFill.color = needs.IsHungry ? hungryColor : hungerColor;
            }
            if (energyFill != null)
            {
                energyFill.fillAmount = needs.Energy01;
                energyFill.color = needs.IsTired || needs.IsExhausted ? tiredColor : energyColor;
            }
        }

        private void RefreshState()
        {
            if (roomText != null)
                roomText.text = ant.IsQueen ? "" : $"Sala: {RoomName(ant.AssignedRoom, "ninguna")}";
            if (stateText != null) stateText.text = DescribeState();
        }

        /// <summary>Qué está haciendo, en palabras del jugador.</summary>
        private string DescribeState()
        {
            if (ant.IsQueen) return needs != null && needs.IsResting ? "Descansando" : "Gobernando la colonia";
            if (brain == null) return actuator != null ? actuator.Current.ToString() : "";
            if (brain.IsSuspended) return "Ocupada";

            string room = RoomName(ant.AssignedRoom, "su sala");
            switch (brain.Goal)
            {
                case BrainGoal.Eat:
                    return brain.Step == BrainStep.Acting ? "Comiendo" : "Yendo a comer";
                case BrainGoal.Rest:
                    if (needs != null && needs.IsExhausted && brain.Step == BrainStep.Acting) return "Agotada: descansando";
                    return brain.Step == BrainStep.Acting ? "Descansando" : "Yendo a descansar";
                default:
                    if (ant.AssignedRoom == null) return "Sin sala asignada";
                    if (brain.Step == BrainStep.Acting) return ant.AssignedRoom.IsBuilt ? $"Trabajando en {room}" : $"Construyendo {room}";
                    if (brain.Step == BrainStep.Moving) return $"Yendo a {room}";
                    return "Esperando";
            }
        }

        // ================= GENES =================

        private void RefreshGenesIfChanged()
        {
            if (shownGenes.Count != ant.Genes.Count) { RebuildGenes(); return; }
            for (int i = 0; i < shownGenes.Count; i++)
                if (shownGenes[i] != ant.Genes[i]) { RebuildGenes(); return; }
        }

        private void RebuildGenes()
        {
            foreach (var go in geneIcons) if (go != null) Destroy(go);
            geneIcons.Clear();
            shownGenes.Clear();
            shownGenes.AddRange(ant.Genes);

            if (geneIconTemplate != null && geneContainer != null)
                foreach (var gene in ant.Genes)
                {
                    if (gene == null) continue;
                    var icon = Instantiate(geneIconTemplate, geneContainer);
                    icon.gameObject.SetActive(true);
                    icon.gameObject.name = "Gene_" + gene.displayName;
                    if (gene.icon != null) { icon.sprite = gene.icon; icon.color = Color.white; }
                    else icon.color = gene.tint;   // sin icono: un cuadro del color del gen
                    var label = icon.GetComponentInChildren<TMP_Text>(true);   // opcional: nombre bajo el icono
                    if (label != null) label.text = gene.displayName;
                    geneIcons.Add(icon.gameObject);
                }

            if (noGenesLabel != null) noGenesLabel.SetActive(geneIcons.Count == 0);
        }

        // ================= ASIGNAR A SALA =================

        private void ToggleRoomList()
        {
            if (roomListPanel != null && roomListPanel.activeSelf) CloseRoomList();
            else OpenRoomList();
        }

        private void OpenRoomList()
        {
            if (ant == null || ant.IsQueen) return;
            ClearRoomButtons();

            AddRoomButton(null, "Ninguna", "", true);
            foreach (var room in Room.All)
            {
                if (room == null || room.Data == null || room.Data.type == RoomType.Royal) continue;

                int used = CountAssigned(room);
                int cap = Mathf.Max(1, room.Data.workerCapacity);
                bool isCurrent = ant.AssignedRoom == room;
                bool full = used >= cap && !isCurrent;

                float eff = ColonyProduction.GetEfficiency(ant, room.Data.type);
                string effText = $"x{eff:0.0#}";
                string effColored = eff > 1.01f ? $"<color=#7CC35A>{effText}</color>"
                                  : eff < 0.99f ? $"<color=#E0604A>{effText}</color>" : effText;
                string building = room.IsBuilt ? "" : " (en obras)";
                string detail = $"{effColored} · {used}/{cap}{(full ? " · llena" : "")}";

                AddRoomButton(room, room.Data.displayName + building, detail, !full);
            }

            if (roomListPanel != null) roomListPanel.SetActive(true);
            // Además de la lista, tocar una sala en el mapa también sirve.
            AntSelection.Instance?.BeginRoomPick(AssignTo, () => { if (roomListPanel != null) roomListPanel.SetActive(false); ClearRoomButtons(); });
        }

        private void CloseRoomList()
        {
            AntSelection.Instance?.CancelRoomPick();
            ClearRoomButtons();
            if (roomListPanel != null) roomListPanel.SetActive(false);
        }

        private void AddRoomButton(Room room, string title, string detail, bool interactable)
        {
            if (roomButtonTemplate == null || roomListContainer == null) return;
            var b = Instantiate(roomButtonTemplate, roomListContainer);
            b.gameObject.SetActive(true);
            b.interactable = interactable;
            var label = b.GetComponentInChildren<TMP_Text>();
            bool current = ant != null && ant.AssignedRoom == room;
            if (label != null)
                label.text = (current ? "► " : "") + title + (string.IsNullOrEmpty(detail) ? "" : $"\n<size=70%>{detail}</size>");
            b.onClick.AddListener(() => AssignTo(room));
            roomButtons.Add(b.gameObject);
        }

        private void ClearRoomButtons()
        {
            foreach (var go in roomButtons) if (go != null) Destroy(go);
            roomButtons.Clear();
        }

        private void AssignTo(Room room)
        {
            if (ant == null || ant.IsQueen) return;
            if (room != null && room.Data != null && room.Data.type == RoomType.Royal) return;
            if (room != null && room != ant.AssignedRoom && room.Data != null &&
                CountAssigned(room) >= Mathf.Max(1, room.Data.workerCapacity)) return;   // llena

            ant.AssignedRoom = room;
            brain?.Think();   // que reaccione ya, sin esperar a su siguiente ciclo
            CloseRoomList();
            RefreshState();
        }

        private static int CountAssigned(Room room)
        {
            int n = 0;
            foreach (var a in Ant.All)
                if (a != null && a.IsAlive && !a.IsQueen && a.AssignedRoom == room) n++;
            return n;
        }

        private static string RoomName(Room room, string fallback) =>
            room == null ? fallback : room.Data != null ? room.Data.displayName : room.name;

        // ================= ACCIONES DE OTROS SISTEMAS =================

        private void RebuildActions()
        {
            foreach (var kv in actionButtons) if (kv.Value != null) Destroy(kv.Value.gameObject);
            actionButtons.Clear();
            if (ant == null || actionButtonTemplate == null || actionContainer == null) return;

            foreach (var entry in AntPanelAction.All)
            {
                var b = Instantiate(actionButtonTemplate, actionContainer);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = entry.Label;
                var e = entry;
                b.onClick.AddListener(() =>
                {
                    if (ant == null) return;
                    e.OnClick(ant);
                    RefreshActionVisibility();
                });
                actionButtons[entry] = b;
            }
            RefreshActionVisibility();
        }

        private void RefreshActionVisibility()
        {
            if (ant == null) return;
            foreach (var kv in actionButtons)
            {
                if (kv.Value == null) continue;
                bool show = kv.Key.IsAvailable == null || kv.Key.IsAvailable(ant);
                if (kv.Value.gameObject.activeSelf != show) kv.Value.gameObject.SetActive(show);
            }
        }
    }
}
