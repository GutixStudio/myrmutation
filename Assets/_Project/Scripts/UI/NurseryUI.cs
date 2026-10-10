using System.Text;
using Myrmutation.Colony;
using Myrmutation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// A5 · Barra de la Guardería en el HUD: muestra las crías y deja elegir la casta de la larva
    /// que lleva más tiempo esperando (Menor / Media / Mayor). Sin esto, en Game nadie alimenta
    /// a las larvas (BroodTester es solo de pruebas).
    /// Los botones solo se activan si hay una larva esperando y comida suficiente.
    /// </summary>
    public class NurseryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        [Header("Botones de casta (texto: se rellena solo con el coste)")]
        [SerializeField] private Button minorButton;
        [SerializeField] private Button mediaButton;
        [SerializeField] private Button majorButton;

        [Tooltip("Objeto que parpadea/se muestra cuando hay una larva esperando (opcional)")]
        [SerializeField] private GameObject waitingHighlight;

        private float refreshTimer;

        private void Awake()
        {
            if (minorButton != null) minorButton.onClick.AddListener(() => FeedOldest(CasteType.Minor));
            if (mediaButton != null) mediaButton.onClick.AddListener(() => FeedOldest(CasteType.Media));
            if (majorButton != null) majorButton.onClick.AddListener(() => FeedOldest(CasteType.Major));
        }

        private void Start()
        {
            SetLabel(minorButton, CasteType.Minor, "Menor");
            SetLabel(mediaButton, CasteType.Media, "Media");
            SetLabel(majorButton, CasteType.Major, "Mayor");
            Refresh();
        }

        private void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.25f;
            Refresh();
        }

        // ---------- Acciones ----------
        private void FeedOldest(CasteType type)
        {
            var nursery = Nursery.Instance;
            if (nursery == null) return;
            var larva = OldestWaiting(nursery);
            var caste = nursery.GetCaste(type);
            if (larva != null && caste != null) nursery.Feed(larva, caste);
            Refresh();
        }

        private static Brood OldestWaiting(Nursery nursery)
        {
            Brood best = null;
            foreach (var b in nursery.AllBrood)
                if (b.NeedsFood && (best == null || b.WaitingFood > best.WaitingFood)) best = b;
            return best;
        }

        // ---------- Visual ----------
        private void Refresh()
        {
            var nursery = Nursery.Instance;
            if (nursery == null)
            {
                if (statusText != null) statusText.text = "Sin Guardería";
                SetInteractable(false, null);
                return;
            }

            int eggs = 0, larvae = 0, waiting = 0, pupae = 0;
            foreach (var b in nursery.AllBrood)
            {
                switch (b.Stage)
                {
                    case BroodStage.Egg: eggs++; break;
                    case BroodStage.Larva: larvae++; if (b.NeedsFood) waiting++; break;
                    case BroodStage.Pupa: pupae++; break;
                }
            }

            if (statusText != null)
            {
                var sb = new StringBuilder();
                sb.Append($"Guardería {nursery.AllBrood.Count}/{nursery.Capacity}  ·  ");
                sb.Append($"Huevos {eggs} · Larvas {larvae} · Pupas {pupae}");
                if (waiting > 0) sb.Append($"\n<color=#FFD24A>¡{waiting} larva(s) esperan comida! Elige casta:</color>");
                statusText.text = sb.ToString();
            }

            if (waitingHighlight != null) waitingHighlight.SetActive(waiting > 0);
            SetInteractable(waiting > 0, nursery);
        }

        private void SetInteractable(bool anyWaiting, Nursery nursery)
        {
            Set(minorButton, CasteType.Minor);
            Set(mediaButton, CasteType.Media);
            Set(majorButton, CasteType.Major);

            void Set(Button b, CasteType type)
            {
                if (b == null) return;
                b.interactable = anyWaiting && nursery != null && nursery.CanAfford(nursery.GetCaste(type));
            }
        }

        private static void SetLabel(Button b, CasteType type, string label)
        {
            if (b == null) return;
            var text = b.GetComponentInChildren<TMP_Text>();
            var caste = Nursery.Instance != null ? Nursery.Instance.GetCaste(type) : null;
            if (text != null) text.text = caste != null ? $"{label} ({caste.foodCostToRaise})" : label;
        }
    }
}
