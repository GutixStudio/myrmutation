using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Myrmutation.UI
{
    /// <summary>
    /// E15 · Aplica los tamaños mínimos de E11 a TODA la interfaz que cuelga de este objeto
    /// (ponerlo en el Canvas de cada escena):
    ///   · Textos de al menos 'minFontSize' (32 px en la resolución de referencia 1920×1080).
    ///   · Todo lo que se toca (botones, toggles, desplegables, campos de texto) de al menos
    ///     'minTouchSize' (88 px) de alto.
    ///
    /// Cómo lo hace:
    ///   · Si el elemento está dentro de un Layout Group → le pone una altura mínima con LayoutElement
    ///     y agranda la fila que lo contiene si tenía una altura fija menor.
    ///   · Si está suelto (colocado a mano) → le cambia el alto directamente.
    ///   · Si un texto tiene una altura fija que ya no le cabe → se la agranda.
    ///   · Revisa de nuevo cada 'rescanInterval' segundos: así también cumplen los botones que se
    ///     crean por código (salas, castas, avisos…) y cualquier panel nuevo que se añada.
    ///
    /// No toca barras de desplazamiento ni sliders (no son botones). Para dejar fuera algo concreto,
    /// añádelo a 'exclude' (se excluyen también sus hijos).
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileUISizer : MonoBehaviour
    {
        [Header("Mínimos (px en la resolución de referencia 1920×1080)")]
        [SerializeField] private float minFontSize = 32f;
        [SerializeField] private float minTouchSize = 88f;

        [Header("Opciones")]
        [Tooltip("Si está marcado, solo actúa en navegadores de móvil. Desmarcado = mismos tamaños en todos los dispositivos")]
        [SerializeField] private bool onlyOnMobile = false;
        [Tooltip("Cada cuántos segundos se revisan los elementos nuevos (0 = solo al arrancar)")]
        [SerializeField] private float rescanInterval = 0.5f;
        [Tooltip("Objetos que no se tocan (ni sus hijos)")]
        [SerializeField] private List<RectTransform> exclude = new List<RectTransform>();

        private readonly HashSet<int> done = new HashSet<int>();
        private readonly List<TMP_Text> textBuffer = new List<TMP_Text>();
        private readonly List<Selectable> selectableBuffer = new List<Selectable>();
        private float timer;

        private void Awake()
        {
            if (onlyOnMobile && !Application.isMobilePlatform) { enabled = false; return; }
            Apply();
        }

        private void Update()
        {
            if (rescanInterval <= 0f) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = rescanInterval;
            Apply();
        }

        /// <summary>Aplica los mínimos a todo lo que aún no se ha revisado.</summary>
        public void Apply()
        {
            GetComponentsInChildren(true, textBuffer);
            foreach (var t in textBuffer)
            {
                if (t == null || !done.Add(t.GetInstanceID()) || IsExcluded(t.transform)) continue;
                FixText(t);
            }

            GetComponentsInChildren(true, selectableBuffer);
            foreach (var s in selectableBuffer)
            {
                if (s == null || !done.Add(s.GetInstanceID()) || IsExcluded(s.transform)) continue;
                if (s is Scrollbar || s is Slider) continue;
                FixTouchHeight((RectTransform)s.transform);
            }
        }

        // ================= TEXTOS =================

        private void FixText(TMP_Text t)
        {
            if (t.enableAutoSizing) t.fontSizeMin = Mathf.Max(t.fontSizeMin, minFontSize);
            if (t.fontSize < minFontSize) t.fontSize = minFontSize;

            // Si el texto tiene una altura fija pensada para una letra más pequeña, se agranda.
            var le = t.GetComponent<LayoutElement>();
            if (le == null) return;
            float needed = t.fontSize * 1.25f;
            if (le.preferredHeight > 0f && le.preferredHeight < needed) le.preferredHeight = needed;
            if (le.minHeight > 0f && le.minHeight < needed) le.minHeight = needed;
        }

        // ================= ELEMENTOS TÁCTILES =================

        private void FixTouchHeight(RectTransform rt)
        {
            var parent = rt.parent as RectTransform;
            var ownLe = rt.GetComponent<LayoutElement>();
            bool inLayout = parent != null && parent.GetComponent<LayoutGroup>() != null &&
                            (ownLe == null || !ownLe.ignoreLayout);

            if (inLayout)
            {
                if (ownLe == null) ownLe = rt.gameObject.AddComponent<LayoutElement>();
                ownLe.minHeight = Mathf.Max(ownLe.minHeight, minTouchSize);
                if (ownLe.preferredHeight > 0f && ownLe.preferredHeight < minTouchSize) ownLe.preferredHeight = minTouchSize;

                // La fila que lo contiene no puede ser más baja que el botón.
                var parentLe = parent.GetComponent<LayoutElement>();
                if (parentLe != null)
                {
                    if (parentLe.preferredHeight > 0f && parentLe.preferredHeight < minTouchSize) parentLe.preferredHeight = minTouchSize;
                    if (parentLe.minHeight > 0f && parentLe.minHeight < minTouchSize) parentLe.minHeight = minTouchSize;
                }
                return;
            }

            // Suelto: solo si su alto es fijo (anclas sin estirar en vertical).
            if (!Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y)) return;
            if (rt.sizeDelta.y < minTouchSize) rt.sizeDelta = new Vector2(rt.sizeDelta.x, minTouchSize);
        }

        private bool IsExcluded(Transform t)
        {
            if (exclude.Count == 0) return false;
            foreach (var e in exclude)
                if (e != null && (t == e || t.IsChildOf(e))) return true;
            return false;
        }
    }
}
