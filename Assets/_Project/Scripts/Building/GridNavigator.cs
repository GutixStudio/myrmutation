using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Navegación de la beta sobre la rejilla fina (diseno-construccion-beta.md §7). Mismo contrato INavigator que la alfa.
    /// Nodos: casillas excavadas con holgura, la fila central de las 3 inferiores de cada sala (en las salas solo se anda
    /// en horizontal, así que solo se entra por las puertas) y la fila central de la hierba.
    /// A* en 8 direcciones sin reservar memoria por búsqueda. Cada ruta se tensa, se redondea (Chaikin) y se baja al suelo
    /// una sola vez y se guarda en caché hasta que cambie una casilla.
    /// También recoloca en el nodo más cercano a las hormigas que se queden dentro de tierra.
    /// </summary>
    public class GridNavigator : MonoBehaviour, INavigator
    {
        /// <summary>Etapas del post-proceso de la ruta (para depurar y para los tests).</summary>
        public enum PathStage { Pulled, Smoothed, Lowered }

        [Tooltip("Rejilla del hormiguero. Si se deja vacío, usa NestGrid.Instance")]
        [SerializeField] private NestGrid grid;

        [Header("Superficie")]
        [Tooltip("Fila de hierba por la que se camina, contada desde la primera fila de superficie (1 = la central de las 3)")]
        [SerializeField] private int grassCenterOffset = 1;

        [Header("Suavizado")]
        [Tooltip("Pasadas de Chaikin para redondear las esquinas")]
        [SerializeField] private int chaikinPasses = 3;
        [Tooltip("Máximo que se recorta cada esquina, en unidades. Sin límite, el 25 % de un tramo largo mete la curva en la roca")]
        [SerializeField] private float maxCornerCut = 0.25f;
        [Tooltip("Cuánto se baja la ruta en los tramos horizontales, en unidades (del centro del túnel al suelo, menos media hormiga)")]
        [SerializeField] private float lowerAmount = 0.2f;
        [Tooltip("Exponente de la horizontalidad: más alto = solo se baja en tramos casi horizontales")]
        [SerializeField] private float lowerExponent = 4f;
        [Tooltip("Distancia mínima entre puntos de la ruta, en unidades (como arriveDistance del actuador). Los tramos más cortos giran a la hormiga al azar")]
        [SerializeField] private float minPointSpacing = 0.05f;

        [Header("Búsqueda")]
        [Tooltip("Rutas guardadas como máximo; al llegar al límite se vacía la caché")]
        [SerializeField] private int maxCachedPaths = 256;
        [Tooltip("Radio máximo, en casillas, al buscar el nodo más cercano")]
        [SerializeField] private int nearestMaxRadius = 40;

        [Header("Hormigas dentro de tierra")]
        [Tooltip("Segundos entre comprobaciones")]
        [SerializeField] private float stuckCheckInterval = 1f;
        [Tooltip("Altura sobre la posición de la hormiga a la que se mira la casilla (los pies van justo en el borde del suelo)")]
        [SerializeField] private float stuckProbeHeight = 0.1f;

        [Header("Alcance")]
        [Tooltip("Casilla de la colonia desde la que se mide qué está conectado (el pozo). Si no es nodo, se usa el más cercano")]
        [SerializeField] private Vector2Int reachRootCell = new Vector2Int(60, 40);

        private const float DiagonalCost = 1.41421356f;
        private static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DirY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private int width, height, surfaceRow;
        private bool[] isNode;

        // A*: arrays del tamaño de la rejilla reservados una vez. 'generation' sube en cada búsqueda
        // y marca qué entradas son de la búsqueda actual, así que nunca hay que limpiarlos.
        private float[] g, f;
        private int[] parent, seenGen, closedGen, heap, heapPos;
        private int heapCount, generation;

        // Alcance desde reachRootCell: relleno que se rehace al preguntar si ha cambiado alguna casilla.
        private bool[] reachable;
        private int[] reachQueue;
        private bool reachDirty = true;

        private readonly Dictionary<long, Vector3[]> cache = new Dictionary<long, Vector3[]>();
        private readonly List<int> cellPath = new List<int>();
        private readonly List<Vector3> scratch = new List<Vector3>();
        private readonly List<Vector3> temp = new List<Vector3>();

        private Vector3[] lastRoute;   // solo para los Gizmos

        private List<Ant> suspects = new List<Ant>();
        private List<Ant> nextSuspects = new List<Ant>();
        private float stuckTimer;

        private void Awake() => Navigation.Current = this;

        private void OnDestroy()
        {
            if (ReferenceEquals(Navigation.Current, this)) Navigation.Current = null;
        }

        // CellChanged cubre excavar (Solid→Dug) y las salas (Solid→Room); CellDug solo lo primero.
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
            stuckTimer += Time.deltaTime;
            if (stuckTimer < stuckCheckInterval) return;
            stuckTimer = 0f;
            CheckStuckAnts();
        }

        // ================= CONTRATO =================

        /// <summary>
        /// Ruta del nodo más cercano a 'from' al nodo más cercano a 'to', ya tensada, redondeada y bajada al suelo.
        /// path[0] es el nodo de salida y la ruta acaba en el nodo de destino (no en 'to' exacto, que puede caer en tierra).
        /// </summary>
        public bool TryGetPath(Vector3 from, Vector3 to, List<Vector3> path)
        {
            path.Clear();
            if (!Nearest(from, out var startCell) || !Nearest(to, out var goalCell)) return false;

            int start = Index(startCell), goal = Index(goalCell);
            long key = ((long)start << 32) | (uint)goal;
            if (!cache.TryGetValue(key, out var route))
            {
                route = BuildPath(start, goal, PathStage.Lowered, scratch) ? scratch.ToArray() : null;
                if (cache.Count >= maxCachedPaths) cache.Clear();
                cache[key] = route;
            }
            if (route == null) return false;
            lastRoute = route;

            // El nodo de salida puede quedar un poco detrás de la hormiga: ir a él la haría dar media vuelta.
            int first = 0;
            if (route.Length >= 2)
            {
                Vector2 toFirst = route[0] - from;
                float half = grid.CellSize * 0.5f;
                if (toFirst.sqrMagnitude < half * half && Vector2.Dot(toFirst, route[1] - route[0]) < 0f) first = 1;
            }
            for (int i = first; i < route.Length; i++) path.Add(route[i]);
            return true;
        }

        /// <summary>Como TryGetPath, pero sin caché y parando en una etapa del post-proceso. Para depurar y para los tests.</summary>
        public bool TryGetPath(Vector3 from, Vector3 to, List<Vector3> path, PathStage stage)
        {
            path.Clear();
            if (!Nearest(from, out var startCell) || !Nearest(to, out var goalCell)) return false;
            return BuildPath(Index(startCell), Index(goalCell), stage, path);
        }

        /// <summary>
        /// Nodo cuyo GroundPoint está más cerca de un punto (si el punto está dentro de tierra, el del hueco más próximo).
        /// Así un GroundPoint (donde acaban las rutas y se paran las hormigas) da su propio nodo y no el de debajo,
        /// que en pozos y huecos anchos también es nodo. False si no hay ninguno a menos de nearestMaxRadius casillas.
        /// </summary>
        public bool Nearest(Vector3 world, out Vector2Int node)
        {
            node = default;
            if (!EnsureBuilt()) return false;

            Vector2Int c = grid.WorldToCell(world + Vector3.up * lowerAmount);
            c.x = Mathf.Clamp(c.x, 0, width - 1);
            c.y = Mathf.Clamp(c.y, 0, height - 1);

            int bestD = int.MaxValue;
            for (int r = 0; r <= nearestMaxRadius; r++)
            {
                if (r * r > bestD) break;   // todo el anillo r está a r o más
                for (int dy = -r; dy <= r; dy++)
                {
                    int y = c.y + dy;
                    if (y < 0 || y >= height) continue;
                    // Del anillo solo hacen falta los bordes: filas enteras arriba y abajo, dos casillas en las demás.
                    int stepX = dy == -r || dy == r ? 1 : 2 * r;
                    for (int dx = -r; dx <= r; dx += stepX)
                    {
                        int x = c.x + dx;
                        if (x < 0 || x >= width || !isNode[y * width + x]) continue;
                        int d = dx * dx + dy * dy;
                        if (d < bestD) { bestD = d; node = new Vector2Int(x, y); }
                    }
                }
            }
            return bestD != int.MaxValue;
        }

        /// <summary>La casilla es un nodo de navegación.</summary>
        public bool IsNode(Vector2Int cell) =>
            EnsureBuilt() && InGrid(cell.x, cell.y) && isNode[cell.y * width + cell.x];

        /// <summary>Punto donde acaba toda ruta que llega a este nodo: su centro bajado lowerAmount.</summary>
        public Vector3 GroundPoint(Vector2Int node) => grid.CellToWorld(node) + Vector3.down * lowerAmount;

        /// <summary>El nodo está conectado con la colonia (reachRootCell) por nodos.</summary>
        public bool IsReachable(Vector2Int node)
        {
            if (!IsNode(node)) return false;
            if (reachDirty) FloodReachable();
            return reachable[node.y * width + node.x];
        }

        // ================= NODOS =================

        private bool EnsureBuilt()
        {
            if (isNode != null) return true;
            if (grid == null) return false;

            width = grid.Width;
            height = grid.Height;
            surfaceRow = grid.TerrainRows;
            int n = width * height;
            isNode = new bool[n];
            g = new float[n];
            f = new float[n];
            parent = new int[n];
            seenGen = new int[n];
            closedGen = new int[n];
            heap = new int[n];
            heapPos = new int[n];
            reachable = new bool[n];
            reachQueue = new int[n];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    isNode[y * width + x] = ComputeIsNode(x, y);
            return true;
        }

        private void OnCellChanged(Vector2Int cell)
        {
            cache.Clear();
            reachDirty = true;
            if (isNode == null) return;
            // La holgura mira las 4 vecinas y la regla de las salas mira 2 casillas hacia abajo.
            for (int y = cell.y - 1; y <= cell.y + 2; y++)
                for (int x = cell.x - 1; x <= cell.x + 1; x++)
                    if (InGrid(x, y)) isNode[y * width + x] = ComputeIsNode(x, y);
        }

        private bool ComputeIsNode(int x, int y)
        {
            var c = new Vector2Int(x, y);
            switch (grid.Get(c))
            {
                case CellType.Dug:
                    return grid.HasClearance(c);
                case CellType.Room:
                    // Fila central de las 3 inferiores de la sala.
                    return grid.Get(new Vector2Int(x, y - 1)) == CellType.Room
                        && grid.Get(new Vector2Int(x, y - 2)) != CellType.Room;
                case CellType.Surface:
                    // Fila central de la hierba y, por debajo, la hierba con holgura (la boca del pozo).
                    return y <= surfaceRow + grassCenterOffset && grid.HasClearance(c);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Relleno en 4 direcciones desde la raíz con las mismas reglas que el A* (en las salas, solo en horizontal).
        /// Las diagonales no hacen falta: el A* solo las usa si sus dos ortogonales son nodo.
        /// </summary>
        private void FloodReachable()
        {
            reachDirty = false;
            System.Array.Clear(reachable, 0, reachable.Length);
            if (!Nearest(grid.CellToWorld(reachRootCell), out var root)) return;

            int head = 0, tail = 0;
            reachQueue[tail++] = Index(root);
            reachable[Index(root)] = true;
            while (head < tail)
            {
                int cur = reachQueue[head++];
                int cx = cur % width, cy = cur / width;
                bool curRoom = grid.Get(new Vector2Int(cx, cy)) == CellType.Room;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DirX[d], ny = cy + DirY[d];
                    if (!InGrid(nx, ny)) continue;
                    int nb = ny * width + nx;
                    if (!isNode[nb] || reachable[nb]) continue;
                    if (DirY[d] != 0 && (curRoom || grid.Get(new Vector2Int(nx, ny)) == CellType.Room)) continue;
                    reachable[nb] = true;
                    reachQueue[tail++] = nb;
                }
            }
        }

        // ================= A* =================

        private bool FindCellPath(int start, int goal)
        {
            cellPath.Clear();
            generation++;
            heapCount = 0;
            int gx = goal % width, gy = goal / width;

            seenGen[start] = generation;
            g[start] = 0f;
            parent[start] = -1;
            f[start] = Octile(start % width, start / width, gx, gy);
            Push(start);

            while (heapCount > 0)
            {
                int cur = Pop();
                if (cur == goal)
                {
                    for (int i = cur; i != -1; i = parent[i]) cellPath.Add(i);
                    cellPath.Reverse();
                    return true;
                }
                closedGen[cur] = generation;

                int cx = cur % width, cy = cur / width;
                bool curRoom = grid.Get(new Vector2Int(cx, cy)) == CellType.Room;
                for (int d = 0; d < 8; d++)
                {
                    int dx = DirX[d], dy = DirY[d];
                    int nx = cx + dx, ny = cy + dy;
                    if (!InGrid(nx, ny)) continue;
                    int nb = ny * width + nx;
                    if (!isNode[nb] || closedGen[nb] == generation) continue;
                    // En las salas solo se anda en horizontal: así solo se entra por las puertas.
                    if (dy != 0 && (curRoom || grid.Get(new Vector2Int(nx, ny)) == CellType.Room)) continue;
                    // Sin cortar esquinas: en diagonal, las dos ortogonales tienen que ser nodo.
                    bool diagonal = dx != 0 && dy != 0;
                    if (diagonal && (!isNode[cy * width + nx] || !isNode[ny * width + cx])) continue;

                    float cost = g[cur] + (diagonal ? DiagonalCost : 1f);
                    bool seen = seenGen[nb] == generation;
                    if (seen && cost >= g[nb]) continue;

                    g[nb] = cost;
                    parent[nb] = cur;
                    f[nb] = cost + Octile(nx, ny, gx, gy);
                    if (seen) SiftUp(heapPos[nb]);
                    else { seenGen[nb] = generation; Push(nb); }
                }
            }
            return false;
        }

        private static float Octile(int x, int y, int gx, int gy)
        {
            int dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
            return dx + dy + (DiagonalCost - 2f) * Mathf.Min(dx, dy);
        }

        // Montículo binario de mínimos por f, con heapPos para poder subir un nodo que ya está dentro.
        private void Push(int node)
        {
            heap[heapCount] = node;
            heapPos[node] = heapCount;
            heapCount++;
            SiftUp(heapCount - 1);
        }

        private int Pop()
        {
            int top = heap[0];
            heapCount--;
            if (heapCount > 0)
            {
                heap[0] = heap[heapCount];
                heapPos[heap[0]] = 0;
                SiftDown(0);
            }
            return top;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (f[heap[p]] <= f[heap[i]]) return;
                Swap(i, p);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= heapCount) return;
                int m = l + 1 < heapCount && f[heap[l + 1]] < f[heap[l]] ? l + 1 : l;
                if (f[heap[i]] <= f[heap[m]]) return;
                Swap(i, m);
                i = m;
            }
        }

        private void Swap(int a, int b)
        {
            int t = heap[a];
            heap[a] = heap[b];
            heap[b] = t;
            heapPos[heap[a]] = a;
            heapPos[heap[b]] = b;
        }

        // ================= POST-PROCESO =================

        private bool BuildPath(int start, int goal, PathStage stage, List<Vector3> result)
        {
            result.Clear();
            if (!FindCellPath(start, goal)) return false;

            Pull(result);
            if (stage == PathStage.Pulled) return true;
            for (int i = 0; i < chaikinPasses; i++) Chaikin(result);
            if (stage == PathStage.Smoothed) return true;
            LowerToFloor(result);
            RemoveClosePoints(result);
            return true;
        }

        /// <summary>Tensado: desde cada punto salta al más lejano de la ruta que se vea en línea recta por nodos.</summary>
        private void Pull(List<Vector3> result)
        {
            result.Add(CellCenter(cellPath[0]));
            int anchor = 0, last = cellPath.Count - 1;
            while (anchor < last)
            {
                int next = anchor + 1;
                while (next < last && LineClear(cellPath[anchor], cellPath[next + 1])) next++;
                result.Add(CellCenter(cellPath[next]));
                anchor = next;
            }
        }

        /// <summary>
        /// La recta de centro a centro solo pisa nodos. Recorre casilla a casilla sin saltarse ninguna;
        /// si pasa justo por una esquina, mira las dos casillas de al lado.
        /// </summary>
        private bool LineClear(int a, int b)
        {
            int x = a % width, y = a / width;
            int dx = b % width - x, dy = b / width - y;
            int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1;
            int nx = Mathf.Abs(dx), ny = Mathf.Abs(dy);

            for (int ix = 0, iy = 0; ix < nx || iy < ny;)
            {
                // Compara dónde cruza la siguiente línea vertical ((0,5+ix)/nx) y la horizontal ((0,5+iy)/ny).
                int cmp = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (cmp == 0)
                {
                    if (!isNode[y * width + x + sx] || !isNode[(y + sy) * width + x]) return false;
                    x += sx; y += sy; ix++; iy++;
                }
                else if (cmp < 0) { x += sx; ix++; }
                else { y += sy; iy++; }
                if (!isNode[y * width + x]) return false;
            }
            return true;
        }

        /// <summary>Una pasada de Chaikin que conserva los extremos. Cada corte es el 25 % del tramo, sin pasar de maxCornerCut.</summary>
        private void Chaikin(List<Vector3> points)
        {
            if (points.Count < 3) return;
            int last = points.Count - 1;
            temp.Clear();
            temp.Add(points[0]);
            for (int i = 0; i < last; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float len = Vector3.Distance(a, b);
                if (len < 1e-5f) continue;
                Vector3 cut = (b - a) * (Mathf.Min(0.25f * len, maxCornerCut) / len);
                if (i > 0) temp.Add(a + cut);
                if (i < last - 1) temp.Add(b - cut);
            }
            temp.Add(points[last]);
            CopyTempTo(points);
        }

        /// <summary>
        /// Baja cada punto lowerAmount × horizontalidad^lowerExponent (por el suelo en lo llano, por el centro en las cuestas).
        /// El último se baja lowerAmount entero: toda ruta acaba en GroundPoint del nodo destino.
        /// </summary>
        private void LowerToFloor(List<Vector3> points)
        {
            int last = points.Count - 1;
            temp.Clear();
            for (int i = 0; i <= last; i++)
            {
                Vector3 t = points[Mathf.Min(i + 1, last)] - points[Mathf.Max(i - 1, 0)];
                float h = t.sqrMagnitude > 1e-8f ? Mathf.Abs(t.x) / t.magnitude : 1f;   // un solo punto: como si fuera llano
                if (i == last) h = 1f;   // el final siempre en GroundPoint, se llegue como se llegue (ahí espera DigJob.StandPosition)
                temp.Add(points[i] + Vector3.down * (lowerAmount * Mathf.Pow(h, lowerExponent)));
            }
            CopyTempTo(points);
        }

        /// <summary>Quita los puntos a menos de minPointSpacing del anterior. Conserva el primero y el último.</summary>
        private void RemoveClosePoints(List<Vector3> points)
        {
            int last = points.Count - 1;
            float min2 = minPointSpacing * minPointSpacing;
            temp.Clear();
            temp.Add(points[0]);
            for (int i = 1; i < last; i++)
                if ((points[i] - temp[temp.Count - 1]).sqrMagnitude >= min2) temp.Add(points[i]);
            if (last > 0)
            {
                // El último manda: se quitan los anteriores que queden pegados a él.
                while (temp.Count > 1 && (points[last] - temp[temp.Count - 1]).sqrMagnitude < min2) temp.RemoveAt(temp.Count - 1);
                if ((points[last] - temp[temp.Count - 1]).sqrMagnitude < min2) temp.Clear();
                temp.Add(points[last]);
            }
            CopyTempTo(points);
        }

        // Sin AddRange: en Mono reserva un array temporal.
        private void CopyTempTo(List<Vector3> points)
        {
            points.Clear();
            for (int i = 0; i < temp.Count; i++) points.Add(temp[i]);
        }

        // ================= HORMIGAS DENTRO DE TIERRA =================

        /// <summary>
        /// Recoloca en el nodo más cercano a las hormigas que salen dentro de tierra en dos comprobaciones seguidas
        /// (en una sola puede ser un redondeo). Las dos listas se reutilizan, sin reservar memoria.
        /// </summary>
        private void CheckStuckAnts()
        {
            if (!EnsureBuilt()) return;
            nextSuspects.Clear();
            foreach (var ant in Ant.All)
            {
                if (ant == null || !ant.isActiveAndEnabled || !ant.IsAlive) continue;
                Vector3 pos = ant.transform.position;
                Vector2Int c = grid.WorldToCell(pos + Vector3.up * stuckProbeHeight);
                // Fuera de la rejilla (expediciones) o en un hueco: nada que hacer.
                if (!InGrid(c.x, c.y) || NestGrid.IsOpen(grid.Get(c))) continue;
                if (!suspects.Contains(ant)) { nextSuspects.Add(ant); continue; }

                if (!Nearest(pos, out var node)) continue;
                Vector3 p = GroundPoint(node);
                p.z = pos.z;
                ant.transform.position = p;
            }
            (suspects, nextSuspects) = (nextSuspects, suspects);
        }

        // ================= GIZMOS =================

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || isNode == null) return;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            Vector3 dot = Vector3.one * (grid.CellSize * 0.2f);
            for (int i = 0; i < isNode.Length; i++)
                if (isNode[i]) Gizmos.DrawCube(CellCenter(i), dot);

            if (lastRoute == null) return;
            Gizmos.color = Color.magenta;
            for (int i = 1; i < lastRoute.Length; i++) Gizmos.DrawLine(lastRoute[i - 1], lastRoute[i]);
        }

        // ================= AUXILIARES =================

        private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        private int Index(Vector2Int c) => c.y * width + c.x;
        private Vector3 CellCenter(int i) => grid.CellToWorld(new Vector2Int(i % width, i / width));
    }
}
