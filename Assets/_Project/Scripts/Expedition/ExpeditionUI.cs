using Myrmutation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.Expedition
{
    /// <summary>
    /// Panel de expedición (D4): elegir 1–3 hormigas con − / +, botón "Enviar expedición",
    /// estado (disponible / fuera / invierno) y aviso de lo que han traído.
    /// Todas las referencias son opcionales salvo sendButton. Va en un Canvas (prefab ExpeditionPanel).
    /// </summary>
    public class ExpeditionUI : MonoBehaviour
    {
        [Header("Grupo")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private int startCount = 2;

        [Header("Enviar")]
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text sendButtonText;

        [Header("Estado")]
        [Tooltip("Disponible / Fuera: 12 s / motivo por el que no se puede")]
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Opcional: obreras disponibles y apuntadas")]
        [SerializeField] private TMP_Text infoText;
        [Tooltip("Opcional: Image con Image Type = Filled para el progreso de la expedición")]
        [SerializeField] private Image progressFill;
        [Tooltip("Opcional: objeto que se muestra solo con la expedición fuera (p. ej. la barra con su fondo). Vacío = progressFill")]
        [SerializeField] private GameObject progressBar;
        [Tooltip("Opcional: presas guardadas")]
        [SerializeField] private TMP_Text preyCountText;

        [Header("Aviso al volver")]
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private float resultSeconds = 4f;

        private int count;
        private float resultTimer;
        private float refreshTimer;

        private ExpeditionSystem Sys => ExpeditionSystem.Instance;

        private void Awake()
        {
            count = Mathf.Clamp(startCount, ExpeditionSystem.MinGroup, ExpeditionSystem.MaxGroup);
            if (minusButton != null) minusButton.onClick.AddListener(() => ChangeCount(-1));
            if (plusButton != null) plusButton.onClick.AddListener(() => ChangeCount(+1));
            if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
            if (resultText != null) resultText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ExpeditionReturned>(OnReturned);
            EventBus.Subscribe<PreyStockChanged>(OnPreyChanged);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExpeditionReturned>(OnReturned);
            EventBus.Unsubscribe<PreyStockChanged>(OnPreyChanged);
        }

        private void Update()
        {
            // La UI usa tiempo sin escalar (sigue viva en pausa).
            if (resultTimer > 0f)
            {
                resultTimer -= Time.unscaledDeltaTime;
                if (resultTimer <= 0f && resultText != null) resultText.gameObject.SetActive(false);
            }

            if (progressFill != null && Sys != null) progressFill.fillAmount = Sys.Progress01;

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.25f;
            Refresh();
        }

        private void ChangeCount(int delta)
        {
            count = Mathf.Clamp(count + delta, ExpeditionSystem.MinGroup, ExpeditionSystem.MaxGroup);
            Refresh();
        }

        private void OnSendClicked()
        {
            if (Sys != null && Sys.Send(count)) Refresh();
        }

        private void Refresh()
        {
            if (countText != null) countText.text = count.ToString();
            if (minusButton != null) minusButton.interactable = count > ExpeditionSystem.MinGroup;
            if (plusButton != null) plusButton.interactable = count < ExpeditionSystem.MaxGroup;

            if (Sys == null)
            {
                if (sendButton != null) sendButton.interactable = false;
                if (statusText != null) statusText.text = "Falta ExpeditionSystem";
                return;
            }

            bool canSend = Sys.CanSend(count, out var reason);
            if (sendButton != null) sendButton.interactable = canSend;
            if (sendButtonText != null) sendButtonText.text = "Enviar expedición";

            if (statusText != null)
            {
                if (Sys.IsAway) statusText.text = $"Fuera ({Sys.AntsAway.Count}): vuelve en {Mathf.CeilToInt(Sys.TimeLeft)} s";
                else if (!canSend) statusText.text = reason;
                else statusText.text = $"En casa · {Mathf.RoundToInt(Sys.PreyChance(count) * 100f)} % de traer presa";
            }

            var bar = progressBar != null ? progressBar : progressFill != null ? progressFill.gameObject : null;
            if (bar != null && bar.activeSelf != Sys.IsAway) bar.SetActive(Sys.IsAway);
            if (infoText != null) infoText.text = $"Obreras disponibles: {Sys.CountAvailable()} · apuntadas: {Sys.Volunteers.Count}";
            if (preyCountText != null) preyCountText.text = $"Presas: {Sys.PreyCount}";
        }

        private void OnPreyChanged(PreyStockChanged e)
        {
            if (preyCountText != null) preyCountText.text = $"Presas: {e.Count}";
        }

        private void OnReturned(ExpeditionReturned e)
        {
            Refresh();
            if (resultText == null) return;
            resultText.text = e.Prey != null
                ? $"¡La expedición vuelve con una presa: {e.Prey.displayName}!"
                : $"La expedición vuelve con {e.Leaves} hojas";
            resultText.gameObject.SetActive(true);
            resultTimer = resultSeconds;
        }
    }
}
