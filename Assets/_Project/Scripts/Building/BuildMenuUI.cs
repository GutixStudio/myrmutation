using System;
using System.Collections.Generic;
using Myrmutation.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.Building
{
    /// <summary>
    /// Menú de construcción: un panel con un botón por sala. Los botones se generan a partir de un botón plantilla.
    /// </summary>
    public class BuildMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [Tooltip("Contenedor de los botones (con Vertical Layout Group)")]
        [SerializeField] private Transform buttonContainer;
        [Tooltip("Botón plantilla (desactivado) que se duplica por cada sala")]
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Button closeButton;

        private readonly List<Button> spawned = new List<Button>();

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open(BuildZone zone, IReadOnlyList<RoomData> options, Action<RoomData> onPick)
        {
            Clear();
            if (title != null) title.text = $"Construir en {zone.ZoneId} · {StratumName(zone.Stratum)}";

            foreach (var opt in options)
            {
                if (opt == null) continue;
                var b = Instantiate(buttonTemplate, buttonContainer);
                b.gameObject.SetActive(true);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    float cost = BuildManager.Instance != null ? BuildManager.Instance.CostFor(opt) : opt.workCost;
                    label.text = $"{opt.displayName}\n<size=70%>{opt.minWorkers} obreras · {Mathf.RoundToInt(cost)} de trabajo</size>";
                }
                var picked = opt;
                b.onClick.AddListener(() => { Close(); onPick(picked); });
                spawned.Add(b);
            }
            if (panel != null) panel.SetActive(true);
        }

        public void Close()
        {
            Clear();
            if (panel != null) panel.SetActive(false);
        }

        private void Clear()
        {
            foreach (var b in spawned) if (b != null) Destroy(b.gameObject);
            spawned.Clear();
        }

        private static string StratumName(Stratum s)
        {
            if (s == Stratum.Clay) return "arcilla";
            if (s == Stratum.Rock) return "roca";
            return "tierra";
        }
    }
}
