using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// A5 · Pestañas de la barra inferior del HUD (Guardería, Expedición…).
    /// Cada botón abre su panel y cierra los demás; pulsarlo otra vez lo cierra.
    /// Así solo hay un panel abierto a la vez en el mismo hueco y nada se solapa.
    /// </summary>
    public class HudTabs : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button button;
            public GameObject panel;
        }

        [SerializeField] private List<Tab> tabs = new List<Tab>();
        [Tooltip("Al empezar la partida, todos los paneles cerrados")]
        [SerializeField] private bool startClosed = true;
        [SerializeField] private Color activeColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color normalColor = Color.white;

        private void Awake()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;   // copia para el lambda
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Toggle(index));
            }
        }

        private void Start()
        {
            if (startClosed) CloseAll();
            RefreshButtons();
        }

        /// <summary>Abre la pestaña i (o la cierra si ya estaba abierta).</summary>
        public void Toggle(int i)
        {
            if (i < 0 || i >= tabs.Count || tabs[i].panel == null) return;
            bool open = !tabs[i].panel.activeSelf;
            CloseAll();
            tabs[i].panel.SetActive(open);
            RefreshButtons();
        }

        public void CloseAll()
        {
            foreach (var t in tabs)
                if (t.panel != null) t.panel.SetActive(false);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            foreach (var t in tabs)
            {
                if (t.button == null || t.button.image == null) continue;
                bool open = t.panel != null && t.panel.activeSelf;
                t.button.image.color = open ? activeColor : normalColor;
            }
        }
    }
}
