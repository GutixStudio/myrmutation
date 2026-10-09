#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Myrmutation.Expedition.EditorTools
{
    /// <summary>
    /// Menú Myrmutation → D4 → Crear prefab ExpeditionPanel.
    /// Genera Assets/_Project/Prefabs/Expedition/ExpeditionPanel.prefab con toda la UI montada y
    /// enlazada a ExpeditionUI. Si la escena abierta NO es Game, además lo coloca en su Canvas para probar.
    /// Se puede volver a ejecutar: sobrescribe el prefab.
    /// </summary>
    public static class ExpeditionPanelBuilder
    {
        private const string Folder = "Assets/_Project/Prefabs/Expedition";
        private const string PrefabPath = Folder + "/ExpeditionPanel.prefab";

        private static readonly Color PanelColor = new Color(0.10f, 0.15f, 0.26f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.27f, 0.40f, 1f);
        private static readonly Color SendColor = new Color(0.85f, 0.55f, 0.25f, 1f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color BarFillColor = new Color(0.95f, 0.80f, 0.30f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color ResultColor = new Color(0.60f, 1f, 0.60f, 1f);

        private static Sprite uiSprite;
        private static Sprite bgSprite;

        [MenuItem("Myrmutation/D4/Crear prefab ExpeditionPanel")]
        public static void Build()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

            // ---------- Panel raíz (abajo a la izquierda, alto automático) ----------
            var root = NewUI("ExpeditionPanel", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(20f, 20f);
            rt.sizeDelta = new Vector2(320f, 0f);
            AddImage(root, bgSprite, PanelColor, Image.Type.Sliced);

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---------- Contenido ----------
            var title = NewText("Title", root.transform, "EXPEDICIÓN", 22, TextAlignmentOptions.Left, FontStyles.Bold);
            Height(title.gameObject, 28);

            var info = NewText("InfoText", root.transform, "Obreras disponibles: 0 · apuntadas: 0", 16, TextAlignmentOptions.Left);
            Height(info.gameObject, 22);

            var status = NewText("StatusText", root.transform, "En casa", 16, TextAlignmentOptions.Left);
            Height(status.gameObject, 22);

            var preyCount = NewText("PreyCountText", root.transform, "Presas: 0", 16, TextAlignmentOptions.Left);
            Height(preyCount.gameObject, 22);

            // Fila − 2 +
            var row = NewUI("GroupRow", root.transform);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            Height(row, 34);

            var minus = NewButton("MinusButton", row.transform, "−", ButtonColor, out _);
            Width(minus.gameObject, 60);
            var count = NewText("CountText", row.transform, "2", 20, TextAlignmentOptions.Center, FontStyles.Bold);
            count.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var plus = NewButton("PlusButton", row.transform, "+", ButtonColor, out _);
            Width(plus.gameObject, 60);

            var send = NewButton("SendButton", root.transform, "Enviar expedición", SendColor, out var sendLabel);
            sendLabel.fontStyle = FontStyles.Bold;
            sendLabel.color = new Color(0.12f, 0.08f, 0.04f, 1f);
            Height(send.gameObject, 40);

            // Barra de progreso (fondo + relleno)
            var bar = NewUI("ProgressBar", root.transform);
            AddImage(bar, uiSprite, BarBackColor, Image.Type.Sliced);
            Height(bar, 12);
            var fillGo = NewUI("ProgressFill", bar.transform);
            Stretch((RectTransform)fillGo.transform, 2f);
            var fill = AddImage(fillGo, uiSprite, BarFillColor, Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;

            var result = NewText("ResultText", root.transform, "La expedición vuelve con 8 hojas", 17, TextAlignmentOptions.Center, FontStyles.Bold);
            result.color = ResultColor;
            Height(result.gameObject, 44);

            // ---------- Script y referencias ----------
            var ui = root.AddComponent<ExpeditionUI>();
            var so = new SerializedObject(ui);
            Set(so, "minusButton", minus);
            Set(so, "plusButton", plus);
            Set(so, "countText", count);
            Set(so, "sendButton", send);
            Set(so, "sendButtonText", sendLabel);
            Set(so, "statusText", status);
            Set(so, "infoText", info);
            Set(so, "progressFill", fill);
            Set(so, "progressBar", bar);
            Set(so, "preyCountText", preyCount);
            Set(so, "resultText", result);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Guardar ----------
            EnsureFolder(Folder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null) { Debug.LogError("[D4] No se pudo guardar el prefab"); return; }
            Debug.Log($"[D4] Prefab creado: {PrefabPath}", prefab);

            PlaceInOpenScene(prefab);
        }

        // ================= ESCENA DE PRUEBA =================

        private static void PlaceInOpenScene(GameObject prefab)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "Game")
            {
                Debug.Log("[D4] Escena Game abierta: no se coloca (solo A edita Game.unity). Arrastra el prefab a mano.");
                Selection.activeObject = prefab;
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                cgo.layer = LayerMask.NameToLayer("UI");
                canvas = cgo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                Undo.RegisterCreatedObjectUndo(cgo, "Crear Canvas");
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Crear EventSystem");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Colocar ExpeditionPanel");
            Selection.activeGameObject = instance;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[D4] ExpeditionPanel colocado en el Canvas de {scene.name}. Guarda la escena.", instance);
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

            text = NewText("Text (TMP)", go.transform, label, 18, TextAlignmentOptions.Center);
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
            if (p == null) { Debug.LogWarning($"[D4] ExpeditionUI no tiene el campo '{field}'"); return; }
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
