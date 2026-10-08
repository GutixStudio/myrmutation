using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Ants.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/B.unity). Da órdenes a una hormiga a mano y muestra lo que percibe.
    ///   Clic en una zona → MoverA (a la sala si está construida, si no a la entrada)
    ///   T → Trabajar (en la sala donde esté, durante 'workSeconds')
    ///   C → Comer
    ///   P → Parar
    ///   H → Ir a la Despensa más cercana y comer al llegar (prueba NearestRoom + IsAtTarget)
    /// </summary>
    public class AntTestController : MonoBehaviour
    {
        [SerializeField] private AntActuator ant;
        [SerializeField] private float workSeconds = 3f;

        private AntSensor sensor;
        private bool eatOnArrival;
        private string lastEvent = "-";

        private void Awake()
        {
            if (ant != null) sensor = ant.GetComponent<AntSensor>();
        }

        private void OnEnable()
        {
            CameraController.Tapped += OnTap;
            if (ant != null) { ant.ActionFinished += OnActionFinished; ant.Ate += OnAte; }
        }

        private void OnDisable()
        {
            CameraController.Tapped -= OnTap;
            if (ant != null) { ant.ActionFinished -= OnActionFinished; ant.Ate -= OnAte; }
        }

        private void OnTap(Vector2 world)
        {
            if (ant == null) return;
            var zone = BuildZone.At(world);
            if (zone == null) return;

            Vector3 target = zone.Room != null ? zone.Room.WorkPosition : zone.EntrancePosition;
            eatOnArrival = false;
            bool ok = ant.MoveTo(target);
            lastEvent = ok ? $"MoverA → {zone.ZoneId}" : $"Sin camino a {zone.ZoneId}";
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || ant == null) return;

            if (kb.tKey.wasPressedThisFrame) { eatOnArrival = false; ant.Work(RoomHere(), workSeconds); lastEvent = "Trabajar"; }
            if (kb.cKey.wasPressedThisFrame) { eatOnArrival = false; ant.Eat(); lastEvent = "Comer"; }
            if (kb.pKey.wasPressedThisFrame) { eatOnArrival = false; ant.Stop(); lastEvent = "Parar"; }
            if (kb.hKey.wasPressedThisFrame) GoEat();
        }

        private void GoEat()
        {
            var storage = sensor != null ? sensor.NearestRoom(RoomType.Storage) : null;
            if (storage == null) { lastEvent = "No hay Despensa construida"; return; }
            eatOnArrival = ant.MoveTo(storage.WorkPosition);
            lastEvent = eatOnArrival ? $"Voy a comer a {storage.name}" : "Sin camino a la Despensa";
        }

        private void OnActionFinished(AntAction action)
        {
            lastEvent = $"Terminó: {action}";
            if (action == AntAction.Moving && eatOnArrival && sensor != null && sensor.IsAtTarget)
            {
                eatOnArrival = false;
                ant.Eat();
                lastEvent = "Llegó a la Despensa → Comer";
            }
        }

        private void OnAte(int rations) => lastEvent = rations > 0 ? $"Ha comido {rations} ración(es)" : "No había comida";

        private Room RoomHere()
        {
            foreach (var r in Room.All) if (r != null && sensor != null && sensor.IsAtRoom(r)) return r;
            return null;
        }

        private void OnGUI()
        {
            if (ant == null) return;
            var storage = sensor != null ? sensor.NearestRoom(RoomType.Storage) : null;
            int food = ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food) : -1;

            GUI.matrix = Matrix4x4.Scale(Vector3.one * 1.5f);
            GUILayout.BeginArea(new Rect(10, 10, 330, 300), GUI.skin.box);
            GUILayout.Label($"Acción: {ant.Current}   Velocidad: {ant.Speed:0.0}");
            if (sensor != null)
            {
                GUILayout.Label($"Hambre: {sensor.Hunger01:0.00}  →  TieneHambre: {sensor.IsHungry}");
                GUILayout.Label($"Energía: {sensor.Energy01:0.00}  →  EstaCansada: {sensor.IsTired}");
                GUILayout.Label($"EstaEnObjetivo: {sensor.IsAtTarget}");
                GUILayout.Label($"Despensa más cercana: {(storage != null ? storage.name : "ninguna")}");
            }
            GUILayout.Label($"Comida colonia: {(food >= 0 ? food.ToString() : "sin ResourceManager")}");
            GUILayout.Label($"Último: {lastEvent}");
            GUILayout.Label("Clic zona: mover · T trabajar · C comer · P parar · H ir a comer");
            GUILayout.EndArea();
        }
    }
}
