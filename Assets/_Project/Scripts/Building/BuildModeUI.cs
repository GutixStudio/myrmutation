using System.Collections.Generic;
using Myrmutation.Core.Data;
using Myrmutation.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.Building
{
    /// <summary>
    /// UI del modo Construcción (§5): botón Construir en la barra inferior y, dentro del modo, la barra
    /// Túnel · Salas · Borrar · ▶ · ✓ · ✕, la lista de salas, los botones ✓ / ✕ del fantasma en táctil y la línea
    /// de estado. Mientras el modo está abierto, los botones de velocidad del HUD (lockedWhileOpen) quedan desactivados:
    /// ▶ es la única forma de reanudar. Funciona en pausa (tiempo sin escala).
    /// </summary>
    public class BuildModeUI : MonoBehaviour
    {
        [SerializeField] private BuildModeController controller;

        [Header("Barra inferior")]
        [SerializeField] private Button buildButton;
        [Tooltip("Botones que se desactivan mientras el modo está abierto (velocidad y pausa del HUD)")]
        [SerializeField] private Selectable[] lockedWhileOpen;

        [Header("Barra del modo")]
        [SerializeField] private GameObject toolbar;
        [SerializeField] private Button tunnelButton;
        [SerializeField] private Button roomButton;
        [SerializeField] private Button eraseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Salas")]
        [SerializeField] private GameObject roomList;
        [Tooltip("Contenedor de las tarjetas (con Layout Group)")]
        [SerializeField] private Transform roomContainer;
        [Tooltip("Tarjeta plantilla (desactivada) que se duplica por cada sala; un Image hijo hace de icono")]
        [SerializeField] private Button roomTemplate;
        [SerializeField] private RoomData[] rooms;
        [Tooltip("Botones ✓ / ✕ del fantasma en táctil")]
        [SerializeField] private GameObject ghostButtons;
        [SerializeField] private Button ghostConfirmButton;
        [SerializeField] private Button ghostCancelButton;

        [Header("Aspecto")]
        [SerializeField] private Color activeColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("Segundos (sin escala) entre refrescos de la línea de estado")]
        [SerializeField] private float statusRefreshInterval = 0.25f;

        private readonly List<(Button button, RoomData data)> cards = new List<(Button, RoomData)>();
        private bool[] lockedState;
        private bool wasOpen;
        private float statusTimer;

        private void Awake()
        {
            buildButton.onClick.AddListener(controller.Open);
            tunnelButton.onClick.AddListener(() => controller.SelectTool(BuildTool.Tunnel));
            roomButton.onClick.AddListener(() => controller.SelectTool(BuildTool.Room));
            eraseButton.onClick.AddListener(() => controller.SelectTool(BuildTool.Erase));
            resumeButton.onClick.AddListener(controller.TogglePause);
            confirmButton.onClick.AddListener(controller.Confirm);
            cancelButton.onClick.AddListener(controller.Cancel);
            ghostConfirmButton.onClick.AddListener(controller.PlacePendingRoom);
            ghostCancelButton.onClick.AddListener(controller.DiscardPendingRoom);

            roomTemplate.gameObject.SetActive(false);
            foreach (var data in rooms)
            {
                if (data == null || !data.buildable) continue;
                var b = Instantiate(roomTemplate, roomContainer);
                b.gameObject.SetActive(true);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = $"{data.displayName}\n<size=70%>{Mathf.RoundToInt(data.workCost)} de trabajo · {data.workerCapacity} obreras</size>";
                foreach (var img in b.GetComponentsInChildren<Image>(true))
                    if (img != b.targetGraphic) { img.sprite = data.icon; img.enabled = data.icon != null; }
                var picked = data;
                b.onClick.AddListener(() => controller.SelectRoom(picked));
                cards.Add((b, data));
            }
            lockedState = new bool[lockedWhileOpen.Length];
            Show(false);
        }

        private void Update()
        {
            bool open = controller.IsOpen;
            if (open != wasOpen)
            {
                wasOpen = open;
                Show(open);
                LockSpeedButtons(open);
            }
            if (!open) return;

            roomList.SetActive(controller.Tool == BuildTool.Room);
            ghostButtons.SetActive(controller.HasPendingRoom);
            Tint(tunnelButton, controller.Tool == BuildTool.Tunnel);
            Tint(roomButton, controller.Tool == BuildTool.Room);
            Tint(eraseButton, controller.Tool == BuildTool.Erase);
            Tint(resumeButton, controller.IsRunning);
            foreach (var (button, data) in cards) Tint(button, controller.SelectedRoom == data);

            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer > 0f) return;
            statusTimer = statusRefreshInterval;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            string free = ColonyStats.Instance != null ? ColonyStats.Instance.FreeWorkers.ToString() : "—";
            string text = $"Planos: {controller.Planner.PlannedChunks} bloques · Obreras libres: {free}";
            if (controller.Tool == BuildTool.Room && controller.HasGhost && controller.GhostResult != RoomPlanResult.Ok)
                text += $"\n<color=#E06050>{DigPlanner.Describe(controller.GhostResult)}</color>";
            if (statusText.text != text) statusText.text = text;
        }

        private void Show(bool open)
        {
            buildButton.gameObject.SetActive(!open);
            toolbar.SetActive(open);
            roomList.SetActive(false);
            ghostButtons.SetActive(false);
            statusTimer = 0f;
        }

        // Al entrar guarda si cada botón estaba activo y al salir lo deja como estaba.
        private void LockSpeedButtons(bool locked)
        {
            for (int i = 0; i < lockedWhileOpen.Length; i++)
            {
                if (lockedWhileOpen[i] == null) continue;
                if (locked) { lockedState[i] = lockedWhileOpen[i].interactable; lockedWhileOpen[i].interactable = false; }
                else lockedWhileOpen[i].interactable = lockedState[i];
            }
        }

        private void Tint(Button b, bool active)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = active ? activeColor : normalColor;
        }
    }
}
