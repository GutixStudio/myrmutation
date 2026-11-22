using System.Reflection;
using Myrmutation.Building;
using Myrmutation.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode. Terrones al excavar (A8 / F13): solo con la excavación real, vida y vuelta al pool, reutilización del más
    /// viejo y nada con el modo Construcción abierto. Mismo mapa que DigPlannerTests (pozo en x 27–29, bloque x 30–32,
    /// y 12–14). Advance(dt) se llama por reflexión en lugar de Update; semilla fija.
    /// </summary>
    public class DigChunksTests
    {
        private GameObject go, chunksGo;
        private NestGrid grid;
        private GridNavigator nav;
        private DigPlanner planner;
        private DigChunks chunks;
        private Ant ant;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DigChunks (test)");
            grid = go.AddComponent<NestGrid>();
            Call(grid, "Reset");
            Call(grid, "Awake");
            nav = go.AddComponent<GridNavigator>();
            Call(nav, "Awake");
            Call(nav, "OnEnable");
            planner = go.AddComponent<DigPlanner>();
            Call(planner, "Awake");
            Call(planner, "OnEnable");
            chunksGo = new GameObject("Terrones (test)");
            chunks = chunksGo.AddComponent<DigChunks>();
            SetField("seed", 1234);
        }

        [TearDown]
        public void TearDown()
        {
            chunks.ReleaseSprites();   // placeholder generado en Start
            Call(planner, "OnDisable");
            Call(nav, "OnDisable");
            if (ant != null) Object.DestroyImmediate(ant.gameObject);
            Object.DestroyImmediate(chunksGo);
            Object.DestroyImmediate(go);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
            Navigation.Current = null;
            Digging.Current = null;
            CameraController.ToolMode = false;
            Time.timeScale = 1f;
        }

        [Test]
        public void AlExcavarUnaCasilla_Salen2o3_YPorOtraViaNinguno()
        {
            Call(chunks, "Start");
            foreach (var c in new RectInt(27, 3, 3, 75).allPositionsWithin) grid.Set(c, CellType.Dug);   // pozo: Set directo
            grid.Set(new Vector2Int(28, 40), CellType.Room);
            Assert.AreEqual(0, Active(), "Set directo y Dug → Room no lanzan terrones");
            grid.Set(new Vector2Int(28, 40), CellType.Dug);

            planner.PaintBrush(At(31, 13));
            var job = Claim();
            while (job.Progress < 0.15f) job.Work(1f, 0.001f);   // 15 % de 9 casillas = 1
            Call(planner, "Update");
            Assert.That(Active(), Is.InRange(2, 3));
        }

        [Test]
        public void PasadoSuTiempoDeVida_VuelvenAlPool()
        {
            Call(chunks, "Start");
            chunks.Burst(new Vector2Int(31, 13));
            int launched = Active();
            Advance(0.3f);
            Assert.AreEqual(launched, Active(), "a mitad de vida siguen");
            Advance(0.31f);
            Assert.AreEqual(0, Active());
        }

        [Test]
        public void ConElPoolLleno_SeReutilizaElMasViejo()
        {
            SetField("poolSize", 3);
            Call(chunks, "Start");
            chunks.Burst(new Vector2Int(31, 13));
            Advance(0.1f);
            var second = new Vector2Int(60, 30);
            chunks.Burst(second);

            Assert.AreEqual(3, Active(), "nunca más que el pool");
            Assert.That(Vector3.Distance(chunksGo.transform.GetChild(0).position, grid.CellToWorld(second)), Is.LessThan(1e-3f),
                "el primer hueco (el más viejo) sale ahora de la segunda casilla");
        }

        [Test]
        public void ConElModoAbierto_NoSeLanzan_YLosQueHabiaSeQuitan()
        {
            var mode = go.AddComponent<BuildModeController>();
            SetField("buildMode", mode);
            Call(chunks, "Start");
            chunks.Burst(new Vector2Int(31, 13));
            Assert.That(Active(), Is.GreaterThan(0));

            mode.Open();
            Advance(0.01f);
            Assert.AreEqual(0, Active(), "al abrir se quitan");
            chunks.Burst(new Vector2Int(31, 13));
            Assert.AreEqual(0, Active(), "con el modo abierto no se lanzan");
            mode.Cancel();
        }

        // Obrera conectada por el pozo que reserva el bloque (10, 4).
        private DigJob Claim()
        {
            ant = new GameObject("Ant (test)").AddComponent<Ant>();
            ant.transform.position = At(28, 40);
            if (!planner.TryClaim(ant, out var job))
            {
                Call(planner, "Assign");
                Assert.IsTrue(planner.TryClaim(ant, out job));
            }
            return job;
        }

        private int Active() => chunksGo.GetComponentsInChildren<SpriteRenderer>().Length;

        private void Advance(float dt) =>
            typeof(DigChunks).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chunks, new object[] { dt });

        private void SetField(string name, object value) =>
            typeof(DigChunks).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chunks, value);

        private Vector3 At(int x, int y) => grid.CellToWorld(new Vector2Int(x, y));

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
