#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Myrmutation.Building.EditorTools
{
    /// <summary>
    /// Menú Myrmutation → Crear modo Construcción en la escena (A9; también para Game.unity en A12).
    /// Monta en la escena abierta lo que falte: GameManager, BlueprintOverlay (sprites de Art/Tiles/Placeholder/Blueprint y
    /// BlueprintGrid.mat y BlueprintSilhouette.mat, que se crean si faltan; se reasignan aunque ya exista), BuildMode
    /// (BuildModeController), Canvas + EventSystem, la UI con BuildModeUI enlazada, la lupa (BuildMagnifier), las grietas
    /// de excavación (DigCracks) y los terrones (DigChunks, enlazados al NestRenderer). No duplica lo que ya existe.
    /// Si hay HUD, sus botones de velocidad van a lockedWhileOpen. Todo con Undo; la escena queda modificada.
    /// </summary>
    public static class BuildModeSetupMenu
    {
        private const string PiecesFolder = "Assets/_Project/Art/Tiles/Placeholder/Blueprint";
        private const string ShadersFolder = "Assets/_Project/Shaders";
        private const string Tag = "[A9]";

        // Glifos que tiene LiberationSans (la fuente por defecto de TMP): ► √ × en vez de ▶ ✓ ✕.
        private static readonly Color PanelColor = new Color(0.10f, 0.15f, 0.26f, 0.92f);
        private static readonly Color ButtonColor = Color.white;   // BuildModeUI tiñe los botones (normal / activo)
        private static readonly Color ButtonTextColor = new Color(0.12f, 0.12f, 0.16f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        private static Sprite uiSprite;
        private static Sprite bgSprite;

        [MenuItem("Myrmutation/Crear modo Construcción en la escena")]
        public static void Setup()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Undo.SetCurrentGroupName("Crear modo Construcción");
            int group = Undo.GetCurrentGroup();

            if (Object.FindFirstObjectByType<GameManager>() == null)
                NewObject("GameManager").AddComponent<GameManager>();

            // Los sprites se asignan también a un overlay que ya exista (para volver a pasar el menú al añadir arte).
            var overlay = Object.FindFirstObjectByType<BlueprintOverlay>();
            if (overlay == null) overlay = NewObject("BlueprintOverlay").AddComponent<BlueprintOverlay>();
            var overlaySo = new SerializedObject(overlay);
            SetArray(overlaySo, "fillPieces", LoadPieces("_bp"));
            SetArray(overlaySo, "linePieces", LoadPieces("_line"));
            Set(overlaySo, "noiseSprite", LoadSprite("bp_paper_noise"));
            Set(overlaySo, "gridMaterial", ShaderMaterial("BlueprintGrid"));
            Set(overlaySo, "silhouetteMaterial", ShaderMaterial("BlueprintSilhouette"));
            overlaySo.ApplyModifiedProperties();

            var controller = Object.FindFirstObjectByType<BuildModeController>();
            if (controller == null)
            {
                controller = NewObject("BuildMode").AddComponent<BuildModeController>();
                var so = new SerializedObject(controller);
                Set(so, "overlay", overlay);
                so.ApplyModifiedProperties();
            }

            if (Object.FindFirstObjectByType<BuildModeUI>() == null) BuildUI(EnsureCanvas(), controller);
            else Debug.Log($"{Tag} Ya hay BuildModeUI en la escena: no se crea otra.");

            // La lupa también se añade a escenas montadas antes de que existiera.
            var controllerSo = new SerializedObject(controller);
            if (controllerSo.FindProperty("magnifier").objectReferenceValue == null)
            {
                var magnifier = NewUI("BuildMagnifier", EnsureCanvas().transform).AddComponent<BuildMagnifier>();
                Undo.RegisterCreatedObjectUndo(magnifier.gameObject, "Crear BuildMagnifier");
                Set(controllerSo, "magnifier", magnifier);
                controllerSo.ApplyModifiedProperties();
            }

            // Grietas de excavación (A8), ocultas con el modo abierto.
            if (Object.FindFirstObjectByType<DigCracks>() == null)
            {
                var cracksSo = new SerializedObject(NewObject("DigCracks").AddComponent<DigCracks>());
                Set(cracksSo, "buildMode", controller);
                cracksSo.ApplyModifiedProperties();
            }

            // Terrones al excavar (A8), con el color del terreno.
            if (Object.FindFirstObjectByType<DigChunks>() == null)
            {
                var chunksSo = new SerializedObject(NewObject("DigChunks").AddComponent<DigChunks>());
                Set(chunksSo, "buildMode", controller);
                Set(chunksSo, "nestRenderer", Object.FindFirstObjectByType<NestRenderer>());
                chunksSo.ApplyModifiedProperties();
            }

            // Barras del HUD que asoman detrás de la barra del modo (también en escenas montadas antes).
            var uiSo = new SerializedObject(Object.FindFirstObjectByType<BuildModeUI>());
            if (uiSo.FindProperty("hiddenWhileOpen").arraySize == 0)
            {
                SetArray(uiSo, "hiddenWhileOpen", new[] { "BottomBar", "SpeedBar" }.Select(FindInScene).Where(o => o != null).ToArray<Object>());
                uiSo.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"{Tag} Modo Construcción montado. Guarda la escena.");
        }

        // ================= UI =================

        private static void BuildUI(Canvas canvas, BuildModeController controller)
        {
            var root = NewUI("BuildModeUI", canvas.transform);
            Undo.RegisterCreatedObjectUndo(root, "Crear BuildModeUI");
            Stretch((RectTransform)root.transform, 0f);

            // Botón Construir, abajo a la derecha.
            var build = NewButton("BuildButton", root.transform, "Construir", 34);
            Place(build.gameObject, new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(280f, 110f));

            // Barra del modo: línea de estado encima de los botones, a lo ancho de la pantalla.
            var toolbar = NewUI("Toolbar", root.transform);
            var tbRt = (RectTransform)toolbar.transform;
            tbRt.anchorMin = Vector2.zero;
            tbRt.anchorMax = new Vector2(1f, 0f);
            tbRt.pivot = new Vector2(0.5f, 0f);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = new Vector2(0f, 180f);
            AddImage(toolbar, bgSprite, PanelColor);
            var tbLayout = Vertical(toolbar, 8f, new RectOffset(20, 20, 10, 14));
            tbLayout.childForceExpandHeight = false;

            var status = NewText("StatusText", toolbar.transform, "Planos: 0 bloques · Obreras libres: 0", 28, TextColor);
            Height(status.gameObject, 44);

            var row = NewUI("Buttons", toolbar.transform);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            Height(row, 100);
            var tunnel = ToolButton("TunnelButton", row, "Túnel", 220);
            var rooms = ToolButton("RoomButton", row, "Salas", 220);
            var erase = ToolButton("EraseButton", row, "Borrar", 220);
            var resume = ToolButton("ResumeButton", row, "►", 140);
            var confirm = ToolButton("ConfirmButton", row, "√ Aceptar", 260);
            var cancel = ToolButton("CancelButton", row, "× Cancelar", 260);

            // Lista de salas, encima de la barra a la izquierda.
            var list = NewUI("RoomList", root.transform);
            Place(list, Vector2.zero, new Vector2(20f, 200f), new Vector2(520f, 0f));
            AddImage(list, bgSprite, PanelColor);
            Vertical(list, 8f, new RectOffset(12, 12, 12, 12)).childForceExpandHeight = false;
            list.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var template = NewButton("RoomCard", list.transform, "Sala\n<size=70%>30 de trabajo · 4 obreras</size>", 28);
            Height(template.gameObject, 96);
            var cardLayout = template.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardLayout.padding = new RectOffset(12, 12, 8, 8);
            cardLayout.spacing = 12f;
            cardLayout.childAlignment = TextAnchor.MiddleLeft;
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = false;
            var icon = NewUI("Icon", template.transform);
            icon.transform.SetAsFirstSibling();
            AddImage(icon, null, Color.white).preserveAspect = true;
            Width(icon, 72);
            var cardText = template.GetComponentInChildren<TextMeshProUGUI>();
            cardText.alignment = TextAlignmentOptions.Left;
            cardText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // ✓ / ✕ del fantasma en táctil, encima de la barra a la derecha.
            var ghost = NewUI("GhostButtons", root.transform);
            Place(ghost, new Vector2(1f, 0f), new Vector2(-20f, 200f), new Vector2(300f, 110f));
            var ghostRow = ghost.AddComponent<HorizontalLayoutGroup>();
            ghostRow.spacing = 20f;
            ghostRow.childControlWidth = ghostRow.childControlHeight = true;
            var ghostOk = NewButton("GhostConfirmButton", ghost.transform, "√", 52);
            var ghostCancel = NewButton("GhostCancelButton", ghost.transform, "×", 52);

            var ui = root.AddComponent<BuildModeUI>();
            var so = new SerializedObject(ui);
            Set(so, "controller", controller);
            Set(so, "buildButton", build);
            SetArray(so, "lockedWhileOpen", HudSpeedButtons());
            Set(so, "toolbar", toolbar);
            Set(so, "tunnelButton", tunnel);
            Set(so, "roomButton", rooms);
            Set(so, "eraseButton", erase);
            Set(so, "resumeButton", resume);
            Set(so, "confirmButton", confirm);
            Set(so, "cancelButton", cancel);
            Set(so, "statusText", status);
            Set(so, "roomList", list);
            Set(so, "roomContainer", list.transform);
            Set(so, "roomTemplate", template);
            SetArray(so, "rooms", BuildableRooms().ToArray<Object>());
            Set(so, "ghostButtons", ghost);
            Set(so, "ghostConfirmButton", ghostOk);
            Set(so, "ghostCancelButton", ghostCancel);
            so.ApplyModifiedProperties();
            Selection.activeGameObject = root;
        }

        /// <summary>Salas del proyecto que se pueden construir (RoomData.buildable), por nombre de asset.</summary>
        public static RoomData[] BuildableRooms() => AssetDatabase.FindAssets("t:RoomData")
            .Select(g => AssetDatabase.LoadAssetAtPath<RoomData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(r => r.buildable).OrderBy(r => r.name).ToArray();

        private static Button ToolButton(string name, GameObject row, string label, float width)
        {
            var b = NewButton(name, row.transform, label, 34);
            Width(b.gameObject, width);
            return b;
        }

        // Botones de pausa y velocidad del HUD (si la escena tiene HUD, como Game.unity).
        private static Object[] HudSpeedButtons()
        {
            var hud = Object.FindFirstObjectByType<HUD>();
            if (hud == null) return new Object[0];
            var so = new SerializedObject(hud);
            return new[] { "pauseButton", "speed1Button", "speed2Button", "speed3Button" }
                .Select(f => so.FindProperty(f)?.objectReferenceValue).Where(o => o != null).ToArray();
        }

        private static Canvas EnsureCanvas()
        {
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
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Crear EventSystem");
            }
            return canvas;
        }

        // Las 15 piezas ms_XXXX<suffix> de la carpeta del plano.
        private static Object[] LoadPieces(string suffix)
        {
            var pieces = BlueprintSprites()
                .Where(s => s.name.StartsWith("ms_") && s.name.EndsWith(suffix))
                .OrderBy(s => s.name).ToArray<Object>();
            if (pieces.Length != 15) Debug.LogWarning($"{Tag} Se esperaban 15 piezas ms_XXXX{suffix} en {PiecesFolder} y hay {pieces.Length}");
            return pieces;
        }

        // Objeto de la escena por nombre, aunque esté desactivado; avisa si no está.
        private static GameObject FindInScene(string name)
        {
            var found = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
            if (found == null) Debug.LogWarning($"{Tag} No está '{name}' en la escena: asígnalo a mano en BuildModeUI → hiddenWhileOpen");
            return found != null ? found.gameObject : null;
        }

        // Shaders/<name>.mat con el shader Shaders/<name>.shader; se crea si falta. Al referenciarlo la escena, el shader entra en la build.
        private static Material ShaderMaterial(string name)
        {
            string materialPath = $"{ShadersFolder}/{name}.mat", shaderPath = $"{ShadersFolder}/{name}.shader";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) { Debug.LogWarning($"{Tag} No está el shader {shaderPath}"); return null; }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = BlueprintSprites().FirstOrDefault(s => s.name == name);
            if (sprite == null) Debug.LogWarning($"{Tag} No está el sprite {name} en {PiecesFolder}");
            return sprite;
        }

        private static IEnumerable<Sprite> BlueprintSprites() =>
            AssetDatabase.FindAssets("t:Sprite", new[] { PiecesFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null);

        // ================= UTILIDADES =================

        private static GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
            return go;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
        {
            var t = NewUI(name, parent).AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label, float size)
        {
            var go = NewUI(name, parent);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = AddImage(go, uiSprite, ButtonColor);
            var text = NewText("Text (TMP)", go.transform, label, size, ButtonTextColor);
            Stretch((RectTransform)text.transform, 4f);
            return btn;
        }

        private static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            return vlg;
        }

        // Anclado a una esquina (anchor = pivot), con posición y tamaño fijos.
        private static void Place(GameObject go, Vector2 corner, Vector2 position, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = corner;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
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
            if (p == null) { Debug.LogWarning($"{Tag} {so.targetObject.GetType().Name} no tiene el campo '{field}'"); return; }
            p.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject so, string field, Object[] values)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"{Tag} {so.targetObject.GetType().Name} no tiene el campo '{field}'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
