using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Contrato de navegación. La IA (B) SOLO usa esto, nunca el grafo directamente.
    /// Alfa: WaypointGraph (túneles fijos). Beta: rejilla con A* (túneles excavados) con la misma interfaz.
    /// </summary>
    public interface INavigator
    {
        /// <summary>Rellena 'path' con los puntos a recorrer de 'from' a 'to' (incluido 'to'). False si no hay camino.</summary>
        bool TryGetPath(Vector3 from, Vector3 to, List<Vector3> path);
    }

    /// <summary>Acceso global al navegador activo de la escena.</summary>
    public static class Navigation
    {
        public static INavigator Current { get; set; }
    }
}
