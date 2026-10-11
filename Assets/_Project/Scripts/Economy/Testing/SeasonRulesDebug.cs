using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy.Testing
{
    /// <summary>
    /// SOLO PARA PROBAR (escena Test/D.unity). Muestra día, estación, comida y hormigas en pantalla
    /// y tiene botones para acelerar el tiempo, tocar la comida y matar a la reina.
    /// No meter en Game.unity.
    /// </summary>
    public class SeasonRulesDebug : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Subscribe<DayPassed>(OnDay);
            EventBus.Subscribe<SeasonChanged>(OnSeason);
            EventBus.Subscribe<AntDied>(OnAntDied);
            EventBus.Subscribe<GameStateChanged>(OnState);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayPassed>(OnDay);
            EventBus.Unsubscribe<SeasonChanged>(OnSeason);
            EventBus.Unsubscribe<AntDied>(OnAntDied);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
        }

        private void OnDay(DayPassed e) => Debug.Log($"[D3] Día {e.Day} · comida {Food()} · hormigas {Ant.All.Count}");
        private void OnSeason(SeasonChanged e) => Debug.Log($"[D3] Nueva estación: {e.Season}");
        private void OnAntDied(AntDied e) => Debug.Log($"[D3] Muere {(e.Ant.IsQueen ? "la REINA" : e.Ant.Name)} ({e.Cause})");
        private void OnState(GameStateChanged e) => Debug.Log($"[D3] Estado: {e.State}");

        private static int Food() => ResourceManager.Instance != null ? ResourceManager.Instance.Get(ResourceType.Food) : -1;

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            GUILayout.BeginArea(new Rect(10, 10, 280, 400), GUI.skin.box);
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 14;

            if (gm == null) { GUILayout.Label("Falta GameManager en la escena"); GUILayout.EndArea(); return; }

            GUILayout.Label($"Estado: {gm.State}");
            GUILayout.Label($"Día {gm.Day} · {gm.CurrentSeason} ({gm.DayProgress:P0})");
            GUILayout.Label($"Comida: {Food()}");
            GUILayout.Label($"Hormigas vivas: {Ant.All.Count}");
            GUILayout.Label($"Coste al pasar el día: {SeasonRules.UpkeepToday}");
            GUILayout.Label($"Hambre x{SeasonRules.HungerMultiplier}");
            GUILayout.Label($"Expediciones: {(SeasonRules.ExpeditionsAllowed ? "SÍ" : "NO")}");
            GUILayout.Label($"Velocidad (timeScale): {Time.timeScale}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("x1")) gm.SetSpeed(1);
            if (GUILayout.Button("x3")) gm.SetSpeed(3);
            if (GUILayout.Button("x10")) Time.timeScale = 10f; // solo debug
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+20 comida")) ResourceManager.Instance?.Add(ResourceType.Food, 20);
            if (GUILayout.Button("Comida a 0")) ResourceManager.Instance?.Add(ResourceType.Food, -Food());
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Matar a la reina"))
                foreach (var a in Ant.All) if (a.IsQueen) { a.Kill(DeathCause.Other); break; }

            GUILayout.EndArea();
        }
    }
}
