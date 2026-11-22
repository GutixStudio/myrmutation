using System;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Economy.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/D.unity). Panel con lo que calcula ColonyStats (D6).
    /// Abajo a la izquierda, plegable. No ponerlo en Game.unity.
    /// </summary>
    public class ColonyStatsDebug : MonoBehaviour
    {
        [SerializeField] private bool startCollapsed = false;

        private bool collapsed;
        private Vector2 scroll;

        private void Awake() => collapsed = startCollapsed;

        private void OnGUI()
        {
            int lf = GUI.skin.label.fontSize, bf = GUI.skin.button.fontSize;
            GUI.skin.label.fontSize = 11; GUI.skin.button.fontSize = 11;
            try { DrawPanel(); }
            finally { GUI.skin.label.fontSize = lf; GUI.skin.button.fontSize = bf; }
        }

        private void DrawPanel()
        {
            var s = ColonyStats.Instance;
            // Debajo del panel de estaciones de D3 (10, 10, 280, 400).
            float top = 420f;
            float h = collapsed ? 30f : Mathf.Max(120f, Screen.height - top - 10f);
            GUILayout.BeginArea(new Rect(10, top, 280, h), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>D6 · Estadísticas</b>", new GUIStyle(GUI.skin.label) { richText = true });
            if (GUILayout.Button(collapsed ? "+" : "–", GUILayout.Width(22))) collapsed = !collapsed;
            GUILayout.EndHorizontal();
            if (collapsed) { GUILayout.EndArea(); return; }

            if (s == null) { GUILayout.Label("⚠ No hay ColonyStats en la escena"); GUILayout.EndArea(); return; }

            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label($"Tiempo de juego medido: {s.GameTime:0}s");

            GUILayout.Label("<b>Por minuto (últimos 60 s)</b>", new GUIStyle(GUI.skin.label) { richText = true });
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            {
                float net = s.NetPerMinute(t);
                GUILayout.Label($"{Name(t)}: +{s.Produced(t):0.0}  −{s.Consumed(t):0.0}  = {(net >= 0 ? "+" : "")}{net:0.0}");
            }

            GUILayout.Label("<b>Invierno</b>", new GUIStyle(GUI.skin.label) { richText = true });
            float covered = s.WinterDaysCovered;
            string coveredText = float.IsPositiveInfinity(covered) ? "∞" : covered.ToString("0.0");
            GUILayout.Label($"Faltan {s.DaysUntilWinter} días · consumo {s.WinterDailyConsumption:0.0}/día");
            GUILayout.Label($"Cubre {coveredText} de {s.DaysPerSeason} días " +
                            (s.IsReadyForWinter ? "✔" : $"✘ (necesita {s.WinterFoodNeeded:0})"));
            GUILayout.Label($"Comida prevista al llegar: {s.ProjectedFoodAtWinter:0}");

            GUILayout.Label("<b>Población</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Total {s.Population} · Reina {s.Queens}");
            GUILayout.Label($"Menor {s.CountByCaste(CasteType.Minor)} · Media {s.CountByCaste(CasteType.Media)} · " +
                            $"Mayor {s.CountByCaste(CasteType.Major)} · Sin casta {s.WithoutCaste}");
            GUILayout.Label($"Libres {s.FreeWorkers} · Con sala {s.AssignedWorkers} · Expedición {s.OnExpedition}");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string Name(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Food: return "Comida";
                case ResourceType.Leaves: return "Hojas";
                case ResourceType.Fungus: return "Hongo";
                case ResourceType.Biomass: return "Biomasa";
                default: return t.ToString();
            }
        }
    }
}
