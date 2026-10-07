using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Punto del grafo de túneles. Ponlo en el objeto Entrance de cada zona y en los cruces de túneles.
    /// Basta con enlazar en un sentido: el grafo crea la conexión en los dos.
    /// </summary>
    public class WaypointNode : MonoBehaviour
    {
        [SerializeField] private List<WaypointNode> links = new List<WaypointNode>();

        public IReadOnlyList<WaypointNode> Links => links;
        public Vector3 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f);
            Gizmos.DrawSphere(transform.position, 0.18f);
            foreach (var l in links)
                if (l != null) Gizmos.DrawLine(transform.position, l.transform.position);
        }
    }
}
