#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Myrmutation.Genetics.EditorTools
{
    /// <summary>
    /// Menú Myrmutation → C9 → Crear prefab FeastPanel.
    /// Genera Assets/_Project/Prefabs/Genetics/FeastPanel.prefab con toda la UI montada y enlazada a
    /// FeastMenuUI, más SpeciesRegistry. Va ABAJO A LA DERECHA (el panel de expedición está abajo a la izquierda).
    /// Si la escena abierta NO es Game, además lo coloca en su Canvas para probar.
    /// Se puede volver a ejecutar: sobrescribe el prefab. Mismos tamaños de móvil que D4/E11
    /// (texto ≥ 32 y botones ≥ 88 px en 1920×1080).
    /// </summary>
    public static class FeastPanelBuilder
    {
        private const string Folder = "Assets/_Project/Prefabs/Genetics";
        private const string PrefabPath = Folder + "/FeastPanel.prefab";

        private static readonly Color PanelColor = new Color(0.10f, 0.15f, 0.26f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.27f, 0.40f, 1f);
        private static readonly Color ConfirmColor = new Color(0.85f, 0.55f, 0.25f, 1f);
        private static readonly Color ListBackColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color ResultColor = new Color(0.60f, 1f, 0.60f, 1f);

        private static Sprite uiSprite;
        private static Sprite bgSprite;

        [MenuItem("Myrmutation/C9/Crear prefab FeastPanel")]
        public static void Build()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

            // ---------- Raíz: ocupa todo el Canvas (sin Graphic, no bloquea toques) ----------
            var root = NewUI("FeastPanel", null);
            Stretch((RectTransform)root.transform, 0f);

            // ---------- Panel visible (abajo a la derecha, alto automático) ----------
            var panel = NewUI("Panel", root.transform);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-20f, 20f);
            prt.sizeDelta = new Vector2(600f, 0f);
            AddImage(panel, bgSprite, PanelColor, Image.Type.Sliced);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 16, 20);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---------- Contenido ----------
            var title = NewText("Title", panel.transform, "FESTÍN", 36, TextAlignmentOptions.Left, FontStyles.Bold);
            Height(title.gameObject, 48);

            var step = NewText("StepText", panel.transform, "1/3 · Elige una presa", 32, TextAlignmentOptions.Left);
            Height(step.gameObject, 42);

            // Lista con scroll
            var scroll = NewUI("Scroll", panel.transform);
            AddImage(scroll, uiSprite, ListBackColor, Image.Type.Sliced);
            Height(scroll, 360);
            var scrollRect = scroll.AddComponent<ScrollRect>();

            var viewport = NewUI("Viewport", scroll.transform);
            Stretch((RectTransform)viewport.transform, 6f);
            viewport.AddComponent<RectMask2D>();

            var content = NewUI("Content", viewport.transform);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            var cvlg = content.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 8f;
            cvlg.childAlignment = TextAnchor.UpperCenter;
            cvlg.childControlWidth = cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = (RectTransform)viewport.transform;
            scrollRect.content = crt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 40f;

            // Resumen (paso 3)
            var summary = NewText("SummaryText", panel.transform, "Hormiga cortadora (Gen: ?)\ndevorada por Lía", 32, TextAlignmentOptions.Left);
            Height(summary.gameObject, 96);

            // Fila de botones: Cerrar/Atrás · Devorar
            var buttons = NewUI("ButtonRow", panel.transform);
            var hlg = buttons.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            Height(buttons, 88);

            var back = NewButton("BackButton", buttons.transform, "Cerrar", ButtonColor, out var backLabel);
            var confirm = NewButton("ConfirmButton", buttons.transform, "Devorar", ConfirmColor, out var confirmLabel);
            confirmLabel.fontStyle = FontStyles.Bold;
            confirmLabel.color = new Color(0.12f, 0.08f, 0.04f, 1f);

            // Resultado
            var result = NewText("ResultText", panel.transform, "¡Descubierto! Hormiga cortadora → Mandíbula cortadora", 32, TextAlignmentOptions.Center, FontStyles.Bold);
            result.color = ResultColor;
            Height(result.gameObject, 84);

            // Fila plantilla (desactivada): se duplica por cada presa u hormiga
            var rowTemplate = BuildRowTemplate(panel.transform);

            // ---------- Scripts y referencias ----------
            var ui = root.AddComponent<FeastMenuUI>();
            root.AddComponent<SpeciesRegistry>();

            var so = new SerializedObject(ui);
            Set(so, "panel", panel);
            Set(so, "stepText", step);
            Set(so, "summaryText", summary);
            Set(so, "resultText", result);
            Set(so, "listContainer", content.transform);
            Set(so, "rowTemplate", rowTemplate);
            Set(so, "backButton", back);
            Set(so, "backButtonText", backLabel);
            Set(so, "confirmButton", confirm);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Guardar ----------
            EnsureFolder(Folder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null) { Debug.LogError("[C9] No se pudo guardar el prefab"); return; }
            Debug.Log($"[C9] Prefab creado: {PrefabPath}", prefab);

            PlaceInOpenScene(prefab);
        }

        private static Button BuildRowTemplate(Transform parent)
        {
            var row = NewUI("RowTemplate", parent);
            var img = AddImage(row, uiSprite, ButtonColor, Image.Type.Sliced);
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 8, 8);
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            Height(row, 96);

            var species = NewUI("SpeciesIcon", row.transform);
            var si = AddImage(species, null, Color.white, Image.Type.Simple);
            si.preserveAspect = true;
            si.raycastTarget = false;
            Width(species, 72);
            Height(species, 72);

            var label = NewText("Label", row.transform, "Hormiga cortadora\n<size=80%>Gen: ?</size>", 32, TextAlignmentOptions.Left);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24;
            label.fontSizeMax = 32;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var gene = NewUI("GeneIcon", row.transform);
            var gi = AddImage(gene, null, Color.white, Image.Type.Simple);
            gi.preserveAspect = true;
            gi.raycastTarget = false;
            Width(gene, 72);
            Height(gene, 72);

            row.SetActive(false);
            return btn;
        }

        // ================= ESCENA DE PRUEBA =================

        private static void PlaceInOpenScene(GameObject prefab)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "Game")
            {
                Debug.Log("[C9] Escena Game abierta: no se coloca (solo A edita Game.unity). Arrastra el prefab a mano.");
                Selection.activeObject = prefab;
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[C9] No hay Canvas en la escena abierta: arrastra el prefab a un Canvas a mano.");
                Selection.activeObject = prefab;
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Colocar FeastPanel");
            Selection.activeGameObject = instance;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[C9] FeastPanel colocado en el Canvas de {scene.name}. Guarda la escena.", instance);
        }

        // ================= UTILIDADES =================

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color, Image.Type type)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
                                               TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = NewUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = TextColor;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label, Color color, out TextMeshProUGUI text)
        {
            var go = NewUI(name, parent);
            var img = AddImage(go, uiSprite, color, Image.Type.Sliced);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            text = NewText("Text (TMP)", go.transform, label, 32, TextAlignmentOptions.Center);
            Stretch((RectTransform)text.transform, 0f);
            return btn;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Height(GameObject go, float h)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
        }

        private static void Width(GameObject go, float w)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[C9] FeastMenuUI no tiene el campo '{field}'"); return; }
            p.objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif