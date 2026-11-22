using System.Linq;
using System.Reflection;
using Myrmutation.Building;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode. Grieta de excavación (A8): fase según Progress, un trozo por casilla que se oculta al vaciarla y limpieza
    /// al terminar o cancelar el trabajo. Mismo mapa que DigPlannerTests: pozo en x 27–29 y el bloque (10, 4) = x 30–32,
    /// y 12–14 pegado a él.
    /// </summary>
    public class DigCracksTests
    {
        private static readonly RectInt Block = new RectInt(30, 12, 3, 3);

        private GameObject go;
        private NestGrid grid;
        private GridNavigator nav;
        private DigPlanner planner;
        private DigCracks cracks;
        private Ant ant;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DigCracks (test)");
            grid = go.AddComponent<NestGrid>();
            Call(grid, "Reset");
            Call(grid, "Awake");
            nav = go.AddComponent<GridNavigator>();
            Call(nav, "Awake");
            Call(nav, "OnEnable");
            planner = go.AddComponent<DigPlanner>();
            Call(planner, "Awake");
            Call(planner, "OnEnable");
            cracks = go.AddComponent<DigCracks>();
        }

        [TearDown]
        public void TearDown()
        {
            cracks.ReleaseSprites();   // texturas y trozos generados en Start
            Call(planner, "OnDisable");
            Call(nav, "OnDisable");
            if (ant != null) Object.DestroyImmediate(ant.gameObject);
            Object.DestroyImmediate(go);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
            Navigation.Current = null;
            Digging.Current = null;
            CameraController.ToolMode = false;
            Time.timeScale = 1f;
        }

        [TestCase(0f, 0)]
        [TestCase(0.1f, 1)]
        [TestCase(0.329f, 1)]
        [TestCase(0.33f, 2)]
        [TestCase(0.65f, 2)]
        [TestCase(0.66f, 3)]
        [TestCase(0.99f, 3)]
        [TestCase(1f, 0)]   // terminado: desaparece con el bloque
        public void Fase_SegunProgress(float progress, int phase)
        {
            var job = new DigJob(Vector2Int.zero, Vector3.zero, true, 1f);
            job.Work(1f, progress);
            Assert.AreEqual(phase, DigCracks.Phase(job));
        }

        [Test]
        public void SinEmpezar_NoHayGrieta_YAlAvanzarCambiaDeFase()
        {
            var job = StartJob();
            Assert.AreEqual(0, Shown(), "con Progress 0 aún no hay grieta");

            WorkTo(job, 0.05f);
            Assert.AreEqual(9, Shown());
            Sprite phase1 = PieceAt(Block.position).sprite;

            WorkTo(job, 0.4f);
            Assert.AreNotSame(phase1, PieceAt(Block.position).sprite, "fase 2");
        }

        [Test]
        public void AlVaciarUnaCasilla_SuTrozoSeOculta_YLosDemasSiguen()
        {
            var job = StartJob();
            WorkTo(job, 0.15f);   // 15 % de 9 casillas = 1
            Call(planner, "Update");

            var dugList = new System.Collections.Generic.List<Vector2Int>();
            foreach (var c in Block.allPositionsWithin)
                if (grid.Get(c) != CellType.Solid) dugList.Add(c);
            var dug = dugList.ToArray();
            Assert.AreEqual(1, dug.Length);
            Assert.IsFalse(IsShown(dug[0]), "el trozo de la casilla vaciada");
            Assert.AreEqual(8, Shown());
        }

        [Test]
        public void AlTerminar_DesapareceLaGrieta()
        {
            var job = StartJob();
            WorkTo(job, 0.5f);
            job.Work(1000f, 1f);
            Call(planner, "Update");
            Call(cracks, "Update");
            Assert.IsTrue(job.IsDone);
            Assert.AreEqual(0, Shown());
        }

        [Test]
        public void AlCancelar_DesapareceLaGrieta()
        {
            var job = StartJob();
            WorkTo(job, 0.05f);
            planner.EraseStroke(At(31, 13), At(31, 13), 2f);
            Call(cracks, "Update");
            Assert.IsTrue(job.IsDone, "borrar el plano cancela el trabajo");
            Assert.AreEqual(0, Shown());
        }

        [Test]
        public void ConElModoAbierto_NoSeDibuja()
        {
            var mode = go.AddComponent<BuildModeController>();
            typeof(DigCracks).GetField("buildMode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cracks, mode);
            var job = StartJob();
            WorkTo(job, 0.05f);
            Assert.AreEqual(9, Shown());

            mode.Open();
            Call(cracks, "Update");
            Assert.AreEqual(0, Shown());
            mode.Cancel();
            Call(cracks, "Update");
            Assert.AreEqual(9, Shown());
        }

        [Test]
        public void BloqueDeSala_CadaTrozoCaeEnSuCasilla()
        {
            var room = ScriptableObject.CreateInstance<RoomData>();
            room.footprint = new Vector2Int(21, 12);
            room.workCost = 30f;
            try
            {
                var origin = new Vector2Int(3, 60);
                Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, origin));
                RectInt rect = DigPlanner.RoomRect(room, origin);
                Shaft();
                planner.PaintBrush(At(25, 61));   // zona de acceso derecha, pegada al pozo
                ant = NewAnt(25, 61);
                Call(cracks, "Start");

                // Se termina la zona de acceso hasta que toca un bloque de la huella.
                DigJob job = null;
                for (int i = 0; i < 20 && job == null; i++)
                {
                    Assert.IsTrue(Claim(out var claimed));
                    if (rect.Contains(claimed.Chunk * 3)) { job = claimed; break; }
                    claimed.Work(1000f, 1f);
                    Call(planner, "Update");
                    claimed.Release(ant);
                }
                Assert.IsNotNull(job, "ningún bloque de la sala");

                WorkTo(job, 0.05f);
                Assert.AreEqual(9, Shown());
                foreach (var c in new RectInt(job.Chunk * 3, Vector2Int.one * 3).allPositionsWithin)
                    Assert.IsTrue(IsShown(c), $"trozo de {c} en el centro de su casilla");
            }
            finally { Object.DestroyImmediate(room); }
        }

        [Test]
        public void SinObreraExcavando_LaGrietaSeVeTenue()
        {
            var job = StartJob();
            job.Work(ant, 1f, 0.1f);   // con hormiga: cuenta como excavando
            Call(cracks, "Update");
            Assert.AreEqual(1f, PieceAt(Block.position).color.a, 1e-4f);

            job.Release(ant);           // la suelta a medias (hambre, cansancio…)
            Call(cracks, "Update");
            Assert.AreEqual(0.4f, PieceAt(Block.position).color.a, 1e-4f);
            Assert.AreEqual(9, Shown(), "sigue viéndose, solo más tenue");
        }

        // Pozo, el bloque en plano y una obrera conectada que lo reserva.
        private DigJob StartJob()
        {
            Shaft();
            planner.PaintBrush(At(31, 13));
            ant = NewAnt(28, 40);
            Assert.IsTrue(Claim(out var job));
            Assert.AreEqual(new Vector2Int(10, 4), job.Chunk);
            Call(cracks, "Start");
            Call(cracks, "Update");
            return job;
        }

        private void Shaft()
        {
            foreach (var c in new RectInt(27, 3, 3, 75).allPositionsWithin) grid.Set(c, CellType.Dug);
        }

        private Ant NewAnt(int x, int y)
        {
            var a = new GameObject("Ant (test)").AddComponent<Ant>();
            a.transform.position = At(x, y);
            return a;
        }

        // Como una obrera que pide cada frame: la primera llamada la apunta y, tras el reparto, la segunda lo recoge.
        private bool Claim(out DigJob job)
        {
            if (planner.TryClaim(ant, out job)) return true;
            Call(planner, "Assign");
            return planner.TryClaim(ant, out job);
        }

        // Avanza el trabajo hasta 'progress' (sin conocer su coste) y deja que las grietas se actualicen.
        private void WorkTo(DigJob job, float progress)
        {
            while (job.Progress < progress) job.Work(1f, 0.001f);
            Call(cracks, "Update");
        }

        private int Shown() => cracks.GetComponentsInChildren<SpriteRenderer>().Count(sr => sr.enabled);

        private bool IsShown(Vector2Int cell)
        {
            var sr = PieceAt(cell);
            return sr != null && sr.enabled && sr.gameObject.activeInHierarchy;
        }

        // Trozo activo dibujado en el centro de la casilla.
        private SpriteRenderer PieceAt(Vector2Int cell) =>
            cracks.GetComponentsInChildren<SpriteRenderer>()
                .FirstOrDefault(sr => Vector3.Distance(sr.transform.position, grid.CellToWorld(cell)) < 1e-3f);

        private Vector3 At(int x, int y) => grid.CellToWorld(new Vector2Int(x, y));

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
