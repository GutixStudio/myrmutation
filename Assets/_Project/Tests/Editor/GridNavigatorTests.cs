using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Myrmutation.Building;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode (Window → General → Test Runner). Unity no llama a Awake ni a OnEnable fuera de Play,
    /// así que se llaman por reflexión. Los túneles se pintan en la esquina inferior izquierda
    /// (x, y &lt; 35), que en el mapa inicial es tierra sin excavar.
    /// </summary>
    public class GridNavigatorTests
    {
        private GameObject go;
        private NestGrid grid;
        private GridNavigator nav;
        private readonly List<Vector3> path = new List<Vector3>();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("GridNavigator (test)");
            grid = go.AddComponent<NestGrid>();
            Call(grid, "Awake");
            nav = go.AddComponent<GridNavigator>();
            Call(nav, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Call(nav, "OnDisable");
            Object.DestroyImmediate(go);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
        }

        [Test]
        public void TunelDiagonalDelPincelDaRutaDeExtremoAExtremo()
        {
            Paint(new Vector2Int(6, 6), new Vector2Int(30, 30));
            Assert.IsTrue(nav.TryGetPath(At(6, 6), At(30, 30), path));
            Assert.Less(Vector3.Distance(path[path.Count - 1], At(30, 30)), 1f);
        }

        [Test]
        public void TensadoDeUnaDiagonalRectaDejaComoMucho3Puntos()
        {
            Paint(new Vector2Int(6, 6), new Vector2Int(30, 30));
            Assert.IsTrue(nav.TryGetPath(At(6, 6), At(30, 30), path, GridNavigator.PathStage.Pulled));
            Assert.LessOrEqual(path.Count, 3);
        }

        [Test]
        public void PuntosTensadosCaenEnNodos()
        {
            Paint(new Vector2Int(6, 6), new Vector2Int(20, 20));
            Paint(new Vector2Int(20, 20), new Vector2Int(32, 12));
            Assert.IsTrue(nav.TryGetPath(At(6, 6), At(32, 12), path, GridNavigator.PathStage.Pulled));
            foreach (var p in path) Assert.IsTrue(nav.IsNode(grid.WorldToCell(p)), $"{p} no cae en un nodo");
        }

        [Test]
        public void ChaikinEnUnaLLargaNoSeMeteEnTierra()
        {
            Paint(new Vector2Int(5, 6), new Vector2Int(32, 6));
            Paint(new Vector2Int(32, 6), new Vector2Int(32, 33));
            Assert.IsTrue(nav.TryGetPath(At(5, 6), At(32, 33), path, GridNavigator.PathStage.Smoothed));
            foreach (var p in path) Assert.IsTrue(NestGrid.IsOpen(grid.Get(grid.WorldToCell(p))), $"{p} cae en tierra");
        }

        [Test]
        public void SinUnionNoHayRutaYAlExcavarlaSi()
        {
            Paint(new Vector2Int(5, 10), new Vector2Int(14, 10));
            Paint(new Vector2Int(21, 10), new Vector2Int(30, 10));
            Assert.IsFalse(nav.TryGetPath(At(5, 10), At(30, 10), path));

            Paint(new Vector2Int(14, 10), new Vector2Int(21, 10));   // grid.Set → CellChanged → vacía la caché
            Assert.IsTrue(nav.TryGetPath(At(5, 10), At(30, 10), path));
        }

        [Test]
        public void ALaSalaSoloSeEntraPorSusPuertas()
        {
            // Sala 21 × 12 en x 5..25, y 5..16. Su fila de nodos es la y = 6.
            for (int y = 5; y <= 16; y++)
                for (int x = 5; x <= 25; x++)
                    grid.Set(new Vector2Int(x, y), CellType.Room);

            // Túnel que toca el techo de la sala: no es una puerta.
            Paint(new Vector2Int(15, 18), new Vector2Int(15, 30));
            Assert.IsFalse(nav.TryGetPath(At(15, 28), At(15, 6), path));

            // Zona de acceso de la puerta izquierda (x 2..4, y 5..7) unida al túnel.
            Paint(new Vector2Int(3, 6), new Vector2Int(3, 25));
            Paint(new Vector2Int(3, 25), new Vector2Int(15, 25));
            Assert.IsTrue(nav.TryGetPath(At(15, 28), At(15, 6), path, GridNavigator.PathStage.Pulled));
            foreach (var p in path)
            {
                Vector2Int c = grid.WorldToCell(p);
                if (grid.Get(c) == CellType.Room) Assert.AreEqual(6, c.y, $"{p} está en la sala fuera de su fila");
            }
        }

        [Test]
        public void DosSalasQueCompartenZonaDeAccesoSeUnenPorElla()
        {
            // Salas 21 × 12 en x 3..23 y x 27..47 (y 3..14), separadas solo por la zona común x 24..26, y 3..5.
            // La fila de nodos de las dos es la y = 4.
            var a = new RectInt(3, 3, 21, 12);
            var b = new RectInt(27, 3, 21, 12);
            foreach (var room in new[] { a, b })
                foreach (var c in room.allPositionsWithin) grid.Set(c, CellType.Room);
            foreach (var c in new RectInt(24, 3, 3, 3).allPositionsWithin) grid.Set(c, CellType.Dug);

            Assert.IsTrue(nav.TryGetPath(At(13, 9), At(37, 9), path, GridNavigator.PathStage.Pulled));
            Assert.IsTrue(a.Contains(grid.WorldToCell(path[0])), "la ruta no sale de la sala A");
            Assert.IsTrue(b.Contains(grid.WorldToCell(path[path.Count - 1])), "la ruta no acaba en la sala B");
            foreach (var p in path)
            {
                Vector2Int c = grid.WorldToCell(p);
                if (grid.Get(c) == CellType.Room) Assert.AreEqual(4, c.y, $"{p} está en una sala fuera de su fila");
            }
        }

        [Test]
        public void MapaInicialLlegaALasTresSalasPorSuFilaCentral()
        {
            // Salas del mapa inicial: suelo en y = 39, así que su fila de nodos es la y = 40.
            var rooms = new[] { new RectInt(35, 39, 21, 12), new RectInt(64, 39, 27, 15), new RectInt(95, 39, 21, 12) };
            foreach (var room in rooms)
            {
                Vector3 inside = At((int)room.center.x, (int)room.center.y);
                Assert.IsTrue(nav.TryGetPath(At(60, 77), inside, path, GridNavigator.PathStage.Pulled), $"sin ruta a la sala {room}");
                foreach (var p in path)
                {
                    Vector2Int c = grid.WorldToCell(p);
                    if (grid.Get(c) == CellType.Room) Assert.AreEqual(40, c.y, $"{p} está en la sala {room} fuera de su fila");
                }
                Assert.AreEqual(CellType.Room, grid.Get(grid.WorldToCell(path[path.Count - 1])), $"la ruta no acaba dentro de {room}");
            }
        }

        [Test]
        public void RutaQueLlegaBajandoPorUnPozoAcabaEnGroundPoint()
        {
            Paint(new Vector2Int(10, 30), new Vector2Int(10, 6));
            AssertEndsAtGroundPoint(At(10, 30), At(10, 8));
        }

        [Test]
        public void RutaQueLlegaEnDiagonalAcabaEnGroundPoint()
        {
            Paint(new Vector2Int(6, 6), new Vector2Int(30, 30));
            AssertEndsAtGroundPoint(At(6, 6), At(30, 30));
        }

        private void AssertEndsAtGroundPoint(Vector3 from, Vector3 to)
        {
            Assert.IsTrue(nav.Nearest(to, out var goal));
            Assert.IsTrue(nav.TryGetPath(from, to, path));
            Assert.Less(Vector3.Distance(path[path.Count - 1], nav.GroundPoint(goal)), 1e-5f);
        }

        [Test]
        public void NingunaRutaTienePuntosPegados()
        {
            Paint(new Vector2Int(6, 6), new Vector2Int(20, 20));
            Paint(new Vector2Int(20, 20), new Vector2Int(32, 12));
            var ends = new[] { At(6, 6), At(20, 20), At(32, 12), At(60, 77), At(45, 45), At(77, 46), At(105, 45), At(60, 50) };
            foreach (var a in ends)
                foreach (var b in ends)
                {
                    if (!nav.TryGetPath(a, b, path)) continue;
                    for (int i = 1; i < path.Count; i++)
                        Assert.GreaterOrEqual(Vector3.Distance(path[i - 1], path[i]), 0.05f - 1e-5f, $"{a} → {b}, puntos {i - 1} y {i}");
                }
        }

        [Test]
        public void NearestDesdeDentroDeLaTierraDevuelveUnNodo()
        {
            Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(20, 20)));
            Assert.IsTrue(nav.Nearest(At(20, 20), out var node));
            Assert.IsTrue(nav.IsNode(node));
        }

        [Test]
        public void MedirNodosYTiempoDe30Busquedas()
        {
            int nodes = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (nav.IsNode(new Vector2Int(x, y))) nodes++;

            // Sin caché (la sobrecarga con etapa no la usa): de la Guardería a la Despensa del mapa inicial.
            Vector3 from = At(36, 40), to = At(114, 40);
            var watch = Stopwatch.StartNew();
            bool found = true;
            for (int i = 0; i < 30; i++) found &= nav.TryGetPath(from, to, path, GridNavigator.PathStage.Lowered);
            watch.Stop();

            Debug.Log($"[A7] Nodos del mapa inicial: {nodes}. 30 A* de punta a punta: {watch.Elapsed.TotalMilliseconds:F1} ms " +
                      $"({watch.Elapsed.TotalMilliseconds / 30:F2} ms cada uno, ruta encontrada: {found}, {path.Count} puntos).");
        }

        // Pincel de radio 1,5 a lo largo de un segmento, solo sobre tierra (como el de A8).
        private void Paint(Vector2Int a, Vector2Int b)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : s / (float)steps);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var c = new Vector2Int(Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.y) + dy);
                        if ((c - p).sqrMagnitude <= 2.25f && grid.Get(c) == CellType.Solid) grid.Set(c, CellType.Dug);
                    }
            }
        }

        private Vector3 At(int x, int y) => grid.CellToWorld(new Vector2Int(x, y));

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
