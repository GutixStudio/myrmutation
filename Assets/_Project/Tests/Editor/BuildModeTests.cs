using System.Linq;
using System.Reflection;
using Myrmutation.Building;
using Myrmutation.Building.EditorTools;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using NUnit.Framework;
using UnityEngine;
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
