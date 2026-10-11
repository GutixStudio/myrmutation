using Myrmutation.Core;
using Myrmutation.Economy;
using UnityEngine;

namespace Myrmutation.Expedition.Testing
{
    /// <summary>
    /// SOLO PARA PROBAR (escena Test/D.unity). Panel OnGUI para enviar expediciones sin UI real,
    /// forzar el invierno y ver las presas guardadas. No meter en Game.unity.
    /// </summary>
    public class ExpeditionDebug : MonoBehaviour
    {
        [SerializeField] private Rect area = new Rect(300, 10, 300, 330);
        private int count = 1;

        private void OnEnable()
        {
            EventBus.Subscribe<ExpeditionSent>(OnSent);
            EventBus.Subscribe<ExpeditionReturned>(OnReturned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExpeditionSent>(OnSent);
            EventBus.Unsubscribe<ExpeditionReturned>(OnReturned);
        }

        private void OnSent(ExpeditionSent e) => Debug.Log($"[D4] Enviadas {e.Ants.Count} hormigas · quedan en el nido {Ant.All.Count}");
        private void OnReturned(ExpeditionReturned e) =>
            Debug.Log($"[D4] Vuelven {e.Ants.Count} · hojas {e.Leaves} · presa {(e.Prey != null ? e.Prey.displayName : "—")} · hormigas en el nido {Ant.All.Count}");

        private void OnGUI()
        {
            var sys = ExpeditionSystem.Instance;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 14;

            if (sys == null) { GUILayout.Label("Falta ExpeditionSystem en la escena"); GUILayout.EndArea(); return; }

            GUILayout.Label("EXPEDICIÓN (D4)");
            GUILayout.Label($"Estación: {SeasonRules.CurrentSeason} · {(SeasonRules.ExpeditionsAllowed ? "permitidas" : "BLOQUEADAS")}");
            GUILayout.Label($"Obreras disponibles: {sys.CountAvailable()} · apuntadas: {sys.Volunteers.Count}");
            GUILayout.Label(sys.IsAway ? $"Fuera ({sys.AntsAway.Count}): {sys.TimeLeft:0.0} s" : "En casa");
            GUILayout.Label($"Hojas: {Get(ResourceType.Leaves)} · Presas: {sys.PreyCount}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−")) count = Mathf.Max(ExpeditionSystem.MinGroup, count - 1);
            GUILayout.Label($"  {count}  ({sys.PreyChance(count):P0} presa)");
            if (GUILayout.Button("+")) count = Mathf.Min(ExpeditionSystem.MaxGroup, count + 1);
            GUILayout.EndHorizontal();

            GUI.enabled = sys.CanSend(count, out var reason);
            if (GUILayout.Button("Enviar expedición")) sys.Send(count);
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(reason) && !sys.IsAway) GUILayout.Label(reason);

            if (sys.HasPrey && GUILayout.Button("Sacar presa (simula Festín)") && sys.TryTakePrey(out var prey))
                Debug.Log($"[D4] Sacada {prey.displayName} → gen {(prey.gene != null ? prey.gene.displayName : "ninguno")}");

            GUILayout.EndArea();
        }

        private static int Get(ResourceType t) => ResourceManager.Instance != null ? ResourceManager.Instance.Get(t) : -1;
    }
}
