using System.Collections.Generic;
using System.Reflection;
using Myrmutation.Ants;
using Myrmutation.Building;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode. Salas como prefabs (A10): RoomSpawner con el mapa por defecto de NestGrid (Reset pone sus RoomData) y
    /// NestRenderer en las puertas. Fuera del modo Play no se llama a OnEnable de lo instanciado, así que Room.All no se
    /// rellena aquí: "registrada" se comprueba con IsBuilt y el evento RoomBuilt, que es lo que escuchan los sistemas de la alfa.
    /// </summary>
    public class RoomSpawnerTests
    {
        private GameObject go;
        private NestGrid grid;
        private GridNavigator nav;
        private RoomSpawner spawner;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("RoomSpawner (test)");
            grid = go.AddComponent<NestGrid>();
            Call(grid, "Reset");
            Call(grid, "Awake");
            nav = go.AddComponent<GridNavigator>();
            Call(nav, "Awake");
            Call(nav, "OnEnable");
            spawner = go.AddComponent<RoomSpawner>();
            Call(spawner, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Call(spawner, "OnDisable");
            Call(nav, "OnDisable");
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
            foreach (var room in Object.FindObjectsByType<Room>(FindObjectsSortMode.None)) Object.DestroyImmediate(room.gameObject);
            Object.DestroyImmediate(go);
            Room.All.Clear();
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
            Navigation.Current = null;
        }

        [Test]
        public void RoomDug_PoneElPrefabEnLaHuella_YLaSalaQuedaTerminada()
        {
            var data = NewData(WithPrefab());
            var origin = new Vector2Int(3, 60);
            Room built = null;
            System.Action<RoomBuilt> onBuilt = e => built = e.Room;
            EventBus.Subscribe(onBuilt);
            try { EventBus.Publish(new RoomDug(data, origin)); }
            finally { EventBus.Unsubscribe(onBuilt); }

            Assert.IsNotNull(built, "RoomBuilt");
            Assert.AreSame(data, built.Data);
            Assert.IsTrue(built.IsBuilt);
            Vector3 corner = grid.CellToWorld(origin) - new Vector3(grid.CellSize, grid.CellSize, 0f) * 0.5f;
            Assert.AreEqual(corner, built.transform.position, "esquina inferior izquierda de la huella");
            Assert.AreEqual(nav.GroundPoint(new Vector2Int(origin.x + 10, origin.y + 1)), built.WorkPosition, "suelo de la fila de nodos");
        }

        [Test]
        public void SalasIniciales_ExistenTerminadasAlArrancar()
        {
            Call(spawner, "Start");
            var rooms = Object.FindObjectsByType<Room>(FindObjectsSortMode.None);
            Assert.AreEqual(3, rooms.Length);
            var names = new List<string>();
            foreach (var room in rooms)
            {
                Assert.IsTrue(room.IsBuilt, room.name);
                names.Add(room.Data.name);
            }
            CollectionAssert.AreEquivalent(new[] { "Room_Nursery", "Room_Royal", "Room_Storage" }, names);
            Assert.AreEqual(CellType.Room, grid.Get(new Vector2Int(64 + 26, 39 + 14)), "esquina de la Sala Real (27 × 15)");
        }

        [Test]
        public void RoomDataSinPrefab_AvisaUnaVez_YCreaLaSalaIgual()
        {
            var data = NewData(null);
            int warnings = 0;
            Application.LogCallback count = (msg, _, type) => { if (type == LogType.Warning && msg.Contains("no tiene prefab")) warnings++; };
            Application.logMessageReceived += count;
            try
            {
                EventBus.Publish(new RoomDug(data, new Vector2Int(3, 60)));
                EventBus.Publish(new RoomDug(data, new Vector2Int(3, 3)));
            }
            finally { Application.logMessageReceived -= count; }

            Assert.AreEqual(1, warnings);
            var rooms = Object.FindObjectsByType<Room>(FindObjectsSortMode.None);
            Assert.AreEqual(2, rooms.Length);
            foreach (var room in rooms) Assert.IsTrue(room.IsBuilt);
        }

        [Test]
        public void Puerta_SoloSeDibujaHuecaConSuZonaDeAccesoExcavada()
        {
            var renderer = go.AddComponent<NestRenderer>();
            typeof(NestRenderer).GetField("nest", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(renderer, grid);

            // Guardería: x 35–55, y 39–50. El túnel al pozo (x 56–58, y 39–41) toca su puerta derecha.
            Assert.IsTrue(Hollow(renderer, 55, 40), "puerta derecha, con túnel");
            Assert.IsFalse(Hollow(renderer, 55, 43), "pared derecha, encima de la puerta");
            Assert.IsFalse(Hollow(renderer, 45, 40), "interior");
            Assert.IsFalse(Hollow(renderer, 35, 40), "puerta izquierda, sin zona de acceso");

            foreach (var c in new RectInt(32, 39, 3, 3).allPositionsWithin) grid.Set(c, CellType.Dug);
            Assert.IsTrue(Hollow(renderer, 35, 40), "puerta izquierda, con su zona excavada");
        }

        [Test]
        public void HormigaAMediaAlturaDentroDeLaSala_AcabaEnElSueloAlTerminarla()
        {
            var rect = new RectInt(3, 60, 21, 12);
            foreach (var c in rect.allPositionsWithin) grid.Set(c, CellType.Room);   // como TryFinishRoom
            var ant = new GameObject("Ant (test)").AddComponent<Ant>();
            created.Add(ant.gameObject);
            ant.transform.position = grid.CellToWorld(new Vector2Int(13, 66)) + Vector3.forward;
            Ant.All.Add(ant);   // fuera del modo Play no se llama a su OnEnable
            try { EventBus.Publish(new RoomDug(NewData(WithPrefab()), rect.position)); }
            finally { Ant.All.Remove(ant); }

            Vector3 floor = nav.GroundPoint(new Vector2Int(13, 61));
            floor.z = 1f;
            Assert.AreEqual(floor, ant.transform.position);
            Assert.IsFalse(nav.OffRoomFloor(ant.transform.position, out _), "ya está en el suelo");
        }

        [Test]
        public void HormigaEnLaFilaDelSuelo_OEnUnaRutaQueCruzaLaSala_NoEstaFueraDelSuelo()
        {
            // Guardería: x 35–55, y 39–50; fila de nodos y = 40. Con su zona de acceso izquierda excavada se cruza de puerta a puerta.
            foreach (var c in new RectInt(32, 39, 3, 3).allPositionsWithin) grid.Set(c, CellType.Dug);
            for (int x = 35; x <= 55; x++)
                Assert.IsFalse(nav.OffRoomFloor(nav.GroundPoint(new Vector2Int(x, 40)), out _), $"GroundPoint de ({x}, 40)");

            var path = new List<Vector3>();
            Assert.IsTrue(nav.TryGetPath(grid.CellToWorld(new Vector2Int(33, 40)), grid.CellToWorld(new Vector2Int(57, 40)), path));
            for (int i = 1; i < path.Count; i++)
                for (int s = 0; s <= 10; s++)
                {
                    Vector3 p = Vector3.Lerp(path[i - 1], path[i], s / 10f);
                    Assert.IsFalse(nav.OffRoomFloor(p, out _), $"tramo {i}, {p}");
                }
            Assert.IsTrue(nav.OffRoomFloor(grid.CellToWorld(new Vector2Int(45, 45)), out _), "a media altura sí");
        }

        [Test]
        public void HormigaEnMarchaHaciaDentroDeLaSala_SePara_AlTerminarla()
        {
            var rect = new RectInt(3, 60, 21, 12);
            foreach (var c in rect.allPositionsWithin) grid.Set(c, CellType.Dug);
            var ant = new GameObject("Ant (test)").AddComponent<Ant>();
            created.Add(ant.gameObject);
            var actuator = ant.gameObject.AddComponent<AntActuator>();
            ant.transform.position = nav.GroundPoint(new Vector2Int(13, 61));
            // Iba al sitio de un bloque alto de la sala (a media altura), que deja de ser nodo al terminarla.
            actuator.MoveAlong(new[] { nav.GroundPoint(new Vector2Int(13, 66)) });
            Assert.AreEqual(AntAction.Moving, actuator.Current);

            foreach (var c in rect.allPositionsWithin) grid.Set(c, CellType.Room);   // como TryFinishRoom
            Ant.All.Add(ant);
            try { EventBus.Publish(new RoomDug(NewData(WithPrefab()), rect.position)); }
            finally { Ant.All.Remove(ant); }

            Assert.AreEqual(AntAction.Idle, actuator.Current, "no recalcula hacia un sitio que ya no existe");
        }

        private static bool Hollow(NestRenderer renderer, int x, int y) =>
            (bool)typeof(NestRenderer).GetMethod("IsHollow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, new object[] { x, y });

        // Plantilla como los prefabs de Prefabs/Rooms: Room con su WorkPoint y un sprite.
        private GameObject WithPrefab()
        {
            var template = new GameObject("Room_Test");
            var work = new GameObject("WorkPoint").transform;
            work.SetParent(template.transform, false);
            template.AddComponent<SpriteRenderer>();
            var room = template.AddComponent<Room>();
            typeof(Room).GetField("workPoint", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(room, work);
            created.Add(template);
            return template;
        }

        private RoomData NewData(GameObject prefab)
        {
            var data = ScriptableObject.CreateInstance<RoomData>();
            data.name = "Room_Test";
            data.footprint = new Vector2Int(21, 12);
            data.prefab = prefab;
            created.Add(data);
            return data;
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
