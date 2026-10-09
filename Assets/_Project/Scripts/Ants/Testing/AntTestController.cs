using Myrmutation.Building;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Ants.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/B.unity). Controla una hormiga y muestra lo que piensa.
    ///
    /// Con cerebro (AntBrain activo):
    ///   Clic en una zona con sala → asignarle esa sala (Ant.AssignedRoom); ella decide el resto
    ///   N → quitarle la sala asignada
    ///   B → suspender / reanudar el cerebro
    ///
    /// Con el cerebro suspendido (o sin cerebro), órdenes manuales como en B1/B3:
    ///   Clic en una zona → MoverA · T trabajar · C comer · P parar · H ir a comer · R descansar
    /// </summary>
    public class AntTestController : MonoBehaviour
    {
        [SerializeField] private AntActuator ant;
        [SerializeField] private float workSeconds = 3f;

        private Ant antCore;
        private AntSensor sensor;
        private AntNeeds needs;
        private AntBrain brain;
        private bool eatOnArrival;
        private string lastEvent = "-";

        private bool BrainActive => brain != null && brain.enabled && !brain.IsSuspended;

        private void Awake()
        {
            if (ant == null) return;
            antCore = ant.GetComponent<Ant>();
            sensor = ant.GetComponent<AntSensor>();
            needs = ant.GetComponent<AntNeeds>();
            brain = ant.GetComponent<AntBrain>();
        }

        private void OnEnable()
        {
            CameraController.Tapped += OnTap;
            EventBus.Subscribe<AntDied>(OnAntDied);
            if (ant != null) { ant.ActionFinished += OnActionFinished; ant.Ate += OnAte; }
            if (brain != null) brain.StateChanged += OnBrainChanged;
        }

        private void OnDisable()
        {
            CameraController.Tapped -= OnTap;
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            if (ant != null) { ant.ActionFinished -= OnActionFinished; ant.Ate -= OnAte; }
            if (brain != null) brain.StateChanged -= OnBrainChanged;
        }

        // ================= ENTRADA =================

        private void OnTap(Vector2 world)
        {
            if (ant == null) return;
            var zone = BuildZone.At(world);
            if (zone == null) return;

            if (BrainActive)
            {
                if (zone.Room == null) { lastEvent = $"{zone.ZoneId} no tiene sala"; return; }
                antCore.AssignedRoom = zone.Room;
                lastEvent = $"Asignada a {zone.Room.name} ({zone.ZoneId})";
                return;
            }

            Vector3 target = zone.Room != null ? zone.Room.WorkPosition : zone.EntrancePosition;
            eatOnArrival = false;
            bool ok = ant.MoveTo(target);
            lastEvent = ok ? $"MoverA → {zone.ZoneId}" : $"Sin camino a {zone.ZoneId}";
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || ant == null) return;

            if (kb.bKey.wasPressedThisFrame) ToggleBrain();
            if (kb.nKey.wasPressedThisFrame) { antCore.AssignedRoom = null; lastEvent = "Sin sala asignada"; }

            bool manual = kb.tKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame
                          || kb.hKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame;
            if (!manual) return;
            if (BrainActive) { lastEvent = "Cerebro activo: pulsa B para órdenes manuales"; return; }

            if (kb.tKey.wasPressedThisFrame)
            {
                eatOnArrival = false;
                if (needs != null && !needs.CanWork) lastEvent = "Agotada: no puede trabajar";
                else { ant.Work(RoomHere(), workSeconds); lastEvent = "Trabajar"; }
            }
            if (kb.cKey.wasPressedThisFrame) { eatOnArrival = false; ant.Eat(Ration); lastEvent = $"Comer ({Ration} ración/es)"; }
            if (kb.pKey.wasPressedThisFrame) { eatOnArrival = false; ant.Stop(); lastEvent = "Parar"; }
            if (kb.hKey.wasPressedThisFrame) GoEat();
            if (kb.rKey.wasPressedThisFrame) ToggleRest();
        }

        private void ToggleBrain()
        {
            if (brain == null) { lastEvent = "La hormiga no tiene AntBrain"; return; }
            if (brain.IsSuspended) { brain.Resume(); lastEvent = "Cerebro reanudado"; }
            else { brain.Suspend(); ant.Stop(); lastEvent = "Cerebro suspendido: órdenes manuales"; }
        }

        // ================= ÓRDENES MANUALES =================

        private int Ration => needs != null ? needs.RationSize : 1;

        private void GoEat()
        {
            var storage = sensor != null ? sensor.NearestRoom(RoomType.Storage) : null;
            if (storage == null) { lastEvent = "No hay Despensa construida"; return; }
            eatOnArrival = ant.MoveTo(storage.WorkPosition);
            lastEvent = eatOnArrival ? $"Voy a comer a {storage.name}" : "Sin camino a la Despensa";
        }

        private void ToggleRest()
        {
            if (needs == null) { lastEvent = "La hormiga no tiene AntNeeds"; return; }
            eatOnArrival = false;
            if (needs.IsResting) { needs.StopRest(); lastEvent = "Deja de descansar"; return; }

            ant.Stop();
            var room = RoomHere();
            needs.StartRest(room);
            lastEvent = room != null ? $"Descansa en {room.name}" : "Descansa en el sitio";
        }

        // ================= EVENTOS =================

        private void OnActionFinished(AntAction action)
        {
            if (BrainActive) return;
            lastEvent = $"Terminó: {action}";
            if (action == AntAction.Moving && eatOnArrival && sensor != null && sensor.IsAtTarget)
            {
                eatOnArrival = false;
                ant.Eat(Ration);
                lastEvent = "Llegó a la Despensa → Comer";
            }
        }

        private void OnAte(int rations) => lastEvent = rations > 0 ? $"Ha comido {rations} ración(es)" : "No había comida";

        private void OnBrainChanged(BrainGoal goal, BrainStep step) => lastEvent = $"Cerebro: {goal}/{step}";

        private void OnAntDied(AntDied e)
        {
            if (antCore != null && e.Ant == antCore) lastEvent = $"MUERTA ({e.Cause})";
        }

        private Room RoomHere()
        {
            foreach (var r in Room.All) if (r != null && sensor != null && sensor.IsAtRoom(r)) return r;
            return null;
        }

        // ================= PANEL =================

        private void OnGUI()
        {
            int food = ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food) : -1;

            GUI.matrix = Matrix4x4.Scale(Vector3.one * 1.5f);
            GUILayout.BeginArea(new Rect(10, 10, 380, 420), GUI.skin.box);

            if (ant == null)
            {
                GUILayout.Label("La hormiga ha muerto.");
                GUILayout.Label($"Último: {lastEvent}");
                GUILayout.EndArea();
                return;
            }

            if (brain != null)
                GUILayout.Label($"CEREBRO: {(brain.IsSuspended ? "suspendido" : brain.StateName)}");
            GUILayout.Label($"Sala asignada: {(antCore.AssignedRoom != null ? antCore.AssignedRoom.name : "ninguna")}");
            GUILayout.Label($"Acción: {ant.Current}   Velocidad: {ant.Speed:0.0}");
            if (sensor != null)
            {
                GUILayout.Label($"Hambre: {sensor.Hunger01:0.00}  →  TieneHambre: {sensor.IsHungry}");
                GUILayout.Label($"Energía: {sensor.Energy01:0.00}  →  Cansada: {sensor.IsTired} · Descansada: {sensor.IsRested}");
                GUILayout.Label($"Descansando: {sensor.IsResting} · Agotada: {sensor.IsExhausted}");
            }
            if (needs != null)
            {
                GUILayout.Label($"Puede trabajar: {needs.CanWork} · Ración: {needs.RationSize}");
                if (needs.IsStarving) GUILayout.Label($"¡MURIENDO DE HAMBRE! {needs.StarvationTimeLeft:0.0} s");
            }
            GUILayout.Label($"Comida colonia: {(food >= 0 ? food.ToString() : "sin ResourceManager")}");
            GUILayout.Label($"Último: {lastEvent}");
            GUILayout.Label(BrainActive
                ? "Clic zona: asignar sala · N quitar sala · B suspender cerebro"
                : "Clic: mover · T trabajar · C comer · P parar · H comer · R descansar · B cerebro");
            GUILayout.EndArea();
        }
    }
}
