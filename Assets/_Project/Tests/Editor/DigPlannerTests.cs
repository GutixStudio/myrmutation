using System;
using System.Collections.Generic;
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
    /// EditMode (Window → General → Test Runner). Awake, OnEnable y Update se llaman por reflexión.
    /// Mapa de prueba: un pozo excavado en x 27–29 (bloque x = 9) de y = 3 hasta la superficie, que conecta con la
    /// colonia por la hierba. Los bloques x = 10 (x 30–32) quedan pegados a él; las salas van en x 3–23, con la zona de
    /// acceso derecha en x 24–26 (bloque x = 8). Estratos: tierra y ≥ 54, arcilla 27–53, roca ≤ 26.
    /// </summary>
    public class DigPlannerTests
    {
        private const float Eps = 1e-3f;

        private GameObject go;
        private NestGrid grid;
        private GridNavigator nav;
        private DigPlanner planner;
        private RoomData room;
        private readonly List<Ant> ants = new List<Ant>();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DigPlanner (test)");
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
            room.workCost = 30f;
        }

        [TearDown]
        public void TearDown()
        {
            Call(planner, "OnDisable");
            Call(nav, "OnDisable");
            foreach (var ant in ants) Object.DestroyImmediate(ant.gameObject);
            ants.Clear();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(room);
            typeof(NestGrid).GetProperty("Instance").SetValue(null, null);
            Navigation.Current = null;
            Digging.Current = null;
        }

        [Test]
        public void Pincel_NoMarcaAnilloNiSuperficieNiExcavado()
        {
            planner.PaintBrush(At(10, 80));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(10, 80)), "superficie");

            planner.PaintBrush(At(60, 50));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(60, 50)), "pozo ya excavado");

            // La Guardería empieza en x = 35: x 33–34 es su anillo.
            planner.PaintBrush(At(32, 45));
            Assert.IsTrue(planner.IsPlanned(new Vector2Int(31, 45)));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(33, 45)), "anillo de sala hecha");

            // Sala en plano en x 3–23: x 24–25 es su anillo (fuera de la zona de acceso, y 60–62).
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 60)));
            planner.PaintBrush(At(25, 66));
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(25, 66)), "anillo de sala en plano");
            Assert.IsTrue(planner.IsPlanned(new Vector2Int(26, 66)));
        }

        [Test]
        public void BloqueSinVecinoAlcanzable_NoEsTrabajable_AlExcavarLaUnionSi()
        {
            Shaft();
            PaintChunk(7, 10);   // x 21–23, y 30–32: entre él y el pozo queda el bloque x 24–26 sin excavar
            var ant = NewAnt(22, 31);
            Assert.IsFalse(planner.HasWork);
            Assert.IsFalse(Claim(ant, out _));

            Dig(new RectInt(24, 30, 3, 3));
            Assert.IsTrue(planner.HasWork);
            Assert.IsTrue(Claim(ant, out var job));
            Assert.AreEqual(new Vector2Int(7, 10), job.Chunk);
        }

        [Test]
        public void HormigasAtrapadasMasCercanas_NoRecibenTrabajo_YLasConectadasLoExcavanEntero()
        {
            Shaft();
            PaintChunk(10, 4);                      // x 30–32, y 12–14, pegado al pozo
            Dig(new RectInt(36, 10, 9, 3));         // bolsa sin unir a nada, a 3 casillas del bloque
            var trapped = new[] { NewAnt(37, 11), NewAnt(38, 11) };
            var connected = new[] { NewAnt(28, 40), NewAnt(28, 45) };

            foreach (var ant in trapped) Assert.IsFalse(Claim(ant, out _), $"{ant.name} está atrapada");
            Assert.IsTrue(Claim(connected[0], out var job));
            Assert.IsTrue(Claim(connected[1], out var second));
            Assert.AreSame(job, second, "las dos conectadas comparten el bloque");
            Assert.AreEqual(new Vector2Int(10, 4), job.Chunk);
            foreach (var ant in trapped) Assert.IsFalse(Claim(ant, out _));

            job.Work(2f, 10f);
            Tick();
            Assert.IsTrue(job.IsDone);
            foreach (var c in new RectInt(30, 12, 3, 3).allPositionsWithin) Assert.AreEqual(CellType.Dug, grid.Get(c), c.ToString());
        }

        [Test]
        public void CosteDeTunel_SigueLaFormulaPorEstrato()
        {
            Shaft();
            Assert.AreEqual(1.5f, ClaimCost(10, 20), Eps, "tierra");
            Assert.AreEqual(1.5f * 1.33f, ClaimCost(10, 10), Eps, "arcilla");
            Assert.AreEqual(3f, ClaimCost(10, 4), Eps, "roca");

            planner.PaintBrush(At(30, 67), 0.5f);   // una sola casilla del bloque (10, 22), pegada al pozo
            Assert.IsTrue(Claim(NewAnt(30, 67), out var job));
            Assert.AreEqual(new Vector2Int(10, 22), job.Chunk);
            Assert.AreEqual(1.5f / 9f, CostOf(job), Eps, "1 de 9 casillas");
        }

        [Test]
        public void Vaciado_DeLaMasCercanaALaMasLejana_Y_EnProgress1TodoDug()
        {
            Shaft();
            PaintChunk(10, 20);   // x 30–32, y 60–62; la obrera se pone en el pozo, en (28, 61)
            Assert.IsTrue(Claim(NewAnt(31, 61), out var job));
            Assert.AreEqual(nav.GroundPoint(new Vector2Int(28, 61)), job.StandPosition);
            Assert.IsTrue(job.FacingRight);

            job.Work(1f, 0.18f);   // 12 % de 9 casillas = 1
            Tick();
            Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(30, 61)));
            Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(30, 60)));

            job.Work(1f, 0.36f);   // 36 % = 3: la columna más cercana entera
            Tick();
            for (int y = 60; y <= 62; y++) Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(30, y)));
            Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(31, 61)));

            job.Work(1f, 10f);
            Tick();
            Assert.IsTrue(job.IsDone);
            foreach (var c in new RectInt(30, 60, 3, 3).allPositionsWithin)
            {
                Assert.AreEqual(CellType.Dug, grid.Get(c), c.ToString());
                Assert.IsFalse(planner.IsPlanned(c));
            }
            Assert.IsFalse(planner.HasWork);
        }

        [Test]
        public void BorrarElPlanoDeUnBloqueEnObras_DejaSuTrabajoIsDone_YLoVaciadoSigueDug()
        {
            Shaft();
            PaintChunk(10, 20);
            Assert.IsTrue(Claim(NewAnt(31, 61), out var job));
            job.Work(1f, 0.36f);   // 24 % = 2 casillas de la columna x = 30
            Tick();

            planner.Erase(At(31, 61));
            Assert.IsTrue(job.IsDone);
            Assert.IsFalse(job.CanClaim);
            Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(30, 61)));
            Assert.AreEqual(CellType.Solid, grid.Get(new Vector2Int(31, 61)));
            Assert.IsFalse(planner.HasWork);
        }

        [Test]
        public void PlanRoom_RechazaSolaparSalasYSuAnillo()
        {
            Assert.AreEqual(RoomPlanResult.TouchesRoom, planner.PlanRoom(room, new Vector2Int(15, 39)), "encima de la Guardería");

            // Guardería terminada: x 35–55, y 39–50; zona de acceso izquierda x 32–34, y 39–41; anillo x 33–34 e y 51–52.
            Assert.AreEqual(RoomPlanResult.TouchesRoom, planner.PlanRoom(room, new Vector2Int(12, 39)), "huella nueva sobre la zona de la Guardería");
            Assert.AreEqual(RoomPlanResult.TouchesRoom, planner.PlanRoom(room, new Vector2Int(12, 42)), "anillo nuevo (y 40–41) sobre la zona de la Guardería");
            Assert.AreEqual(RoomPlanResult.AccessBlocked, planner.PlanRoom(room, new Vector2Int(12, 45)), "zona nueva sobre la huella y el anillo de la Guardería");
            Assert.AreEqual(RoomPlanResult.AccessBlocked, planner.PlanRoom(room, new Vector2Int(12, 51)), "zona nueva (y 51–53) sobre el anillo de la Guardería");

            // Sala A en plano: x 3–23, y 3–14; zona de acceso derecha x 24–26, y 3–5; anillo x 1–25, y 1–16.
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 3)));
            Assert.AreEqual(RoomPlanResult.TouchesRoom, planner.PlanRoom(room, new Vector2Int(24, 3)), "huella nueva sobre la zona de A");
            Assert.AreEqual(RoomPlanResult.TouchesRoom, planner.PlanRoom(room, new Vector2Int(27, 6)), "anillo nuevo (y 4–5) sobre la zona de A");
            Assert.AreEqual(RoomPlanResult.AccessBlocked, planner.PlanRoom(room, new Vector2Int(27, 15)), "zona nueva (x 24–26, y 15–17) sobre el anillo de A");
            // Separada de A solo por la zona x 24–26, que es de las dos (decisiones.md: zona compartida).
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(27, 3)), "zona de acceso compartida");

            Assert.AreEqual(RoomPlanResult.OutOfTerrain, planner.PlanRoom(room, new Vector2Int(3, 72)));

            grid.Set(new Vector2Int(10, 57), CellType.Dug);   // en el anillo de una sala en (3, 45)
            Assert.AreEqual(RoomPlanResult.TooCloseToTunnel, planner.PlanRoom(room, new Vector2Int(3, 45)));
            Assert.AreEqual("Muy cerca de un túnel", DigPlanner.Describe(RoomPlanResult.TooCloseToTunnel));
        }

        [Test]
        public void Sala_SinAccesoHastaEmpezarLaZona_YAlTerminarEsRoomYPublicaRoomDug()
        {
            var origin = new Vector2Int(3, 60);
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, origin));
            Shaft();
            Assert.AreEqual(RoomDigState.NoAccess, State(origin));
            var ant = NewAnt(25, 61);
            Assert.IsFalse(planner.HasWork, "sin zona de acceso no se trabaja");

            planner.PaintBrush(At(25, 61));   // zona de acceso derecha, pegada al pozo
            Assert.AreEqual(RoomDigState.InProgress, State(origin));

            RoomDug? dug = null;
            Action<RoomDug> onDug = e => dug = e;
            EventBus.Subscribe(onDug);
            try
            {
                for (int i = 0; i < 100 && Claim(ant, out var job); i++)
                {
                    job.Work(1000f, 1f);
                    Tick();
                    job.Release(ant);
                }
            }
            finally { EventBus.Unsubscribe(onDug); }

            Assert.IsTrue(dug.HasValue, "RoomDug");
            Assert.AreEqual(origin, dug.Value.Origin);
            Assert.AreSame(room, dug.Value.Room);
            foreach (var c in new RectInt(origin, room.footprint).allPositionsWithin)
                Assert.AreEqual(CellType.Room, grid.Get(c), c.ToString());
            Assert.AreEqual(RoomDigState.Done, State(origin));
        }

        [Test]
        public void DosObreras_EnElMismoBloque_SumanSuFuerza()
        {
            Shaft();
            PaintChunk(10, 20);
            Assert.IsTrue(Claim(NewAnt(31, 61), out var a));
            Assert.IsTrue(Claim(NewAnt(31, 61), out var b));
            Assert.AreSame(a, b);
            Assert.AreEqual(2, a.Workers);
            Assert.IsFalse(Claim(NewAnt(31, 61), out _), "tercera obrera");

            a.Work(1f, 0.5f);
            b.Work(1f, 0.5f);
            Assert.AreEqual(1f / 1.5f, a.Progress, Eps);
        }

        [Test]
        public void BloqueDeSala_EnRocaCuestaElDobleQueEnTierra()
        {
            Shaft();
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 60)));
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 3)));
            Dig(new RectInt(24, 60, 3, 3));   // zonas de acceso excavadas
            Dig(new RectInt(24, 3, 3, 3));

            Assert.IsTrue(Claim(NewAnt(25, 61), out var soil));
            Assert.IsTrue(Claim(NewAnt(25, 4), out var rock));
            Assert.AreEqual(new Vector2Int(7, 20), soil.Chunk);
            Assert.AreEqual(new Vector2Int(7, 1), rock.Chunk);
            Assert.AreEqual(30f / 28f, CostOf(soil), Eps, "workCost entre 7 × 4 bloques");
            Assert.AreEqual(2f * CostOf(soil), CostOf(rock), Eps);
        }

        [Test]
        public void Integracion_DosObreras_PidenRuta_LleganYExcavanTodoElTunel()
        {
            Shaft();
            planner.PaintStroke(At(30, 61), At(45, 61));
            DigAllWithTwoWorkers(NewAnt(28, 70), NewAnt(28, 50));
        }

        [Test]
        public void TunelQueCruzaDosFilasDeBloques_SeExcavaEntero()
        {
            Shaft();
            planner.PaintStroke(At(30, 63), At(45, 63));   // casillas y 62–64: bloques de las filas 20 (60–62) y 21 (63–65)
            DigAllWithTwoWorkers(NewAnt(28, 70), NewAnt(28, 50));
        }

        [Test]
        public void HaciaAbajoDesdeElFondoDelPozo_LlegaAlFinalDelPlano()
        {
            // Pozo del mapa inicial en x 59–61, desalineado con los bloques (57–59 / 60–62); su fondo es la fila 39.
            planner.PaintStroke(At(60, 38), At(60, 18));
            DigAllWithTwoWorkers(NewAnt(60, 50), NewAnt(60, 60));
        }

        [Test]
        public void HormigaEnUnaSalaAislada_NoRecibeTrabajo()
        {
            Dig(new RectInt(27, 60, 3, 18));   // pozo de prueba hasta la superficie
            PaintChunk(10, 20);
            foreach (var c in new RectInt(56, 39, 3, 3).allPositionsWithin) grid.Set(c, CellType.Solid);   // tapa la puerta de la Guardería
            Assert.IsTrue(planner.HasWork);

            Assert.IsFalse(Claim(NewAnt(45, 40), out var job));
            Assert.IsNull(job);
            Assert.IsTrue(Claim(NewAnt(31, 61), out _), "una hormiga conectada sí lo recibe");
        }

        [Test]
        public void SalaConWorkCost0_NoLanza_YAvisaUnaSolaVez()
        {
            room.workCost = 0f;
            Shaft();
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 60)));
            Dig(new RectInt(24, 60, 3, 3));   // zona de acceso excavada

            int warnings = 0;
            Application.LogCallback count = (msg, _, type) => { if (type == LogType.Warning && msg.Contains("workCost")) warnings++; };
            Application.logMessageReceived += count;
            try
            {
                var ant = NewAnt(25, 61);
                for (int i = 0; i < 3; i++)
                {
                    Assert.IsTrue(Claim(ant, out var job), $"bloque {i}");
                    job.Work(1f, 1f);
                    Tick();
                    job.Release(ant);
                }
            }
            finally { Application.logMessageReceived -= count; }
            Assert.AreEqual(1, warnings);
        }

        [Test]
        public void BloqueSinCasillasEnPlano_NoCreaTrabajo()
        {
            Shaft();
            Assert.IsFalse(planner.IsPlanned(new Vector2Int(31, 61)));   // de paso construye los arrays
            int chunksX = (grid.Width + NestGrid.ChunkSize - 1) / NestGrid.ChunkSize;
            var create = typeof(DigPlanner).GetMethod("CreateJob", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNull(create.Invoke(planner, new object[] { 20 * chunksX + 10, new Vector2Int(28, 61) }));
        }

        [Test]
        public void Reparto_PorDistanciaDeRuta_NoEnLineaRecta()
        {
            Shaft();
            Dig(new RectInt(36, 58, 3, 20));   // otro pozo hasta la superficie, separado del bloque por la tierra de x 33–35
            PaintChunk(10, 20);                // x 30–32, y 60–62; la obrera se pone en (28, 61)
            var near = NewAnt(28, 64);         // 3 pasos
            var second = NewAnt(28, 72);       // 11 pasos y 11 casillas en línea recta
            var wall = NewAnt(37, 61);         // 9 casillas en línea recta, pero hay que subir a la superficie y bajar
            var far = NewAnt(28, 10);          // 51 pasos

            foreach (var ant in new[] { wall, far, second, near }) Assert.IsFalse(planner.TryClaim(ant, out _));
            Call(planner, "Assign");
            Assert.IsTrue(planner.TryClaim(near, out var job));
            Assert.IsTrue(planner.TryClaim(second, out var other));
            Assert.AreSame(job, other);
            Assert.IsFalse(planner.TryClaim(wall, out _), "al otro lado de la pared");
            Assert.IsFalse(planner.TryClaim(far, out _));
        }

        [Test]
        public void Reparto_DosBloques_CadaUnoRecibeSusDosMasCercanas()
        {
            Shaft();
            PaintChunk(10, 20);   // y 60–62
            PaintChunk(10, 4);    // y 12–14
            var top = new[] { NewAnt(28, 66), NewAnt(28, 55) };
            var bottom = new[] { NewAnt(28, 18), NewAnt(28, 7) };

            foreach (var ant in new[] { bottom[1], top[1], bottom[0], top[0] }) Assert.IsFalse(planner.TryClaim(ant, out _));
            Call(planner, "Assign");
            foreach (var ant in top)
            {
                Assert.IsTrue(planner.TryClaim(ant, out var job));
                Assert.AreEqual(new Vector2Int(10, 20), job.Chunk, ant.transform.position.ToString());
            }
            foreach (var ant in bottom)
            {
                Assert.IsTrue(planner.TryClaim(ant, out var job));
                Assert.AreEqual(new Vector2Int(10, 4), job.Chunk, ant.transform.position.ToString());
            }
        }

        [Test]
        public void ObreraExcavando_NoCambiaDeBloque_AlAparecerOtroMasCerca()
        {
            Shaft();
            PaintChunk(10, 4);
            var ant = NewAnt(28, 61);
            Assert.IsTrue(Claim(ant, out var job));
            job.Work(1f, 0.1f);

            PaintChunk(10, 20);   // justo al lado de la obrera
            Call(planner, "Assign");
            Assert.IsTrue(planner.TryClaim(ant, out var again));
            Assert.AreSame(job, again);
            Assert.AreEqual(1, job.Workers);
        }

        [Test]
        public void ObreraElegidaQueNoVuelveAPedir_PierdeElHueco_YLoCogeOtra()
        {
            Shaft();
            PaintChunk(10, 20);
            var gone = NewAnt(28, 64);      // las dos más cercanas
            var stays = NewAnt(28, 66);
            var waiting = NewAnt(28, 72);

            foreach (var ant in new[] { gone, stays, waiting }) Assert.IsFalse(planner.TryClaim(ant, out _));
            Call(planner, "Assign");
            Assert.IsTrue(planner.TryClaim(stays, out var job));
            Assert.IsFalse(planner.TryClaim(waiting, out _), "aún no le toca");

            Call(planner, "Assign");   // 'gone' no ha recogido lo suyo
            Assert.IsTrue(planner.TryClaim(waiting, out var taken));
            Assert.AreSame(job, taken);
            Assert.IsFalse(planner.TryClaim(gone, out _), "llega tarde");
        }

        /// <summary>
        /// Simula a dos obreras como DigTestDriver: TryClaim → ruta → se colocan en su último punto → comprueban que han llegado
        /// → Work en bucle. Al final todo el plano tiene que estar excavado.
        /// </summary>
        private void DigAllWithTwoWorkers(params Ant[] workers)
        {
            var plan = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (planner.IsPlanned(new Vector2Int(x, y))) plan.Add(new Vector2Int(x, y));
            Assert.Greater(plan.Count, 0);

            Simulate(workers, null);

            foreach (var c in plan) Assert.IsTrue(NestGrid.IsOpen(grid.Get(c)), c.ToString());   // Dug o, en una sala, Room
            Assert.IsFalse(planner.HasWork);
        }

        /// <summary>
        /// Bucle de DigTestDriver: cada obrera sin bloque pide; con bloque, trabaja; al terminarlo lo suelta y pide en el
        /// mismo frame (excavación continua); si pierde su reserva (HasWorker false), igual. Si se pasa 'diggers', apunta
        /// qué obreras han cogido cada bloque. Devuelve los frames seguidos más largos que una obrera pasó sin bloque
        /// después de tener el primero.
        /// </summary>
        private int Simulate(Ant[] workers, Dictionary<Vector2Int, HashSet<Ant>> diggers)
        {
            var jobs = new DigJob[workers.Length];
            var idle = new int[workers.Length];
            var started = new bool[workers.Length];
            var path = new List<Vector3>();
            bool busy = false;
            int maxIdle = 0;
            for (int frame = 0; frame < 5000 && (busy || planner.HasWork); frame++)
            {
                for (int i = 0; i < workers.Length; i++)
                {
                    if (jobs[i] == null && !Take(workers[i], out jobs[i], path, diggers))
                    {
                        if (started[i]) maxIdle = Mathf.Max(maxIdle, ++idle[i]);
                        continue;
                    }
                    started[i] = true;
                    idle[i] = 0;
                    jobs[i].Work(workers[i], 1f, 0.1f);   // dt de DigTestDriver
                }
                Tick();
                busy = false;
                for (int i = 0; i < workers.Length; i++)
                {
                    if (jobs[i] != null && (jobs[i].IsDone || !jobs[i].HasWorker(workers[i])))
                    {
                        jobs[i].Release(workers[i]);
                        Take(workers[i], out jobs[i], path, diggers);
                    }
                    busy |= jobs[i] != null;
                }
            }
            return maxIdle;
        }

        // TryClaim, ruta hasta el sitio (sin pasar por una sala fuera de su suelo) y la obrera colocada en su final.
        private bool Take(Ant ant, out DigJob job, List<Vector3> path, Dictionary<Vector2Int, HashSet<Ant>> diggers)
        {
            const float workDistance = 0.15f;   // el de DigTestDriver
            if (!planner.TryClaim(ant, out job)) return false;
            if (diggers != null)
            {
                if (!diggers.TryGetValue(job.Chunk, out var set)) diggers[job.Chunk] = set = new HashSet<Ant>();
                set.Add(ant);
            }
            Assert.IsTrue(nav.TryGetPath(ant.transform.position, job.StandPosition, path), $"ruta al bloque {job.Chunk}");
            // Salas con gravedad: ninguna ruta pasa por una sala (en obras o terminada) fuera de su suelo.
            for (int p = 1; p < path.Count; p++)
                for (int s = 0; s <= 4; s++)
                    Assert.IsFalse(nav.OffRoomFloor(Vector3.Lerp(path[p - 1], path[p], s / 4f), out _), $"ruta al bloque {job.Chunk}, tramo {p}");
            ant.transform.position = path[path.Count - 1];
            float far = Vector2.Distance(ant.transform.position, job.StandPosition);
            Assert.LessOrEqual(far, workDistance, $"la ruta al bloque {job.Chunk} acaba a {far:0.00} u de StandPosition");
            return true;
        }

        [Test]
        public void BloqueDeLaZonaDeAcceso_NoPoneASuObreraEnLaHuellaDeLaSalaEnPlano()
        {
            // Sala entre el pozo de prueba (x 27–29) y el del mapa inicial (x 59–61): huella x 33–53, y 60–71.
            Shaft();
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(33, 60)));
            PaintChunk(18, 20);                   // zona de acceso derecha, x 54–56, y 60–62
            Dig(new RectInt(30, 60, 24, 3));      // zona de acceso izquierda (x 30–32) y capa 0 de la sala, desde el pozo de prueba
            Dig(new RectInt(57, 60, 2, 3));       // hueco entre la zona de acceso derecha y el pozo del mapa
            // Con gravedad, dentro de la huella solo hay nodos en su suelo (y = 61), y se llega por la puerta izquierda.
            // (53, 61) queda a 1 casilla del bloque y (58, 61), fuera, a 2: sin el arreglo ganaría el de dentro.
            Assert.IsTrue(nav.IsReachable(new Vector2Int(53, 61)), "nodo alcanzable dentro de la huella");
            Assert.IsTrue(nav.IsReachable(new Vector2Int(58, 61)), "nodo alcanzable fuera");

            Assert.IsTrue(Workable(18, 20, out var stand));
            Assert.IsFalse(new RectInt(33, 60, 21, 12).Contains(stand), $"sitio de la obrera {stand}, dentro de la huella");
        }

        [Test]
        public void SalaPorCapas_LaCapa1EsperaATodaLaCapa0_YSeTrabajaDesdeElSuelo()
        {
            Shaft();
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, new Vector2Int(3, 60)));   // bloques x 1–7, capas y 20–23
            Dig(new RectInt(24, 60, 3, 3));   // zona de acceso derecha

            Assert.IsTrue(Workable(7, 20, out var stand), "capa 0, junto a la puerta");
            Assert.AreEqual(new Vector2Int(25, 61), stand, "en el suelo de la zona de acceso, lo más cerca en x");
            Assert.IsFalse(Workable(6, 20, out _), "capa 0, aún sin vecino excavado");

            Dig(new RectInt(9, 60, 15, 3));   // capa 0 a medias: bloques 3–7
            Assert.IsTrue(Workable(2, 20, out _), "capa 0 avanza hacia dentro");
            for (int cx = 1; cx <= 7; cx++) Assert.IsFalse(Workable(cx, 21, out _), $"capa 1, bloque {cx}, con la capa 0 a medias");

            Dig(new RectInt(3, 60, 6, 3));    // capa 0 entera
            for (int cx = 1; cx <= 7; cx++)
            {
                Assert.IsTrue(Workable(cx, 21, out stand), $"capa 1, bloque {cx}");
                Assert.AreEqual(new Vector2Int(cx * 3 + 1, 61), stand, "de pie en el suelo, en su columna");
                Assert.IsFalse(Workable(cx, 22, out _), $"capa 2, bloque {cx}");
            }
        }

        [Test]
        public void SalaConGravedad_DosObreras_LaExcavanEnteraDesdeElSuelo_YPasaARoom()
        {
            Shaft();
            var origin = new Vector2Int(3, 60);
            Assert.AreEqual(RoomPlanResult.Ok, planner.PlanRoom(room, origin));
            planner.PaintBrush(At(25, 61));   // zona de acceso derecha
            DigAllWithTwoWorkers(NewAnt(28, 70), NewAnt(28, 50));   // comprueba también que ninguna ruta sale del suelo
            foreach (var c in new RectInt(origin, room.footprint).allPositionsWithin)
                Assert.AreEqual(CellType.Room, grid.Get(c), c.ToString());
        }

        [Test]
        public void TunelDe6Bloques_Con4Obreras_LoCavaEnteroLaParejaQueEmpezo()
        {
            Shaft();
            planner.PaintStroke(At(30, 61), At(46, 61));   // bloques x 10–15 de la fila 20
            var ants = new[] { NewAnt(28, 64), NewAnt(28, 66), NewAnt(28, 72), NewAnt(28, 74) };
            var diggers = new Dictionary<Vector2Int, HashSet<Ant>>();
            Simulate(ants, diggers);

            Assert.AreEqual(6, diggers.Count, string.Join(", ", diggers.Keys));
            HashSet<Ant> pair = diggers[new Vector2Int(10, 20)];
            Assert.AreEqual(2, pair.Count, "empieza una pareja");
            for (int cx = 10; cx <= 15; cx++)
                Assert.IsTrue(pair.SetEquals(diggers[new Vector2Int(cx, 20)]), $"bloque {cx}: sin relevos");
        }

        [Test]
        public void AlTerminarUnBloque_TieneElSiguienteEnElMismoFrame()
        {
            Shaft();
            planner.PaintStroke(At(30, 61), At(37, 61));   // bloques 10–12
            var ant = NewAnt(28, 64);
            Assert.IsTrue(Claim(ant, out var job));
            Assert.AreEqual(new Vector2Int(10, 20), job.Chunk);

            job.Work(1f, 10f);   // terminado; ni Update del planificador ni reparto
            Assert.IsTrue(planner.TryClaim(ant, out var next), "sin pasar por libre");
            Assert.AreEqual(new Vector2Int(11, 20), next.Chunk);
            Assert.IsFalse(next.IsDone);
            Assert.AreEqual(1, next.Workers);
            Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(32, 61)), "el terminado ya está vaciado");
        }

        [Test]
        public void AlAcabarseElPlano_LaObreraQuedaLibre_YElRepartoLaUsaEnOtro()
        {
            Shaft();
            PaintChunk(10, 20);
            PaintChunk(10, 4);   // otro plano, lejos
            var ant = NewAnt(28, 64);
            Assert.IsTrue(Claim(ant, out var job));
            Assert.AreEqual(new Vector2Int(10, 20), job.Chunk);

            job.Work(1f, 10f);
            Assert.IsFalse(planner.TryClaim(ant, out _), "sin bloque contiguo: libre");
            Call(planner, "Assign");
            Assert.IsTrue(planner.TryClaim(ant, out var other));
            Assert.AreEqual(new Vector2Int(10, 4), other.Chunk);
        }

        [Test]
        public void ReservaDelSiguiente_LlegaConElProgreso_YSeSueltaSiLaObreraLoDejaAMedias()
        {
            Shaft();
            planner.PaintStroke(At(30, 61), At(46, 61));
            var ant = NewAnt(28, 64);
            Assert.IsTrue(Claim(ant, out var job));
            var reserved = (System.Collections.IDictionary)typeof(DigPlanner)
                .GetField("nextJobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(planner);

            job.Work(1f, 0.9f);   // 60 %: aún no
            Tick();
            Assert.AreEqual(0, reserved.Count);
            job.Work(1f, 0.3f);   // 80 %: la 7.ª casilla abre el paso al bloque 11
            Tick();
            Assert.AreEqual(1, reserved.Count, "reservado al llegar a lookAheadProgress");

            job.Release(ant);     // B6 la manda a comer
            Tick();
            Assert.AreEqual(0, reserved.Count, "pierde la reserva y su hueco vuelve al reparto");
        }

        [Test]
        public void BloqueReservadoPorDosLejanas_LoCogeLaLibreDeAlLado()
        {
            Shaft();
            PaintChunk(10, 20);                                    // sitio en (28, 61)
            var far = new[] { NewAnt(28, 10), NewAnt(28, 14) };   // de camino, sin llegar nunca
            Assert.IsTrue(Claim(far[0], out var job));
            Assert.IsTrue(Claim(far[1], out var same));
            Assert.AreSame(job, same);

            var near = NewAnt(28, 64);
            Assert.IsTrue(Claim(near, out var taken), "se lo queda la de al lado");
            Assert.AreSame(job, taken);
            Assert.IsFalse(job.HasWorker(far[0]), "la más lejana lo pierde");
            Assert.IsTrue(job.HasWorker(far[1]));
            Assert.AreEqual(2, job.Workers);
        }

        [Test]
        public void TunelACaballo_UnaObreraSola_LoCavaEnteroSinParar()
        {
            Shaft();
            planner.PaintStroke(At(30, 63), At(45, 63));   // casillas y 62–64: bloques de las filas 20 y 21
            int idle = Simulate(new[] { NewAnt(28, 70) }, null);
            Assert.LessOrEqual(idle, 1, "frames seguidos sin bloque");
            Assert.IsFalse(planner.HasWork);
            for (int x = 30; x <= 45; x++) Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(x, 63)), $"({x}, 63)");
        }

        [Test]
        public void TunelACaballo_LaOtraMitadReservadaPorLejanas_LaObreraDeAlLadoNoSePara()
        {
            Shaft();
            planner.PaintStroke(At(30, 63), At(45, 63));
            var ant = NewAnt(28, 62);
            Assert.IsTrue(Claim(ant, out var first));
            // Tres lejanas que no llegan nunca: se reservan los huecos que quedan cerca del frente.
            foreach (var far in new[] { NewAnt(28, 10), NewAnt(28, 12), NewAnt(28, 14) }) Claim(far, out _);

            int idle = Simulate(new[] { ant }, null);
            Assert.LessOrEqual(idle, 1, "frames seguidos sin bloque");
            Assert.IsFalse(planner.HasWork);
            for (int x = 30; x <= 45; x++) Assert.AreEqual(CellType.Dug, grid.Get(new Vector2Int(x, 63)), $"({x}, 63)");
        }

        [Test]
        public void ObreraExcavando_NuncaPierdeSuBloque()
        {
            Shaft();
            PaintChunk(10, 20);
            var digger = NewAnt(28, 64);
            Assert.IsTrue(Claim(digger, out var job));
            digger.transform.position = job.StandPosition;
            job.Work(digger, 1f, 0.1f);

            var second = NewAnt(28, 64);
            Assert.IsTrue(Claim(second, out var same));
            Assert.AreSame(job, same);
            second.transform.position = job.StandPosition;   // llegada; aún sin Work(ant): cuenta por distancia

            var other = NewAnt(28, 61);
            other.transform.position = job.StandPosition;    // libre y en el mismo sitio: no es "más cerca"
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(Claim(other, out _));
                job.Work(digger, 1f, 0.01f);
            }
            Assert.IsTrue(job.HasWorker(digger));
            Assert.IsTrue(job.HasWorker(second));
        }

        [Test]
        public void ObreraQueLlamaAWorkLejosDeSuSitio_NoPierdeSuBloque()
        {
            Shaft();
            PaintChunk(10, 20);
            var digger = NewAnt(28, 64);
            Assert.IsTrue(Claim(digger, out var job));
            digger.transform.position = job.StandPosition + Vector3.up * 0.5f;   // 2 casillas por encima del sitio
            job.Work(digger, 1f, 0.1f);
            var far = NewAnt(28, 10);
            Assert.IsTrue(Claim(far, out _), "segundo hueco, de camino");

            var near = NewAnt(28, 61);   // libre, en el sitio: más cerca que las dos
            Assert.IsTrue(Claim(near, out var taken));
            Assert.AreSame(job, taken);
            Assert.IsTrue(job.HasWorker(digger), "excava (Work(ant, …)) aunque esté a 0,5 u");
            Assert.IsFalse(job.HasWorker(far), "la de camino sí lo cede");
        }

        // ================= AUXILIARES =================

        // IsWorkable del bloque (cx, cy), sin pasar por el reparto.
        private bool Workable(int cx, int cy, out Vector2Int stand)
        {
            int k = (int)typeof(DigPlanner).GetMethod("ChunkIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(planner, new object[] { new Vector2Int(cx * 3, cy * 3) });
            var args = new object[] { k, null };
            bool ok = (bool)typeof(DigPlanner).GetMethod("IsWorkable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(planner, args);
            stand = (Vector2Int)args[1];
            return ok;
        }

        private void Shaft() => Dig(new RectInt(27, 3, 3, 75));

        private void Dig(RectInt r)
        {
            foreach (var c in r.allPositionsWithin) grid.Set(c, CellType.Dug);
        }

        private void PaintChunk(int cx, int cy) => planner.PaintBrush(At(cx * 3 + 1, cy * 3 + 1));

        // Como una obrera que pide cada frame: la primera llamada la apunta, el reparto le guarda un bloque y la segunda lo recoge.
        private bool Claim(Ant ant, out DigJob job)
        {
            if (planner.TryClaim(ant, out job)) return true;
            Call(planner, "Assign");
            return planner.TryClaim(ant, out job);
        }

        private float ClaimCost(int cx, int cy)
        {
            PaintChunk(cx, cy);
            Assert.IsTrue(Claim(NewAnt(cx * 3 + 1, cy * 3 + 1), out var job));
            Assert.AreEqual(new Vector2Int(cx, cy), job.Chunk);
            return CostOf(job);
        }

        // Un trabajo sin empezar: un paso pequeño de fuerza 1 da el coste sin llegar a 1.
        private static float CostOf(DigJob job)
        {
            float before = job.Progress;
            job.Work(1f, 0.01f);
            return 0.01f / (job.Progress - before);
        }

        private RoomDigState State(Vector2Int origin)
        {
            Assert.IsTrue(planner.TryGetRoomState(origin, out var state, out _));
            return state;
        }

        private Ant NewAnt(int x, int y)
        {
            var ant = new GameObject("Ant (test)").AddComponent<Ant>();
            ant.transform.position = At(x, y);
            ants.Add(ant);
            return ant;
        }

        private Vector3 At(int x, int y) => grid.CellToWorld(new Vector2Int(x, y));

        private void Tick() => Call(planner, "Update");

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
