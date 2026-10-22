using System;
using System.Collections.Generic;
using Myrmutation.Core;
using Myrmutation.Core.Data;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>Resultado de PlanRoom. El texto para el jugador sale de DigPlanner.Describe.</summary>
    public enum RoomPlanResult { Ok, OutOfTerrain, TouchesRoom, AccessBlocked, TooCloseToTunnel }

    /// <summary>Estado de una sala en plano: sin zona de acceso empezada, en obras o terminada.</summary>
    public enum RoomDigState { NoAccess, InProgress, Done }

    /// <summary>
    /// Planos de excavación y trabajos por bloques 3×3 (diseno-construccion-beta.md §3 y §4). Implementa IDigJobs.
    /// Plano = casillas Solid marcadas con el pincel o huellas de sala. Un bloque es trabajable si hay un nodo alcanzable
    /// desde la colonia a StandReach casillas o menos (Chebyshev) de alguna de sus casillas en plano; los de sala, además,
    /// solo si tienen al lado su zona de acceso excavada o otro bloque suyo ya excavado. Los bloques se reparten entre las
    /// obreras que piden (Assign), el trabajo se crea al repartirlo y vacía sus casillas en Update, de la más cercana a la
    /// obrera a la más lejana.
    /// </summary>
    public class DigPlanner : MonoBehaviour, IDigJobs
    {
        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid grid;
        [Tooltip("Navegador de la rejilla. Si se deja vacío, usa Navigation.Current")]
        [SerializeField] private GridNavigator navigator;

        [Header("Coste")]
        [Tooltip("Trabajo de un bloque de túnel entero de tierra (fuerza × segundos)")]
        [SerializeField] private float tunnelChunkCost = 1.5f;
        [Tooltip("Multiplicador del coste en arcilla")]
        [SerializeField] private float clayMultiplier = 1.33f;
        [Tooltip("Multiplicador del coste en roca")]
        [SerializeField] private float rockMultiplier = 2f;
        [Tooltip("Subida del coste de las salas por cada sala ya construida (0,25 = +25 %)")]
        [SerializeField] private float roomCostIncrease = 0.25f;

        [Header("Reparto")]
        [Tooltip("Segundos (sin escala) entre repartos de bloques a las obreras libres. También se reparte en el frame siguiente si cambia el plano, se termina o cancela un bloque o pide una obrera nueva")]
        [SerializeField] private float assignInterval = 0.5f;

        // Coste mínimo de un bloque si los datos lo dejan en 0 o menos (workCost 0): DigJob no admite coste 0.
        private const float MinCost = 0.01f;
        private const int ChunkCells = NestGrid.ChunkSize * NestGrid.ChunkSize;
        // §4: distancia máxima (Chebyshev) del nodo de la obrera a una casilla en plano del bloque.
        private const int StandReach = 2;
        private static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        private enum Part { None, Footprint, Ring, Access }

        private sealed class PlannedRoom
        {
            public RoomData Data;
            public RectInt Rect;
            public bool Done;
        }

        private sealed class Job
        {
            public DigJob Dig;
            public int Chunk;
            public PlannedRoom Room;
            public Vector2Int Stand;
            // Casillas a vaciar, de la más cercana a la obrera a la más lejana.
            public readonly Vector2Int[] Order = new Vector2Int[ChunkCells];
            public int Count, Applied;
        }

        // Bloque con hueco en un reparto: con trabajo (Job) o trabajable aún sin él (Chunk, Stand).
        private struct Candidate
        {
            public Job Job;
            public int Chunk, Slots;
            public Vector2Int Stand;
        }

        // Invariante: una casilla en plano siempre es Solid (al cambiar de tipo sale del plano).
        private bool[] planned;
        private int[] plannedPerChunk;
        private PlannedRoom[] roomOfChunk;
        private int width, chunksX, chunksY;

        private readonly List<PlannedRoom> rooms = new List<PlannedRoom>();
        private readonly List<int> plannedChunks = new List<int>();
        private readonly Dictionary<int, Job> jobs = new Dictionary<int, Job>();
        private readonly List<Job> active = new List<Job>();
        // Bloques trabajables que aún no tienen trabajo, con su casilla para la obrera. Se rehace si algo cambia.
        private readonly List<(int chunk, Vector2Int stand)> workable = new List<(int, Vector2Int)>();
        private bool workableDirty = true;
        private int roomsBuilt;

        // Reparto: cuándo pidió trabajo por última vez cada obrera libre (sin escala) y qué le guardó el último reparto.
        private readonly Dictionary<Ant, float> askers = new Dictionary<Ant, float>();
        private readonly Dictionary<Ant, Job> offers = new Dictionary<Ant, Job>();
        private readonly List<Ant> free = new List<Ant>();
        private readonly List<Vector2Int> freeNodes = new List<Vector2Int>();
        private readonly List<Ant> stale = new List<Ant>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<int> routeSteps = new List<int>();   // pasos de cada candidato a cada obrera libre
        private readonly HashSet<RoomData> warnedCost = new HashSet<RoomData>();
        private float assignTimer;
        private bool assignDirty;

        // Copia del plano al entrar en el modo Construcción (BeginEdit), para poder cancelar.
        private bool[] savedPlanned;
        private readonly List<PlannedRoom> savedRooms = new List<PlannedRoom>();

        /// <summary>Una casilla entra o sale del plano (para el dibujo del plano azul).</summary>
        public event Action<Vector2Int> PlannedChanged;

        /// <summary>Hay una edición abierta (entre BeginEdit y Commit o Revert).</summary>
        public bool Editing { get; private set; }

        /// <summary>Bloques con alguna casilla en plano.</summary>
        public int PlannedChunks => plannedChunks.Count;

        /// <summary>Salas planificadas, terminadas o no (para las etiquetas de obra).</summary>
        public int RoomCount => rooms.Count;

        private void Awake() => Digging.Current = this;

        private void OnDestroy()
        {
            if (ReferenceEquals(Digging.Current, this)) Digging.Current = null;
        }

        private void OnEnable()
        {
            if (grid == null) grid = NestGrid.Instance;
            if (grid != null) grid.CellChanged += OnCellChanged;
        }

        private void OnDisable()
        {
            if (grid != null) grid.CellChanged -= OnCellChanged;
        }

        private void Update()
        {
            // Hacia atrás: Apply puede quitar su propio trabajo de la lista.
            for (int i = active.Count - 1; i >= 0; i--)
                if (i < active.Count) Apply(active[i]);

            assignTimer += Time.unscaledDeltaTime;
            if (assignDirty || assignTimer >= assignInterval) Assign();
        }

        // ================= CONTRATO =================

        public bool HasWork
        {
            get
            {
                if (!EnsureBuilt()) return false;
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Dig.CanClaim) return true;
                RefreshWorkable();
                return workable.Count > 0;
            }
        }

        /// <summary>
        /// Devuelve el bloque sin terminar que ya tiene la hormiga o reserva el que le guardó el último reparto (Assign).
        /// Si no, la apunta como libre para el siguiente y devuelve false. También false, sin apuntarla, si el nodo más
        /// cercano a la hormiga no conecta con la colonia (atrapada en tierra o en una sala sin salida).
        /// </summary>
        public bool TryClaim(Ant ant, out DigJob job)
        {
            job = null;
            if (ant == null || !EnsureBuilt()) return false;
            // La que ya excava un bloque sigue con él aunque aparezca otro más cerca.
            for (int i = 0; i < active.Count; i++)
                if (active[i].Dig.HasWorker(ant) && !active[i].Dig.IsDone) { job = active[i].Dig; return true; }
            if (!navigator.Nearest(ant.transform.position, out var node) || !navigator.IsReachable(node)) return false;

            if (!askers.ContainsKey(ant)) assignDirty = true;   // obrera nueva: se reparte en el frame siguiente
            askers[ant] = Time.unscaledTime;
            if (!offers.TryGetValue(ant, out var offer)) return false;
            offers.Remove(ant);
            if (!offer.Dig.Claim(ant)) return false;   // cancelado o terminado desde el reparto: sigue libre
            askers.Remove(ant);
            job = offer.Dig;
            return true;
        }

        /// <summary>
        /// Reparte los bloques con hueco entre las obreras libres (han pedido hace poco y no tienen bloque): cada vez, el par
        /// bloque–obrera más cercano por ruta (pasos de una búsqueda en anchura desde el sitio de la obrera del bloque),
        /// hasta MaxWorkers por bloque. Lo guardado caduca en el siguiente reparto si la obrera no lo recoge con TryClaim.
        /// shortcut: una búsqueda por bloque candidato en cada reparto con obreras libres; si hay muchos bloques en plano,
        /// buscar desde las obreras cuando sean menos que los bloques.
        /// </summary>
        private void Assign()
        {
            assignTimer = 0f;
            assignDirty = false;
            if (!EnsureBuilt()) return;

            // Las que no han recogido lo suyo dejan de estar libres hasta que vuelvan a pedir: su hueco se reparte de nuevo.
            foreach (var pair in offers) askers.Remove(pair.Key);
            offers.Clear();

            free.Clear();
            freeNodes.Clear();
            stale.Clear();
            float now = Time.unscaledTime;
            foreach (var pair in askers)
            {
                // Sin pedir en dos repartos seguidos: B6 le ha dado otra tarea (o ha muerto).
                if (pair.Key != null && now - pair.Value <= assignInterval * 2f && navigator.Nearest(pair.Key.transform.position, out var node))
                {
                    free.Add(pair.Key);
                    freeNodes.Add(node);
                }
                else stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++) askers.Remove(stale[i]);
            if (free.Count == 0) return;

            RefreshWorkable();
            candidates.Clear();
            for (int i = 0; i < active.Count; i++)
                if (active[i].Dig.CanClaim)
                    candidates.Add(new Candidate { Job = active[i], Stand = active[i].Stand, Slots = DigJob.MaxWorkers - active[i].Dig.Workers });
            for (int i = 0; i < workable.Count; i++)
                candidates.Add(new Candidate { Chunk = workable[i].chunk, Stand = workable[i].stand, Slots = DigJob.MaxWorkers });
            if (candidates.Count == 0) return;

            int n = free.Count;
            routeSteps.Clear();
            for (int c = 0; c < candidates.Count; c++)
            {
                navigator.FloodSteps(candidates[c].Stand);
                for (int a = 0; a < n; a++) routeSteps.Add(navigator.StepsTo(freeNodes[a]));
            }

            // Cada vuelta quita una obrera o cierra un bloque, así que termina.
            while (true)
            {
                int best = int.MaxValue, bc = -1, ba = -1;
                for (int c = 0; c < candidates.Count; c++)
                {
                    if (candidates[c].Slots == 0) continue;
                    for (int a = 0; a < n; a++)
                    {
                        int s = routeSteps[c * n + a];
                        if (free[a] != null && s >= 0 && s < best) { best = s; bc = c; ba = a; }
                    }
                }
                if (bc < 0) return;

                Candidate cand = candidates[bc];
                if (cand.Job == null) cand.Job = CreateJob(cand.Chunk, cand.Stand);
                if (cand.Job == null) cand.Slots = 0;   // se quedó sin casillas en plano
                else
                {
                    cand.Slots--;
                    offers[free[ba]] = cand.Job;
                    free[ba] = null;
                }
                candidates[bc] = cand;
            }
        }

        // ================= PLANOS =================

        /// <summary>La casilla está en plano (túnel o huella de sala) y aún sin excavar.</summary>
        public bool IsPlanned(Vector2Int cell) => EnsureBuilt() && InGrid(cell) && planned[Index(cell)];

        /// <summary>Marca un círculo. Solo tierra Solid que no sea anillo de sala (hecha o en plano) ni huella de sala en plano.</summary>
        public void PaintBrush(Vector3 center, float radiusCells = 1.5f)
        {
            if (EnsureBuilt() && Stamp(center, radiusCells, true))
                EventBus.Publish(new BuildPlanned(null, grid.WorldToCell(center)));
        }

        /// <summary>Marca el trazo de a a b estampando el pincel cada media casilla.</summary>
        public void PaintStroke(Vector3 a, Vector3 b, float radiusCells = 1.5f)
        {
            if (EnsureBuilt() && Stroke(a, b, radiusCells, true))
                EventBus.Publish(new BuildPlanned(null, grid.WorldToCell(b)));
        }

        /// <summary>Quita del plano un círculo de túnel (no toca las salas, que se quitan con CancelRoom). No rellena lo excavado.</summary>
        public void Erase(Vector3 center, float radiusCells = 1.5f)
        {
            if (EnsureBuilt()) Stamp(center, radiusCells, false);
        }

        /// <summary>Como Erase, a lo largo del trazo de a a b (cada media casilla).</summary>
        public void EraseStroke(Vector3 a, Vector3 b, float radiusCells = 1.5f)
        {
            if (EnsureBuilt()) Stroke(a, b, radiusCells, false);
        }

        /// <summary>El pincel de túnel puede marcar la casilla: tierra Solid fuera del anillo y la huella de las salas.</summary>
        public bool CanPaint(Vector2Int c) =>
            EnsureBuilt() && InGrid(c) && grid.Get(c) == CellType.Solid && !grid.IsRoomWall(c)
            && PlannedRoomPart(c) is Part.None or Part.Access;

        private bool Stroke(Vector3 a, Vector3 b, float radius, bool paint)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / grid.CellSize * 2f);
            bool any = false;
            for (int s = 0; s <= steps; s++)
                any |= Stamp(Vector3.Lerp(a, b, steps == 0 ? 0f : s / (float)steps), radius, paint);
            return any;
        }

        private bool Stamp(Vector3 world, float radius, bool paint)
        {
            Vector2Int center = grid.WorldToCell(world);
            int r = Mathf.CeilToInt(radius);
            float r2 = radius * radius;
            bool changed = false;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dy);
                    if (((Vector2)(grid.CellToWorld(c) - world) / grid.CellSize).sqrMagnitude > r2 || !InGrid(c)) continue;
                    bool allowed = paint ? CanPaint(c) : roomOfChunk[ChunkIndex(c)] == null;
                    if (allowed) changed |= SetPlanned(c, paint);
                }
            assignDirty |= changed;
            return changed;
        }

        /// <summary>
        /// Planifica una sala con la esquina inferior izquierda de su huella en 'origin', redondeada hacia abajo a bloques.
        /// Reglas 1–4 del §3 contra las salas hechas y las que están en plano.
        /// </summary>
        public RoomPlanResult PlanRoom(RoomData data, Vector2Int origin)
        {
            RoomPlanResult result = CheckRoom(data, origin);
            if (result != RoomPlanResult.Ok) return result;
            RectInt rect = RoomRect(data, origin);
            RectInt left = AccessZone(rect, -1), right = AccessZone(rect, 1);
            RectInt outer = Grow(rect, NestGrid.RoomWallThickness);

            var room = new PlannedRoom { Data = data, Rect = rect };
            rooms.Add(room);
            foreach (var c in outer.allPositionsWithin)
            {
                if (left.Contains(c) || right.Contains(c) || !InGrid(c)) continue;
                if (rect.Contains(c))
                {
                    int k = ChunkIndex(c);
                    if (roomOfChunk[k] == null) { RemoveJob(k); roomOfChunk[k] = room; }
                }
                SetPlanned(c, rect.Contains(c));   // la huella entra en plano; el túnel en plano del anillo sale
            }
            assignDirty = true;
            EventBus.Publish(new BuildPlanned(data, rect.position));
            return RoomPlanResult.Ok;
        }

        /// <summary>Huella que tendría la sala con su esquina en 'origin' (redondeada hacia abajo a bloques, como PlanRoom).</summary>
        public static RectInt RoomRect(RoomData data, Vector2Int origin) =>
            new RectInt(Snap(origin.x), Snap(origin.y), data.footprint.x, data.footprint.y);

        /// <summary>Lo que devolvería PlanRoom, sin planificar nada (para el fantasma de A9).</summary>
        public RoomPlanResult CheckRoom(RoomData data, Vector2Int origin)
        {
            if (data == null || !EnsureBuilt()) return RoomPlanResult.OutOfTerrain;
            RectInt rect = RoomRect(data, origin);
            RectInt left = AccessZone(rect, -1), right = AccessZone(rect, 1);

            RectInt outer = Grow(rect, NestGrid.RoomWallThickness);
            foreach (var c in outer.allPositionsWithin)
            {
                CellType t = grid.Get(c);
                Part part = PlannedRoomPart(c);
                if (rect.Contains(c))
                {
                    if (t == CellType.Room || part != Part.None || grid.IsAccessZone(c)) return RoomPlanResult.TouchesRoom;
                    if (t == CellType.Dug) return RoomPlanResult.TooCloseToTunnel;
                    if (t != CellType.Solid) return RoomPlanResult.OutOfTerrain;
                }
                else if (!left.Contains(c) && !right.Contains(c))
                {
                    if (t == CellType.Room || part is Part.Footprint or Part.Access || grid.IsAccessZone(c)) return RoomPlanResult.TouchesRoom;
                    if (t == CellType.Dug) return RoomPlanResult.TooCloseToTunnel;
                }
            }
            return AccessFree(left) && AccessFree(right) ? RoomPlanResult.Ok : RoomPlanResult.AccessBlocked;
        }

        /// <summary>Sala i (0 a RoomCount - 1): sus datos y su huella. Su estado sale de TryGetRoomState(rect.position).</summary>
        public void GetRoom(int i, out RoomData data, out RectInt rect)
        {
            data = rooms[i].Data;
            rect = rooms[i].Rect;
        }

        private bool AccessFree(RectInt zone)
        {
            foreach (var c in zone.allPositionsWithin)
                if (grid.Get(c) == CellType.Room || grid.IsRoomWall(c) || PlannedRoomPart(c) is Part.Footprint or Part.Ring) return false;
            return true;
        }

        /// <summary>Quita del plano la sala sin terminar que contiene 'cell'. Lo ya excavado queda como túnel.</summary>
        public bool CancelRoom(Vector2Int cell)
        {
            if (!EnsureBuilt()) return false;
            PlannedRoom room = null;
            for (int i = 0; i < rooms.Count && room == null; i++)
                if (!rooms[i].Done && rooms[i].Rect.Contains(cell)) room = rooms[i];
            if (room == null) return false;
            rooms.Remove(room);
            foreach (var c in room.Rect.allPositionsWithin)
            {
                roomOfChunk[ChunkIndex(c)] = null;
                SetPlanned(c, false);
            }
            assignDirty = true;
            return true;
        }

        // ================= EDICIÓN (modo Construcción) =================

        /// <summary>Guarda una copia del plano (casillas y salas) para poder volver a ella con Revert.</summary>
        public void BeginEdit()
        {
            if (!EnsureBuilt()) return;
            if (savedPlanned == null || savedPlanned.Length != planned.Length) savedPlanned = new bool[planned.Length];
            Array.Copy(planned, savedPlanned, planned.Length);
            savedRooms.Clear();
            savedRooms.AddRange(rooms);
            Editing = true;
        }

        /// <summary>Se queda con el plano actual.</summary>
        public void Commit()
        {
            Editing = false;
            savedRooms.Clear();
        }

        /// <summary>
        /// Vuelve al plano guardado en BeginEdit. Lo excavado mientras tanto no se rellena (sus casillas ya no vuelven
        /// al plano) y las salas terminadas mientras tanto se quedan.
        /// </summary>
        public void Revert()
        {
            if (!Editing) return;
            Editing = false;
            for (int i = rooms.Count - 1; i >= 0; i--)
            {
                PlannedRoom r = rooms[i];
                if (r.Done || savedRooms.Contains(r)) continue;
                AssignRoomChunks(r, null);
                rooms.RemoveAt(i);
            }
            foreach (PlannedRoom r in savedRooms)
            {
                if (!rooms.Contains(r)) rooms.Add(r);
                if (!r.Done) AssignRoomChunks(r, r);
            }
            savedRooms.Clear();

            for (int i = 0; i < planned.Length; i++)
            {
                var c = new Vector2Int(i % width, i / width);
                SetPlanned(c, savedPlanned[i] && grid.Get(c) == CellType.Solid);
            }
            workableDirty = true;
            assignDirty = true;
        }

        private void AssignRoomChunks(PlannedRoom room, PlannedRoom value)
        {
            foreach (var c in room.Rect.allPositionsWithin)
            {
                if (!InGrid(c)) continue;
                int k = ChunkIndex(c);
                if (roomOfChunk[k] == value) continue;
                RemoveJob(k);
                roomOfChunk[k] = value;
            }
        }

        /// <summary>Estado de la sala planificada en 'origin' (el mismo que se pasó a PlanRoom). False si no hay ninguna.</summary>
        public bool TryGetRoomState(Vector2Int origin, out RoomDigState state, out float progress)
        {
            state = RoomDigState.NoAccess;
            progress = 0f;
            if (!EnsureBuilt()) return false;
            var pos = new Vector2Int(Snap(origin.x), Snap(origin.y));
            PlannedRoom room = null;
            for (int i = 0; i < rooms.Count && room == null; i++)
                if (rooms[i].Rect.position == pos) room = rooms[i];
            if (room == null) return false;
            if (room.Done) { state = RoomDigState.Done; progress = 1f; return true; }

            int dug = 0;
            foreach (var c in room.Rect.allPositionsWithin)
                if (grid.Get(c) != CellType.Solid) dug++;
            progress = dug / (float)(room.Rect.width * room.Rect.height);
            state = ZoneStarted(AccessZone(room.Rect, -1)) || ZoneStarted(AccessZone(room.Rect, 1))
                ? RoomDigState.InProgress : RoomDigState.NoAccess;
            return true;
        }

        // §4: la zona está excavada entera o tiene algo en plano.
        private bool ZoneStarted(RectInt zone)
        {
            bool open = true;
            foreach (var c in zone.allPositionsWithin)
            {
                if (IsPlanned(c)) return true;
                open &= NestGrid.IsOpen(grid.Get(c));
            }
            return open;
        }

        /// <summary>Texto para el jugador de cada motivo de PlanRoom (para A9).</summary>
        public static string Describe(RoomPlanResult result) => result switch
        {
            RoomPlanResult.OutOfTerrain => "Fuera del terreno",
            RoomPlanResult.TouchesRoom => "Muy cerca de otra sala",
            RoomPlanResult.AccessBlocked => "La entrada está ocupada",
            RoomPlanResult.TooCloseToTunnel => "Muy cerca de un túnel",
            _ => "",
        };

        // ================= BLOQUES Y TRABAJOS =================

        private bool SetPlanned(Vector2Int c, bool value)
        {
            int i = Index(c);
            if (planned[i] == value) return false;
            planned[i] = value;
            int k = ChunkIndex(c);
            if (value)
            {
                if (plannedPerChunk[k]++ == 0) plannedChunks.Add(k);
            }
            else if (--plannedPerChunk[k] == 0)
            {
                plannedChunks.Remove(k);
                RemoveJob(k);
            }
            workableDirty = true;
            PlannedChanged?.Invoke(c);
            return true;
        }

        private void OnCellChanged(Vector2Int cell)
        {
            if (planned == null) return;
            workableDirty = true;
            if (InGrid(cell) && planned[Index(cell)] && grid.Get(cell) != CellType.Solid) SetPlanned(cell, false);
        }

        private void RefreshWorkable()
        {
            if (!workableDirty) return;
            workableDirty = false;
            workable.Clear();
            for (int i = 0; i < plannedChunks.Count; i++)
            {
                int k = plannedChunks[i];
                if (!jobs.ContainsKey(k) && IsWorkable(k, out var stand)) workable.Add((k, stand));
            }
        }

        /// <summary>
        /// Busca un nodo alcanzable a StandReach casillas o menos (Chebyshev) de alguna casilla en plano del bloque: el más
        /// cercano a ellas y, a igualdad, al centro del bloque. Ahí se pone la obrera. No mira bloques vecinos enteros porque
        /// los túneles del pincel casi nunca van alineados con los bloques de 3.
        /// </summary>
        private bool IsWorkable(int k, out Vector2Int stand)
        {
            stand = default;
            Vector2Int chunk = ChunkCoord(k);
            PlannedRoom room = roomOfChunk[k];
            if (room != null)
            {
                bool start = false;
                foreach (var dir in Dirs)
                {
                    Vector2Int n = chunk + dir;
                    start |= n.x >= 0 && n.y >= 0 && n.x < chunksX && n.y < chunksY && RoomCanStartFrom(room, n);
                }
                if (!start) return false;
            }

            Vector2Int origin = chunk * NestGrid.ChunkSize;
            Vector2Int center = origin + Vector2Int.one;
            int best = int.MaxValue;
            for (int y = origin.y - StandReach; y < origin.y + NestGrid.ChunkSize + StandReach; y++)
                for (int x = origin.x - StandReach; x < origin.x + NestGrid.ChunkSize + StandReach; x++)
                {
                    var n = new Vector2Int(x, y);
                    if (!navigator.IsReachable(n)) continue;
                    int near = int.MaxValue;
                    for (int cy = 0; cy < NestGrid.ChunkSize; cy++)
                        for (int cx = 0; cx < NestGrid.ChunkSize; cx++)
                        {
                            var c = new Vector2Int(origin.x + cx, origin.y + cy);
                            Vector2Int d = c - n;
                            if (planned[Index(c)] && Mathf.Abs(d.x) <= StandReach && Mathf.Abs(d.y) <= StandReach)
                                near = Mathf.Min(near, d.sqrMagnitude);
                        }
                    if (near == int.MaxValue) continue;
                    // La distancia al centro (≤ 18 en esta ventana) solo desempata.
                    int score = near * 100 + (n - center).sqrMagnitude;
                    if (score < best) { best = score; stand = n; }
                }
            return best != int.MaxValue;
        }

        // Una sala avanza desde su zona de acceso o desde un bloque suyo, en los dos casos ya excavados enteros.
        private bool RoomCanStartFrom(PlannedRoom room, Vector2Int n)
        {
            int k = n.y * chunksX + n.x;
            bool own = roomOfChunk[k] == room;
            bool access = n == ChunkOf(AccessZone(room.Rect, -1)) || n == ChunkOf(AccessZone(room.Rect, 1));
            if (!own && !access) return false;
            for (int y = 0; y < NestGrid.ChunkSize; y++)
                for (int x = 0; x < NestGrid.ChunkSize; x++)
                    if (!NestGrid.IsOpen(grid.Get(new Vector2Int(n.x * NestGrid.ChunkSize + x, n.y * NestGrid.ChunkSize + y)))) return false;
            return true;
        }

        private Job CreateJob(int k, Vector2Int stand)
        {
            Vector2Int chunk = ChunkCoord(k);
            var job = new Job { Chunk = k, Room = roomOfChunk[k], Stand = stand };
            for (int y = 0; y < NestGrid.ChunkSize; y++)
                for (int x = 0; x < NestGrid.ChunkSize; x++)
                {
                    var c = new Vector2Int(chunk.x * NestGrid.ChunkSize + x, chunk.y * NestGrid.ChunkSize + y);
                    if (!planned[Index(c)]) continue;
                    // Inserción ordenada por distancia a la obrera (como mucho 9 casillas).
                    int i = job.Count++, d = (c - stand).sqrMagnitude;
                    for (; i > 0 && (job.Order[i - 1] - stand).sqrMagnitude > d; i--) job.Order[i] = job.Order[i - 1];
                    job.Order[i] = c;
                }
            if (job.Count == 0)
            {
                workableDirty = true;   // la caché de trabajables iba por detrás del plano
                return null;
            }

            float cost = job.Room != null
                ? job.Room.Data.workCost * (1f + roomCostIncrease * roomsBuilt)
                  / (job.Room.Rect.width / NestGrid.ChunkSize * (job.Room.Rect.height / NestGrid.ChunkSize))
                : job.Count / (float)ChunkCells * tunnelChunkCost;
            cost *= StratumMultiplier(grid.StratumAt(chunk * NestGrid.ChunkSize));
            if (cost <= 0f)
            {
                RoomData data = job.Room?.Data;
                if (warnedCost.Add(data))
                    Debug.LogWarning($"[DigPlanner] El bloque de {(data != null ? data.name : "túnel")} cuesta {cost} (¿workCost o tunnelChunkCost a 0?): se usa {MinCost}.", data);
                cost = MinCost;
            }
            bool facingRight = chunk.x * NestGrid.ChunkSize + 1 >= stand.x;
            job.Dig = new DigJob(chunk, navigator.GroundPoint(stand), facingRight, cost);

            jobs[k] = job;
            active.Add(job);
            workableDirty = true;
            return job;
        }

        private void RemoveJob(int k)
        {
            if (!jobs.TryGetValue(k, out var job)) return;
            job.Dig.Cancel();   // sin plano no queda nada que vaciar: las obreras lo sueltan como si estuviera terminado
            jobs.Remove(k);
            active.Remove(job);
            workableDirty = true;
            assignDirty = true;   // sus obreras quedan libres
        }

        /// <summary>Vacía las casillas que tocan según Progress y, si el trabajo era de sala, mira si la sala ya está entera.</summary>
        private void Apply(Job job)
        {
            int target = Mathf.FloorToInt(job.Dig.Progress * job.Count);
            bool changed = job.Applied < target;
            while (job.Applied < target)
            {
                Vector2Int c = job.Order[job.Applied++];
                if (planned[Index(c)]) grid.Set(c, CellType.Dug);   // las borradas o canceladas se saltan
            }
            if (changed && job.Room != null) TryFinishRoom(job.Room);
            if (job.Dig.IsDone) RemoveJob(job.Chunk);
        }

        private void TryFinishRoom(PlannedRoom room)
        {
            if (room.Done || !rooms.Contains(room)) return;
            foreach (var c in room.Rect.allPositionsWithin)
                if (grid.Get(c) != CellType.Dug) return;

            room.Done = true;
            roomsBuilt++;
            foreach (var c in room.Rect.allPositionsWithin)
            {
                roomOfChunk[ChunkIndex(c)] = null;
                grid.Set(c, CellType.Room);
            }
            EventBus.Publish(new RoomDug(room.Data, room.Rect.position));
        }

        private float StratumMultiplier(Stratum s) => s switch
        {
            Stratum.Clay => clayMultiplier,
            Stratum.Rock => rockMultiplier,
            _ => 1f,
        };

        // ================= SALAS EN PLANO =================

        /// <summary>Qué es la casilla para las salas en plano sin terminar. La huella manda sobre el anillo y el anillo sobre la zona de acceso.</summary>
        private Part PlannedRoomPart(Vector2Int c)
        {
            bool ring = false, access = false;
            for (int i = 0; i < rooms.Count; i++)
            {
                PlannedRoom r = rooms[i];
                if (r.Done) continue;
                if (r.Rect.Contains(c)) return Part.Footprint;
                if (AccessZone(r.Rect, -1).Contains(c) || AccessZone(r.Rect, 1).Contains(c)) access = true;
                else if (Grow(r.Rect, NestGrid.RoomWallThickness).Contains(c)) ring = true;
            }
            return ring ? Part.Ring : access ? Part.Access : Part.None;
        }

        private static RectInt AccessZone(RectInt room, int side) =>
            new RectInt(side < 0 ? room.xMin - NestGrid.DoorSize : room.xMax, room.yMin, NestGrid.DoorSize, NestGrid.DoorSize);

        private static RectInt Grow(RectInt r, int n) => new RectInt(r.xMin - n, r.yMin - n, r.width + 2 * n, r.height + 2 * n);

        private static int Snap(int v) => Mathf.FloorToInt(v / (float)NestGrid.ChunkSize) * NestGrid.ChunkSize;

        private static Vector2Int ChunkOf(RectInt zone) => new Vector2Int(zone.xMin / NestGrid.ChunkSize, zone.yMin / NestGrid.ChunkSize);

        // ================= AUXILIARES =================

        private bool EnsureBuilt()
        {
            if (planned != null) return true;
            if (grid == null) grid = NestGrid.Instance;
            if (navigator == null) navigator = Navigation.Current as GridNavigator;
            if (grid == null || navigator == null) return false;

            width = grid.Width;
            chunksX = (grid.Width + NestGrid.ChunkSize - 1) / NestGrid.ChunkSize;
            chunksY = (grid.Height + NestGrid.ChunkSize - 1) / NestGrid.ChunkSize;
            planned = new bool[grid.Width * grid.Height];
            plannedPerChunk = new int[chunksX * chunksY];
            roomOfChunk = new PlannedRoom[chunksX * chunksY];
            return true;
        }

        private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < grid.Height;
        private int Index(Vector2Int c) => c.y * width + c.x;
        private int ChunkIndex(Vector2Int c) => c.y / NestGrid.ChunkSize * chunksX + c.x / NestGrid.ChunkSize;
        private Vector2Int ChunkCoord(int k) => new Vector2Int(k % chunksX, k / chunksX);

        // ================= GIZMOS =================

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || planned == null) return;

            float cs = grid.CellSize;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            for (int i = 0; i < planned.Length; i++)
                if (planned[i]) Gizmos.DrawCube(grid.CellToWorld(new Vector2Int(i % width, i / width)), Vector3.one * (cs * 0.8f));

            RefreshWorkable();
            Gizmos.color = Color.green;
            foreach (var (chunk, stand) in workable)
            {
                Vector3 center = grid.CellToWorld(ChunkCoord(chunk) * NestGrid.ChunkSize + Vector2Int.one);
                Gizmos.DrawWireCube(center, Vector3.one * (cs * NestGrid.ChunkSize));
                Gizmos.DrawSphere(navigator.GroundPoint(stand), cs * 0.3f);
            }
            Gizmos.color = Color.yellow;
            foreach (var job in active)
            {
                Gizmos.DrawSphere(job.Dig.StandPosition, cs * 0.3f);
#if UNITY_EDITOR
                Vector3 top = grid.CellToWorld(job.Dig.Chunk * NestGrid.ChunkSize + new Vector2Int(0, NestGrid.ChunkSize));
                UnityEditor.Handles.Label(top, $"{job.Dig.Progress * 100f:0} % · {job.Dig.Workers}/{DigJob.MaxWorkers}");
#endif
            }
        }
    }
}
