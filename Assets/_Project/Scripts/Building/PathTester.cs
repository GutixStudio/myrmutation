using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>
    /// Solo para probar A3: al tocar una zona, este objeto viaja por los túneles desde donde esté hasta su entrada.
    /// Ponlo en un Square pequeño. Quitar cuando B tenga las hormigas moviéndose.
    /// </summary>
    public class PathTester : MonoBehaviour
    {
        [SerializeField] private float speed = 4f;

        private readonly List<Vector3> path = new List<Vector3>();
        private Coroutine moving;

        private void OnEnable() => CameraController.Tapped += OnTap;
        private void OnDisable() => CameraController.Tapped -= OnTap;

        private void OnTap(Vector2 world)
        {
            var zone = BuildZone.At(world);
            if (zone == null || Navigation.Current == null) return;

            if (!Navigation.Current.TryGetPath(transform.position, zone.EntrancePosition, path))
            {
                Debug.LogWarning($"[PathTester] No hay camino hasta {zone.ZoneId}");
                return;
            }
            Debug.Log($"[PathTester] Ruta a {zone.ZoneId}: {path.Count} puntos");
            if (moving != null) StopCoroutine(moving);
            moving = StartCoroutine(Follow(new List<Vector3>(path)));
        }

        private IEnumerator Follow(List<Vector3> points)
        {
            foreach (var p in points)
                while ((transform.position - p).sqrMagnitude > 0.0004f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, p, speed * Time.deltaTime);
                    yield return null;
                }
            moving = null;
        }
    }
}
