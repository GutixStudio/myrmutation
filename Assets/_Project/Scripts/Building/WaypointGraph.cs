using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Navegación de la alfa: grafo de túneles fijos entre las entradas de las zonas.
    /// - Construye el grafo con todos los WaypointNode de la escena (enlaces en los dos sentidos).
    /// - Calcula rutas con A* y las guarda en caché.
    /// - Opcional: dibuja los túneles con sprites estirados.
    /// </summary>
    public class WaypointGraph : MonoBehaviour, INavigator
    {
        [Header("Túneles visibles (opcional)")]
        [SerializeField] private bool drawTunnels = true;
        [Tooltip("Sprite cuadrado de 1x1 (por ejemplo, el mismo del fondo)")]
        [SerializeField] private Sprite tunnelSprite;
        [SerializeField] private Color tunnelColor = new Color(0.16f, 0.1f, 0.06f, 1f);
        [SerializeField] private float tunnelWidth = 0.8f;
        [SerializeField] private int tunnelSortingOrder = 1;

        private readonly List<WaypointNode> nodes = new List<WaypointNode>();
        private readonly Dictionary<WaypointNode, List<WaypointNode>> adj = new Dictionary<WaypointNode, List<WaypointNode>>();
        private readonly Dictionary<long, List<WaypointNode>> cache = new Dictionary<long, List<WaypointNode>>();

        private void Awake()
        {
            Rebuild();
            Navigation.Current = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Navigation.Current, this)) Navigation.Current = null;
        }

        /// <summary>Reconstruye el grafo (llamar si cambian los túneles; en la beta lo hará la excavación).</summary>
        public void Rebuild()
        {
            nodes.Clear(); adj.Clear(); cache.Clear();
            nodes.AddRange(FindObjectsByType<WaypointNode>(FindObjectsSortMode.None));
            foreach (var n in nodes) adj[n] = new List<WaypointNode>();
            foreach (var n in nodes)
                foreach (var l in n.Links)
                {
                    if (l == null || !adj.ContainsKey(l)) continue;
                    if (!adj[n].Contains(l)) adj[n].Add(l);
                    if (!adj[l].Contains(n)) adj[l].Add(n);
                }
            if (drawTunnels) BuildTunnelVisuals();
        }

        public bool TryGetPath(Vector3 from, Vector3 to, List<Vector3> path)
        {
            path.Clear();
            var start = Nearest(from);
            var goal = Nearest(to);
            if (start == null || goal == null) return false;

            var nodePath = FindPath(start, goal);
            if (nodePath == null) return false;

            foreach (var n in nodePath) path.Add(n.Position);
            path.Add(new Vector3(to.x, to.y, 0f));
            return true;
        }

        public WaypointNode Nearest(Vector3 p)
        {
            WaypointNode best = null;
            float bestD = float.MaxValue;
            foreach (var n in nodes)
            {
                float d = (n.Position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        // ---------- A* ----------
        private List<WaypointNode> FindPath(WaypointNode start, WaypointNode goal)
        {
            long key = ((long)start.GetInstanceID() << 32) ^ (uint)goal.GetInstanceID();
            if (cache.TryGetValue(key, out var cached)) return cached;

            var open = new List<WaypointNode> { start };
            var cameFrom = new Dictionary<WaypointNode, WaypointNode>();
            var g = new Dictionary<WaypointNode, float> { [start] = 0f };
            var f = new Dictionary<WaypointNode, float> { [start] = Dist(start, goal) };

            while (open.Count > 0)
            {
                // Pocos nodos: buscar el mínimo a mano es suficiente.
                var current = open[0];
                foreach (var n in open) if (f[n] < f[current]) current = n;

                if (current == goal)
                {
                    var result = new List<WaypointNode> { current };
                    while (cameFrom.TryGetValue(current, out current)) result.Insert(0, current);
                    cache[key] = result;
                    return result;
                }

                open.Remove(current);
                foreach (var nb in adj[current])
                {
                    float tentative = g[current] + Dist(current, nb);
                    if (g.TryGetValue(nb, out float old) && tentative >= old) continue;
                    cameFrom[nb] = current;
                    g[nb] = tentative;
                    f[nb] = tentative + Dist(nb, goal);
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
            cache[key] = null;
            return null;
        }

        private static float Dist(WaypointNode a, WaypointNode b) => Vector3.Distance(a.Position, b.Position);

        // ---------- Túneles visibles ----------
        private Transform tunnelRoot;

        private void BuildTunnelVisuals()
        {
            if (tunnelSprite == null) return;
            if (tunnelRoot != null) Destroy(tunnelRoot.gameObject);
            tunnelRoot = new GameObject("Tunnels").transform;
            tunnelRoot.SetParent(transform, false);

            var done = new HashSet<long>();
            foreach (var a in nodes)
                foreach (var b in adj[a])
                {
                    long k = a.GetInstanceID() < b.GetInstanceID()
                        ? ((long)a.GetInstanceID() << 32) ^ (uint)b.GetInstanceID()
                        : ((long)b.GetInstanceID() << 32) ^ (uint)a.GetInstanceID();
                    if (!done.Add(k)) continue;
                    CreateSegment(a.Position, b.Position);
                }
            // Un "codo" en cada nodo para que los tramos se unan sin huecos.
            foreach (var n in nodes) CreateSegment(n.Position, n.Position);
        }

        private void CreateSegment(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Tunnel");
            go.transform.SetParent(tunnelRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tunnelSprite;
            sr.color = tunnelColor;
            sr.sortingOrder = tunnelSortingOrder;

            Vector3 d = b - a;
            float len = d.magnitude + tunnelWidth; // se solapan un poco en los extremos
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            // Ajusta la escala al tamaño real del sprite (funciona con cualquier sprite y Pixels Per Unit).
            Vector2 size = tunnelSprite.bounds.size;
            float sx = size.x > 0.0001f ? size.x : 1f, sy = size.y > 0.0001f ? size.y : 1f;
            go.transform.localScale = new Vector3(len / sx, tunnelWidth / sy, 1f);
        }
    }
}
