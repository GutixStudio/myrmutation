using System.Linq;
using System.Reflection;
using Myrmutation.Building;
using Myrmutation.Building.EditorTools;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode. Modo Construcción (A9): Confirmar y Cancelar sobre la copia del plano de DigPlanner, y la velocidad al
    /// entrar y salir. Mapa por defecto de NestGrid; todo lo que se pinta va por debajo de las salas iniciales (y &lt; 39).
    /// </summary>
    public class BuildModeTests
    {
        private GameObject go;
        private NestGrid grid;
        private GridNavigator nav;
        private DigPlanner planner;
        private RoomData room;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("BuildMode (test)");
            grid = go.AddComponent<NestGrid>();
            Call(grid, "Reset");   // RoomData de las salas iniciales
            Call(grid, "Awake");
            nav = go.AddComponent<GridNavigator>();
            Call(nav, "Awake");
            Call(nav, "OnEnable");
            planner = go.AddComponent<DigPlanner>();
            Call(planner, "Awake");
            Call(planner, "OnEnable");

            room = ScriptableObject.CreateInstance<RoomData>();
            room.footprint = new Vector2Int(21, 12);
        }

        [TearDown]
        public void TearDown()
        {
            Call(planner, "OnDisable");
            Call(nav, "OnDisable");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(room);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
            typeof(GameManager).GetProperty("Instance").SetValue(null, null);
            Navigation.Current = null;
            Digging.Current = null;
            CameraController.ToolMode = false;
            Time.timeScale = 1f;
        }

        [Test]
        public void Cancelar_RestauraExactamenteElPlanoAnterior()
        {
            PlanBefore();
            bool[] before = Snapshot();

            planner.BeginEdit();
            EditPlan();
            planner.Revert();

            Assert.IsFalse(planner.Editing);
            CollectionAssert.AreEqual(before, Snapshot());
            Assert.IsTrue(planner.TryGetRoomState(new Vector2Int(3, 60), out _, out _), "la sala cancelada vuelve");
            Assert.IsFalse(planner.TryGetRoomState(new Vector2Int(75, 3), out _, out _), "la sala nueva se va");
            Assert.AreEqual(1, planner.RoomCount);
        }

        [Test]
        public void Confirmar_MantieneElPlanoNuevo()
        {
            PlanBefore();
            planner.BeginEdit();
            EditPlan();
            planner.Commit();

            Assert.IsFalse(planner.Editing);
            Assert.IsTrue(planner.IsPlanned(new Vector2Int(40, 30)));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(37, 20)));
            Assert.IsFalse(planner.TryGetRoomState(new Vector2Int(3, 60), out _, out _));
            Assert.IsTrue(planner.TryGetRoomState(new Vector2Int(75, 3), out _, out _));
        }

        [Test]
        public void Cancelar_NoRellenaLoExcavadoDuranteLaEdicion()
        {
            PlanBefore();
            planner.BeginEdit();
            grid.Set(new Vector2Int(40, 20), CellType.Dug);
            planner.Revert();

            Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(40, 20)));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(40, 20)));
            Assert.IsTrue(planner.IsPlanned(new Vector2Int(41, 20)));
        }

        [TestCase(2, true)]
        [TestCase(3, false)]
        public void Salir_RecuperaLaVelocidadAnterior(int speed, bool confirm)
        {
            var gm = NewGameManager();
            gm.SetSpeed(speed);
            var mode = go.AddComponent<BuildModeController>();

            mode.Open();
            Assert.AreEqual(0f, Time.timeScale, "al entrar se pausa");
            Assert.IsTrue(CameraController.ToolMode);
            Assert.IsTrue(planner.Editing);

            mode.TogglePause();
            Assert.AreEqual(speed, gm.Speed, "▶ reanuda a la velocidad anterior");
            mode.TogglePause();
            Assert.AreEqual(0, gm.Speed);

            if (confirm) mode.Confirm(); else mode.Cancel();
            Assert.AreEqual(speed, gm.Speed);
            Assert.AreEqual(speed, Time.timeScale);
            Assert.IsFalse(CameraController.ToolMode);
            Assert.IsFalse(planner.Editing);
        }

        [Test]
        public void DesdePausa_ReanudaAx1_YAlSalirSigueEnPausa()
        {
            var gm = NewGameManager();
            gm.SetSpeed(0);
            var mode = go.AddComponent<BuildModeController>();

            mode.Open();
            mode.TogglePause();
            Assert.AreEqual(1, gm.Speed);
            mode.Cancel();
            Assert.AreEqual(0, gm.Speed);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void SalaReal_NoEstaEntreLasSalasConstruibles()
        {
            var names = BuildModeSetupMenu.BuildableRooms().Select(r => r.name).ToArray();
            CollectionAssert.DoesNotContain(names, "Room_Royal");
            CollectionAssert.Contains(names, "Room_Nursery");
        }

        [TestCase(CellType.Dug, true)]
        [TestCase(CellType.Room, true)]
        [TestCase(CellType.Solid, false)]
        [TestCase(CellType.Surface, false)]
        [TestCase(CellType.Blocked, false)]
        public void ContornoDeLoExistente_IncluyeTunelesYSalas(CellType type, bool drawn) =>
            Assert.AreEqual(drawn, BlueprintOverlay.IsExisting(type));

        // Un nivel se ve a > 40 px y es opaco a ≥ 80 px. Con el mapa casi entero (~12 px por casilla) solo el de 9×9.
        [TestCase(4f, 0f, 0f, 0f)]
        [TestCase(6f, 0.35f, 0f, 0f)]
        [TestCase(12f, 1f, 0f, 0f)]
        [TestCase(20f, 1f, 0.5f, 0f)]
        [TestCase(50f, 1f, 1f, 0.25f)]
        [TestCase(80f, 1f, 1f, 1f)]
        public void Rejilla_NivelVisibleSegunElZoom(float pixelsPerCell, float squares9, float blocks3, float cells)
        {
            Vector4 a = BlueprintOverlay.GridLevelAlphas(pixelsPerCell, 40f, 80f);
            Assert.AreEqual(squares9, a.x, 1e-4f, "9×9");
            Assert.AreEqual(blocks3, a.y, 1e-4f, "3×3");
            Assert.AreEqual(cells, a.z, 1e-4f, "casillas");
        }

        [Test]
        public void Lupa_VaEncimaDelDedo_YCercaDelBordeDeArribaAUnLado()
        {
            var screen = new Vector2(1000f, 600f);
            Assert.AreEqual(new Vector2(500f, 400f), MagnifierAt(new Vector2(500f, 300f), screen), "arriba");
            Assert.AreEqual(new Vector2(600f, 530f), MagnifierAt(new Vector2(500f, 580f), screen), "a la derecha, sin salirse por arriba");
            Assert.AreEqual(new Vector2(850f, 500f), MagnifierAt(new Vector2(950f, 500f), screen), "a la izquierda junto al borde derecho");
        }

        [Test]
        public void Siluetas_EncimaDelPapelAlAbrir_YComoEstabanAlCerrar()
        {
            var original = new Material(Shader.Find("Sprites/Default"));
            var silhouette = new Material(Shader.Find("Sprites/Default"));
            var grouped = NewAnt(true, original, out var group, out var groupedParts);
            var loose = NewAnt(false, original, out _, out var looseParts);
            var silhouettes = new BlueprintOverlay.Silhouettes(silhouette, 305);
            try
            {
                silhouettes.Apply(grouped);
                silhouettes.Apply(loose);
                silhouettes.Apply(grouped);   // repetir no duplica lo guardado
                Assert.AreEqual(305, group.sortingOrder);
                Assert.AreEqual(12, groupedParts[0].sortingOrder, "dentro del SortingGroup cada pieza conserva su orden");
                Assert.AreEqual(305, looseParts[1].sortingOrder, "sin SortingGroup, cada pieza va al orden de las siluetas");
                foreach (var sr in groupedParts.Concat(looseParts)) Assert.AreSame(silhouette, sr.sharedMaterial);

                silhouettes.RestoreAll();
                Assert.AreEqual(17, group.sortingOrder);
                Assert.AreEqual(new[] { 12, 13 }, looseParts.Select(sr => sr.sortingOrder).ToArray());
                foreach (var sr in groupedParts.Concat(looseParts)) Assert.AreSame(original, sr.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(grouped.gameObject);
                Object.DestroyImmediate(loose.gameObject);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(silhouette);
            }
        }

        // Hormiga con un hijo Visual (con o sin SortingGroup de orden 17) y dos piezas de orden 12 y 13.
        private static Ant NewAnt(bool withGroup, Material material, out SortingGroup group, out SpriteRenderer[] parts)
        {
            var ant = new GameObject("Ant (test)").AddComponent<Ant>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(ant.transform, false);
            group = withGroup ? visual.AddComponent<SortingGroup>() : null;
            if (group != null) group.sortingOrder = 17;
            parts = new SpriteRenderer[2];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = new GameObject($"Pieza {i}").AddComponent<SpriteRenderer>();
                parts[i].transform.SetParent(visual.transform, false);
                parts[i].sharedMaterial = material;
                parts[i].sortingOrder = 12 + i;
            }
            return ant;
        }

        private static Vector2 MagnifierAt(Vector2 finger, Vector2 screen) => BuildMagnifier.BubbleCenter(finger, 70f, 100f, screen);

        // Túnel en y = 20 (x 30–50) y una sala en (3, 60).
        private void PlanBefore()
        {
            planner.PaintStroke(At(30, 20), At(50, 20));
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 60)));
        }

        // Pinta, borra, cancela la sala de antes y planifica otra.
        private void EditPlan()
        {
            planner.PaintStroke(At(30, 30), At(50, 30));
            planner.EraseStroke(At(35, 20), At(40, 20));
            Assert.IsTrue(planner.CancelRoom(new Vector2Int(10, 65)));
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(75, 3)));
        }

        private bool[] Snapshot()
        {
            var cells = new bool[grid.Width * grid.Height];
            for (int i = 0; i < cells.Length; i++) cells[i] = planner.IsPlanned(new Vector2Int(i % grid.Width, i / grid.Width));
            return cells;
        }

        private GameManager NewGameManager()
        {
            var gm = go.AddComponent<GameManager>();
            Call(gm, "Awake");
            return gm;
        }

        private Vector3 At(int x, int y) => grid.CellToWorld(new Vector2Int(x, y));

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
