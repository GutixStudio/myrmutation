using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Myrmutation.Building;
using Myrmutation.Colony;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using Myrmutation.Genetics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Myrmutation.Ants.Testing
{
    /// <summary>
    /// SOLO DEMOSTRACIÓN (Scenes/Test/B_Demo.unity). Panel para enseñar uno a uno los comportamientos de la obrera
    /// sin esperar a que pasen solos: necesidades, rutina, excavación, ociosas, reparto en las salas y castas.
    ///
    /// No da órdenes de movimiento: solo cambia lo que la hormiga PERCIBE (hambre, energía, sala asignada, comida,
    /// planos…) y deja que su cerebro decida, igual que en la partida.
    ///
    /// · Hormiga de la demo: la última que se ha seleccionado tocándola (se mantiene aunque se toque el panel).
    /// · Encima de cada hormiga se puede mostrar su estado y dos barras: hambre (naranja) y energía (azul).
    /// · Atajos de teclado: F1 muestra u oculta el panel · F2 las etiquetas · 1–6 acciones sobre la hormiga.
    ///
    /// El panel se dibuja con OnGUI. Para que tocarlo no seleccione ni deseleccione hormigas, se coloca debajo un
    /// rectángulo invisible del Canvas: CameraController.IsOverUI lo detecta y no lanza Tapped.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // antes que NestGrid.Awake, que construye el mapa inicial
    public class BehaviourDemo : MonoBehaviour
    {
        [Header("Mapa de la demo")]
        [Tooltip("Añade al mapa inicial, solo en esta escena, un Jardín de hongos y una Cámara del Festín ya construidos " +
                 "(debajo de la Guardería y de la Despensa), unidos al pozo por un túnel: así hay una sala de cada tipo")]
        [SerializeField] private bool addDemoRooms = true;
        [SerializeField] private RoomData fungusRoom;
        [SerializeField] private RoomData feastRoom;

        [Header("Generar obreras")]
        [SerializeField] private Ant antPrefab;
        [SerializeField] private CasteData minor;
        [SerializeField] private CasteData media;
        [SerializeField] private CasteData major;

        [Header("Al empezar")]
        [Tooltip("Reparte las obreras entre las salas de trabajo (cada una a la que mejor se le da), para que se vean trabajar solas desde el principio")]
        [SerializeField] private bool assignRoomsOnStart = true;
        [Tooltip("Obreras extra que se crean al empezar (alternando Menor y Mayor) para que haya más variedad")]
        [SerializeField] private int extraWorkersOnStart = 2;
        [Tooltip("Hambre y energía iniciales al azar, para que no coman ni descansen todas a la vez")]
        [SerializeField] private bool randomizeNeedsOnStart = true;

        [Header("Comida")]
        [Tooltip("Comida que añade el botón +Comida")]
        [SerializeField] private int foodStep = 20;
        [Tooltip("Mientras no se quite la comida con el botón, la colonia nunca baja de esta cantidad (0 = desactivado). " +
                 "Así la demo no se para porque se acabe la comida")]
        [SerializeField] private int keepFoodAtLeast = 40;

        [Header("Plano de ejemplo")]
        [Tooltip("Largo (u) del túnel de ejemplo que se planifica desde la puerta izquierda de la sala más a la izquierda")]
        [SerializeField] private float exampleTunnelLength = 4f;
        [SerializeField] private float exampleTunnelDrop = 2.5f;

        [Header("Vista")]
        [SerializeField] private bool showPanel = true;
        [SerializeField] private bool showLabels = true;
        [Tooltip("Texto de estado encima de todas las hormigas (si no, solo de la elegida; las barras salen en todas)")]
        [SerializeField] private bool labelsForAll = false;
        [Tooltip("Alto de referencia para escalar el panel")]
        [SerializeField] private float referenceHeight = 1080f;

        private const float PanelWidth = 470f;

        private Ant target;
        private int savedFood = -1;
        private int spawned;
        private string lastMessage = "Toca una hormiga para elegirla.";
        private Vector2 scroll;
        private float panelHeight = 900f;

        private GUIStyle box, title, label, small, button, tag;
        private Texture2D white;
        private RectTransform blocker;
        private Canvas canvas;

        private static readonly string[] Names =
            { "Brizna", "Polen", "Savia", "Tierra", "Raíz", "Musgo", "Rocío", "Semilla", "Corteza", "Ámbar" };

        // ================= CICLO =================

        private void Awake()
        {
            if (addDemoRooms) ExtendStartingMap();
        }

        /// <summary>
        /// Amplía el mapa inicial de NestGrid ANTES de que lo construya (este script se ejecuta antes, ver
        /// DefaultExecutionOrder). Solo para la demo: así no hay que tocar el prefab del nido ni NestGrid. Los campos son
        /// privados (los rellena el Inspector), por eso se usa reflexión.
        /// </summary>
        private void ExtendStartingMap()
        {
            var grid = FindFirstObjectByType<NestGrid>();
            if (grid == null) return;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var dugField = typeof(NestGrid).GetField("startingDug", flags);
            var roomsField = typeof(NestGrid).GetField("startingRooms", flags);
            if (dugField == null || roomsField == null)
            {
                Debug.LogWarning("[BehaviourDemo] NestGrid ha cambiado: no se pueden añadir las salas de la demo.", this);
                return;
            }

            var dug = new List<RectInt>((RectInt[])dugField.GetValue(grid));
            AddOnce(dug, new RectInt(59, 20, 3, 19));    // el pozo baja hasta la fila 20
            AddOnce(dug, new RectInt(56, 20, 3, 3));     // pozo - Jardín de hongos
            AddOnce(dug, new RectInt(62, 20, 33, 3));    // pozo - Cámara del Festín (por debajo de la Sala Real)
            dugField.SetValue(grid, dug.ToArray());

            var rooms = new List<NestGrid.StartingRoom>((NestGrid.StartingRoom[])roomsField.GetValue(grid));
            AddRoomOnce(rooms, fungusRoom, new Vector2Int(35, 20));   // debajo de la Guardería
            AddRoomOnce(rooms, feastRoom, new Vector2Int(95, 20));    // debajo de la Despensa
            roomsField.SetValue(grid, rooms.ToArray());
        }

        private static void AddOnce(List<RectInt> list, RectInt r) { if (!list.Contains(r)) list.Add(r); }

        private static void AddRoomOnce(List<NestGrid.StartingRoom> list, RoomData data, Vector2Int origin)
        {
            if (data == null) return;
            foreach (var s in list) if (s.origin == origin) return;
            list.Add(new NestGrid.StartingRoom { data = data, origin = origin });
        }

        private void OnEnable() => EventBus.Subscribe<AntSelected>(OnAntSelected);
        private void OnDisable() => EventBus.Unsubscribe<AntSelected>(OnAntSelected);

        private IEnumerator Start()
        {
            CreateBlocker();
            // Las salas iniciales las crea RoomSpawner en su Start: esperar a que existan.
            for (int i = 0; i < 60 && BuiltRooms().Count == 0; i++) yield return null;
            yield return null;

            for (int i = 0; i < extraWorkersOnStart; i++) Spawn(i % 2 == 0 ? minor : major);
            yield return null;   // que las nuevas pasen su Start (Ant.All)

            if (randomizeNeedsOnStart)
                foreach (var a in Workers())
                {
                    var n = a.GetComponent<AntNeeds>();
                    n.SetHungerForTesting(Random.Range(0f, 0.45f));
                    n.SetEnergyForTesting(Random.Range(0.45f, 1f));
                }
            if (assignRoomsOnStart) AssignAllByAffinity();
            target = null;
            lastMessage = "Cada obrera tiene su sala. Toca una para elegirla.";
        }

        /// <summary>
        /// Reparte las obreras entre las salas de trabajo por turnos: en cada vuelta, cada sala se queda con la obrera libre
        /// que mejor rinde en ella (afinidad de su casta). Así todas las salas tienen gente y cada casta va a lo suyo.
        /// </summary>
        private void AssignAllByAffinity()
        {
            var rooms = BuiltRooms();
            if (rooms.Count == 0) return;
            var left = new List<Ant>(Workers());
            while (left.Count > 0)
            {
                foreach (var room in rooms)
                {
                    if (left.Count == 0) break;
                    Ant best = null; float bestAff = -1f;
                    foreach (var a in left)
                    {
                        float aff = a.Caste != null ? a.Caste.GetAffinity(room.Data.type) : 1f;
                        if (aff > bestAff) { bestAff = aff; best = a; }
                    }
                    best.AssignedRoom = room;
                    left.Remove(best);
                }
            }
        }

        private void OnAntSelected(AntSelected e)
        {
            // Solo cambia al elegir otra: tocar el panel o el vacío deselecciona, pero la demo sigue con la misma.
            if (e.Ant != null && !e.Ant.IsQueen) target = e.Ant;
        }

        private void Update()
        {
            if (target != null && !target.IsAlive) target = null;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) showPanel = !showPanel;
                if (kb.f2Key.wasPressedThisFrame) showLabels = !showLabels;
                if (kb.digit1Key.wasPressedThisFrame) SetHunger(0.6f);
                if (kb.digit2Key.wasPressedThisFrame) SetEnergy(0.25f);
                if (kb.digit3Key.wasPressedThisFrame) SetEnergy(0f);
                if (kb.digit4Key.wasPressedThisFrame) SetHunger(1f);
                if (kb.digit5Key.wasPressedThisFrame) Refill();
                if (kb.digit6Key.wasPressedThisFrame) Unassign();
            }
            KeepFood();
            UpdateBlocker();
        }

        private void KeepFood()
        {
            var res = ResourceManager.Instance;
            if (res == null || keepFoodAtLeast <= 0 || savedFood >= 0) return;
            int food = res.Get(ResourceType.Food);
            if (food < keepFoodAtLeast) res.Add(ResourceType.Food, keepFoodAtLeast - food);
        }

        // ================= ACCIONES SOBRE LA HORMIGA =================

        private bool NeedTarget()
        {
            if (target != null) return true;
            lastMessage = "Primero toca una obrera.";
            return false;
        }

        private void SetHunger(float value)
        {
            if (!NeedTarget()) return;
            target.GetComponent<AntNeeds>().SetHungerForTesting(value);
            lastMessage = $"{target.Name}: hambre al {value * 100f:0} %.";
        }

        private void SetEnergy(float value)
        {
            if (!NeedTarget()) return;
            target.GetComponent<AntNeeds>().SetEnergyForTesting(value);
            lastMessage = value <= 0f ? $"{target.Name}: agotada (energía 0)." : $"{target.Name}: energía al {value * 100f:0} %.";
        }

        private void Refill()
        {
            if (!NeedTarget()) return;
            var n = target.GetComponent<AntNeeds>();
            n.SetHungerForTesting(0f);
            n.SetEnergyForTesting(1f);
            lastMessage = $"{target.Name}: saciada y descansada.";
        }

        private void Assign(Room room)
        {
            if (!NeedTarget()) return;
            target.AssignedRoom = room;
            lastMessage = $"{target.Name} → {RoomName(room)}.";
        }

        private void Unassign()
        {
            if (!NeedTarget()) return;
            target.AssignedRoom = null;
            lastMessage = $"{target.Name}: sin sala (excava si hay planos; si no, ociosa).";
        }

        private void ToggleBrain()
        {
            if (!NeedTarget()) return;
            var brain = target.GetComponent<AntBrain>();
            if (brain.IsSuspended) { brain.Resume(); lastMessage = $"{target.Name}: cerebro reanudado."; }
            else { brain.Suspend(); target.GetComponent<AntActuator>().Stop(); lastMessage = $"{target.Name}: cerebro suspendido (como en una expedición)."; }
        }

        // ================= ACCIONES SOBRE LA COLONIA =================

        private void ToggleFood()
        {
            var res = ResourceManager.Instance;
            if (res == null) return;
            if (savedFood < 0)
            {
                savedFood = res.Get(ResourceType.Food);
                res.TrySpend(ResourceType.Food, savedFood);
                lastMessage = "Sin comida (ya no se repone sola): las hambrientas no pueden comer y, al 100 %, mueren en 10 s.";
            }
            else
            {
                res.Add(ResourceType.Food, savedFood);
                lastMessage = $"Comida devuelta (+{savedFood}).";
                savedFood = -1;
            }
        }

        private void AddFood()
        {
            ResourceManager.Instance?.Add(ResourceType.Food, foodStep);
            lastMessage = $"+{foodStep} de comida.";
        }

        private void HungerForAll()
        {
            int n = 0;
            foreach (var a in Workers()) { a.GetComponent<AntNeeds>().SetHungerForTesting(0.6f); n++; }
            lastMessage = $"{n} obreras con hambre: van a la Despensa y se reparten por su suelo.";
        }

        private void UnassignAll()
        {
            int n = 0;
            foreach (var a in Workers()) { a.AssignedRoom = null; n++; }
            lastMessage = $"{n} obreras sin sala.";
        }

        private void Spawn(CasteData caste)
        {
            if (antPrefab == null || caste == null) { lastMessage = "Falta el prefab o la casta en el Inspector."; return; }
            var royal = Room.FindBuilt(RoomType.Royal);
            Vector3 pos = royal != null ? royal.WorkPosition : NestEntrance.Instance != null ? NestEntrance.Instance.Position : Vector3.zero;
            pos += Vector3.right * Random.Range(-1f, 1f);
            pos.z = 0f;

            var ant = Instantiate(antPrefab, pos, Quaternion.identity);
            ant.Caste = caste;
            ant.Stats = caste.baseStats;
            ant.Name = Names[spawned++ % Names.Length] + (spawned > Names.Length ? $" {spawned / Names.Length + 1}" : "");
            if (ant.GetComponent<AntGenome>() == null) ant.gameObject.AddComponent<AntGenome>();
            ant.gameObject.name = $"Ant_{ant.Name}";
            Nursery.ApplyCasteVisual(ant, caste);
            target = ant;
            lastMessage = $"Nueva {caste.displayName}: {ant.Name}.";
        }

        private void PlanExampleTunnel()
        {
            var planner = FindFirstObjectByType<DigPlanner>();
            Room left = null;
            foreach (var r in Room.All)
                if (r != null && r.IsBuilt && (left == null || r.WorkPosition.x < left.WorkPosition.x)) left = r;
            if (planner == null || left == null) { lastMessage = "No hay planificador o salas en la escena."; return; }

            float doorX = left.WorkPosition.x - AntSpread.HalfWidth(left);
            Vector3 a = new Vector3(doorX - 0.3f, left.WorkPosition.y + 0.25f, 0f);
            Vector3 b = a + new Vector3(-exampleTunnelLength, -exampleTunnelDrop, 0f);
            planner.PaintStroke(a, b);
            lastMessage = "Túnel planificado junto a " + RoomName(left) + ": lo excavan las obreras sin sala.";
        }

        // ================= DIBUJO =================

        private void OnGUI()
        {
            EnsureStyles();
            float s = Screen.height / referenceHeight;
            var old = GUI.matrix;

            if (showLabels) DrawLabels();

            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float h = Mathf.Min(panelHeight, Screen.height / s - 140f);
            var area = new Rect(16f, 120f, PanelWidth, showPanel ? h : 54f);
            GUI.Box(area, GUIContent.none, box);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 8f, area.width - 24f, area.height - 16f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Demo · IA de la obrera", title);
            if (GUILayout.Button(showPanel ? "Ocultar" : "Mostrar", button, GUILayout.Width(110f))) showPanel = !showPanel;
            GUILayout.EndHorizontal();

            if (showPanel)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                DrawTarget();
                Section("Necesidades (1–5)");
                Row(("Hambre 60 %", () => SetHunger(0.6f)), ("Hambre 100 %", () => SetHunger(1f)));
                Row(("Energía 25 %", () => SetEnergy(0.25f)), ("Agotarla", () => SetEnergy(0f)));
                Row(("Saciar y descansar", Refill));

                Section("Trabajo");
                var rooms = BuiltRooms();
                for (int i = 0; i < rooms.Count; i += 2)
                {
                    var r1 = rooms[i];
                    if (i + 1 < rooms.Count) { var r2 = rooms[i + 1]; Row(("→ " + RoomName(r1), () => Assign(r1)), ("→ " + RoomName(r2), () => Assign(r2))); }
                    else Row(("→ " + RoomName(r1), () => Assign(r1)));
                }
                Row(("Quitar sala (6)", Unassign), (BrainLabel(), ToggleBrain));

                Section("Colonia");
                Row(("Hambre a todas", HungerForAll), ("Quitar sala a todas", UnassignAll));
                Row((savedFood < 0 ? "Quitar la comida" : "Devolver la comida", ToggleFood), ($"+{foodStep} comida", AddFood));
                Row(("Planificar túnel de ejemplo", PlanExampleTunnel));
                Row(("+ Menor", () => Spawn(minor)), ("+ Media", () => Spawn(media)), ("+ Mayor", () => Spawn(major)));
                Row((showLabels ? "Ocultar etiquetas (F2)" : "Ver etiquetas (F2)", () => showLabels = !showLabels));

                GUILayout.Space(6f);
                GUILayout.Label(lastMessage, small);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint) lastArea = new Rect(area.x * s, area.y * s, area.width * s, area.height * s);
            GUI.matrix = old;
        }

        private Rect lastArea;

        private void DrawTarget()
        {
            if (target == null) { GUILayout.Label("Ninguna obrera elegida: tócala en el nido.", label); return; }
            var needs = target.GetComponent<AntNeeds>();
            var brain = target.GetComponent<AntBrain>();
            var act = target.GetComponent<AntActuator>();
            string caste = target.Caste != null ? target.Caste.displayName : "—";
            GUILayout.Label($"<b>{target.Name}</b> · {caste}", label);
            GUILayout.Label($"Estado: {AntPanelUI.DescribeState(target)}", label);
            GUILayout.Label($"Cerebro: {GoalName(brain.Goal)} / {StepName(brain.Step)}{(brain.IsSuspended ? " (suspendido)" : "")}" +
                            $" · Actuador: {act.Current}", small);
            GUILayout.Label($"Hambre {needs.Hunger01 * 100f:0} % · Energía {needs.Energy01 * 100f:0} %" +
                            (needs.IsExhausted ? " · AGOTADA" : "") + (brain.HasNoPath ? " · SIN CAMINO" : ""), small);
            GUILayout.Label($"Sala: {RoomName(target.AssignedRoom)} · Fuerza {target.Stats.strength:0.#} · Velocidad {target.Stats.speed:0.#}", small);
        }

        private void DrawLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float s = Mathf.Clamp(Screen.height / referenceHeight, 0.6f, 2f);
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive) continue;
                Vector3 sp = cam.WorldToScreenPoint(a.transform.position + Vector3.up * 0.45f);
                if (sp.z < 0f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;
                float w = 150f * s, x = sp.x - w * 0.5f, y = Screen.height - sp.y - 34f * s;

                var needs = a.GetComponent<AntNeeds>();
                if (labelsForAll || a == target)
                {
                    string text = a.IsQueen ? "Reina" : $"{a.Name}: {AntPanelUI.DescribeState(a)}";
                    tag.fontSize = Mathf.RoundToInt(14 * s);
                    var r = new Rect(x - 60f * s, y - 2f * s, w + 120f * s, 20f * s);
                    GUI.color = new Color(1f, 0.95f, 0.6f);
                    GUI.Label(r, text, tag);
                    GUI.color = Color.white;
                }
                if (needs == null) continue;
                Bar(new Rect(x, y + 19f * s, w, 4f * s), needs.Hunger01, new Color(0.95f, 0.55f, 0.15f));
                Bar(new Rect(x, y + 25f * s, w, 4f * s), needs.Energy01, new Color(0.3f, 0.6f, 1f));
            }
        }

        private void Bar(Rect r, float v, Color c)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), white);
            GUI.color = Color.white;
        }

        private void Section(string text) { GUILayout.Space(8f); GUILayout.Label(text, title); }

        private void Row(params (string text, System.Action action)[] buttons)
        {
            GUILayout.BeginHorizontal();
            foreach (var (text, action) in buttons)
                if (GUILayout.Button(text, button)) action();
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (box != null) return;
            white = Texture2D.whiteTexture;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.1f, 0.08f, 0.06f, 0.88f)); bg.Apply();
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.55f) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true, wordWrap = true, normal = { textColor = Color.white } };
            small = new GUIStyle(label) { fontSize = 17, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fixedHeight = 40f, wordWrap = false };
            tag = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            tag.normal.background = null;
        }

        // ================= BLOQUEO DE TOQUES =================

        private void CreateBlocker()
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
            if (canvas == null) return;
            var go = new GameObject("DemoPanelBlocker", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();   // detrás del resto de la interfaz
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            blocker = (RectTransform)go.transform;
            blocker.anchorMin = blocker.anchorMax = Vector2.zero;
            blocker.pivot = Vector2.zero;
        }

        private void UpdateBlocker()
        {
            if (blocker == null || canvas == null) return;
            float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            // OnGUI cuenta Y desde arriba; el Canvas, desde abajo.
            blocker.anchoredPosition = new Vector2(lastArea.x, Screen.height - lastArea.yMax) / k;
            blocker.sizeDelta = new Vector2(lastArea.width, lastArea.height) / k;
        }

        // ================= AUXILIARES =================

        private static IEnumerable<Ant> Workers()
        {
            var list = new List<Ant>(Ant.All);
            foreach (var a in list) if (a != null && a.IsAlive && !a.IsQueen) yield return a;
        }

        private static List<Room> BuiltRooms()
        {
            var list = new List<Room>();
            foreach (var r in Room.All)
                if (r != null && r.IsBuilt && r.Data != null && r.Data.type != RoomType.Royal) list.Add(r);
            list.Sort((a, b) => a.WorkPosition.x.CompareTo(b.WorkPosition.x));
            return list;
        }

        private string BrainLabel() =>
            target != null && target.GetComponent<AntBrain>().IsSuspended ? "Reanudar cerebro" : "Suspender cerebro";

        private static string RoomName(Room r) => r == null ? "ninguna" : r.Data != null ? r.Data.displayName : r.name;

        private static string GoalName(BrainGoal g) => g switch
        {
            BrainGoal.Routine => "Rutina", BrainGoal.Eat => "Comer", BrainGoal.Rest => "Descansar",
            BrainGoal.Dig => "Excavar", BrainGoal.Idle => "Ociosa", _ => g.ToString()
        };

        private static string StepName(BrainStep s) => s switch
        {
            BrainStep.None => "Sin paso", BrainStep.Moving => "Moviéndose", BrainStep.Acting => "Actuando",
            BrainStep.Waiting => "Esperando", _ => s.ToString()
        };
    }
}
