using UnityEngine;

namespace Myrmutation.Building
{
    /// <summary>Solo para probar A2: muestra en consola qué zona se ha tocado. Quitar cuando exista A4.</summary>
    public class ZoneTapTest : MonoBehaviour
    {
        private void OnEnable() => CameraController.Tapped += OnTap;
        private void OnDisable() => CameraController.Tapped -= OnTap;

        private void OnTap(Vector2 world)
        {
            var zone = BuildZone.At(world);
            Debug.Log(zone != null
                ? $"Zona {zone.ZoneId} ({zone.Size}, {zone.Stratum}) · libre: {zone.IsFree}"
                : $"Toque en {world} (sin zona)");
        }
    }
}
