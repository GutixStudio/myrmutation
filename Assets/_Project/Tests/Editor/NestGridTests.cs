using System.Collections.Generic;
using System.Reflection;
using Myrmutation.Building;
using NUnit.Framework;
using UnityEngine;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode (Window → General → Test Runner). Unity no llama a Awake fuera de Play,
    /// así que cada test lo llama por reflexión y el TearDown limpia el singleton.
    /// </summary>
    public class NestGridTests
    {
        private GameObject go;
        private NestGrid grid;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("NestGrid (test)");
            go.transform.position = new Vector3(3f, -2f, 0f);   // origen no nulo para probar el desplazamiento
            grid = go.AddComponent<NestGrid>();
            typeof(NestGrid).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grid, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
        }

        [Test]
        public void CeldaAMundoIdaYVuelta()
        {
            float half = grid.CellSize * 0.5f - 0.001f;
            foreach (var c in new[] { new Vector2Int(0, 0), new Vector2Int(119, 89), new Vector2Int(60, 39), new Vector2Int(17, 55) })
            {
                Vector3 center = grid.CellToWorld(c);
                Assert.AreEqual(c, grid.WorldToCell(center));
                Assert.AreEqual(c, grid.WorldToCell(center - new Vector3(half, half)), "esquina inferior izquierda");
                Assert.AreEqual(c, grid.WorldToCell(center + new Vector3(half, half)), "esquina superior derecha");
            }
            Assert.AreEqual(new Vector2Int(20, 13), grid.CellToChunk(new Vector2Int(60, 39)));
        }

        [Test]
        public void EstratoSegunLaFila()
        {
            Assert.AreEqual(Stratum.Soil, grid.StratumAt(new Vector2Int(5, 77)));
            Assert.AreEqual(Stratum.Soil, grid.StratumAt(new Vector2Int(5, 54)));
            Assert.AreEqual(Stratum.Clay, grid.StratumAt(new Vector2Int(5, 53)));
            Assert.AreEqual(Stratum.Clay, grid.StratumAt(new Vector2Int(5, 27)));
            Assert.AreEqual(Stratum.Rock, grid.StratumAt(new Vector2Int(5, 26)));
            Assert.AreEqual(Stratum.Rock, grid.StratumAt(new Vector2Int(5, 0)));
        }

        [Test]
        public void HolguraConLasCuatroVecinas()
        {
            Fill(new RectInt(10, 10, 3, 3));
            Assert.IsTrue(grid.HasClearance(new Vector2Int(11, 11)));
            Assert.IsFalse(grid.HasClearance(new Vector2Int(10, 11)), "borde del hueco");

            grid.Set(new Vector2Int(20, 10), CellType.Dug);
            Assert.IsFalse(grid.HasClearance(new Vector2Int(20, 10)), "casilla suelta");

            Fill(new RectInt(1, 20, 3, 3));
            Assert.IsFalse(grid.HasClearance(new Vector2Int(1, 21)), "pegada al borde del mapa");

            // La superficie cuenta como abierta: la boca del pozo conecta con la hierba.
            Assert.IsTrue(grid.HasClearance(new Vector2Int(60, 77)));
        }

        [Test]
        public void SetAvisaYRespetaLosBordes()
        {
            int changes = 0;
            grid.CellChanged += _ => changes++;

            grid.Set(new Vector2Int(20, 20), CellType.Dug);
            grid.Set(new Vector2Int(20, 20), CellType.Dug);
            Assert.AreEqual(1, changes, "repetir el mismo tipo no avisa");

            grid.Set(new Vector2Int(22, 20), CellType.Room);
            Assert.AreEqual(CellType.Room, grid.Get(new Vector2Int(22, 20)), "Solid→Room (A10)");

            grid.Set(new Vector2Int(0, 20), CellType.Dug);
            grid.Set(new Vector2Int(20, 80), CellType.Dug);
            grid.Set(new Vector2Int(24, 20), CellType.Surface);
            Assert.AreEqual(CellType.Blocked, grid.Get(new Vector2Int(0, 20)));
            Assert.AreEqual(CellType.Surface, grid.Get(new Vector2Int(20, 80)));
            Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(24, 20)));
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void MapaInicialConZonasDeAccesoExcavadas()
        {
            // Zonas de acceso 3×3 fuera de las puertas que dan a la red de túneles (filas 39–41).
            AssertDug(new RectInt(56, 39, 3, 3), "Guardería, puerta derecha");
            AssertDug(new RectInt(61, 39, 3, 3), "Sala Real, puerta izquierda");
            AssertDug(new RectInt(91, 39, 3, 3), "Sala Real, puerta derecha");
            AssertDug(new RectInt(92, 39, 3, 3), "Despensa, puerta izquierda");

            // El pozo no entra por el techo de la Sala Real, que queda a 24 de profundidad.
            for (int x = 64; x <= 90; x++)
                Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(x, 54)), $"techo de la Sala Real en x={x}");
            Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(64, 53)));
        }

        [Test]
        public void SueloBajoUnPunto()
        {
            Vector3 inRoom = grid.CellToWorld(new Vector2Int(77, 47));
            Assert.AreEqual(go.transform.position.y + 39 * grid.CellSize, grid.FloorPoint(inRoom).y, 1e-4f);

            Vector3 inSoil = grid.CellToWorld(new Vector2Int(20, 60));
            Assert.AreEqual(inSoil, grid.FloorPoint(inSoil));
        }

        [Test]
        public void TunelDiagonalA45GradosQuedaConectado()
        {
            PaintLine(new Vector2(10f, 5f), new Vector2(30f, 25f));
            Assert.IsTrue(Connected(new Vector2Int(10, 5), new Vector2Int(30, 25)));
        }

        [Test]
        public void TunelConPendiente2a1QuedaConectado()
        {
            PaintLine(new Vector2(40f, 5f), new Vector2(70f, 20f));
            Assert.IsTrue(Connected(new Vector2Int(40, 5), new Vector2Int(70, 20)));
        }

        // ---------------- Ayudas ----------------
        private void Fill(RectInt r)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    grid.Set(new Vector2Int(x, y), CellType.Dug);
        }

        private void AssertDug(RectInt r, string what)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    Assert.IsTrue(NestGrid.IsOpen(grid.Get(new Vector2Int(x, y))), $"{what}: ({x},{y}) sin excavar");
        }

        // Pincel de radio 1,5 casillas (túnel de 3) arrastrado en pasos de un cuarto de casilla.
        private void PaintLine(Vector2 from, Vector2 to)
        {
            const float radius = 1.5f;
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 4f);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                for (int y = Mathf.FloorToInt(p.y - radius); y <= Mathf.CeilToInt(p.y + radius); y++)
                    for (int x = Mathf.FloorToInt(p.x - radius); x <= Mathf.CeilToInt(p.x + radius); x++)
                        if ((new Vector2(x, y) - p).sqrMagnitude <= radius * radius)
                            grid.Set(new Vector2Int(x, y), CellType.Dug);
            }
        }

        // BFS en 8 direcciones (como el A* de A7) solo por casillas con holgura.
        private bool Connected(Vector2Int from, Vector2Int to)
        {
            Assert.IsTrue(grid.HasClearance(from) && grid.HasClearance(to), "los extremos deben tener holgura");
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) return true;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var n = new Vector2Int(c.x + dx, c.y + dy);
                        if (grid.HasClearance(n) && seen.Add(n)) queue.Enqueue(n);
                    }
            }
            return false;
        }
    }
}
