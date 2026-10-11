using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;

namespace Myrmutation.Colony.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/D2.unity). Construye la Guardería al empezar, aplica el visual de casta
    /// a las hormigas que ya estén en la escena y muestra un panel para alimentar larvas.
    /// No ponerlo en Game.unity.
    /// </summary>
    public class BroodTester : MonoBehaviour
    {
        [Tooltip("Salas a las que se llama CompleteBuild() al empezar (la Guardería)")]
        [SerializeField] private List<Room> roomsToBuild = new List<Room>();
        [Tooltip("Aplica escala y tinte de casta a las hormigas que ya están en la escena")]
        [SerializeField] private bool applyVisualToSceneAnts = true;
        [SerializeField] private int foodPerClick = 20;
        [Tooltip("Comida extra al empezar, para que las estaciones (D3) no maten a la colonia mientras pruebas")]
        [SerializeField] private int startFood = 100;

        private Queen queen;

        private void Start()
        {
            foreach (var r in roomsToBuild) if (r != null) r.CompleteBuild();
            queen = FindAnyObjectByType<Queen>();
            if (startFood > 0) ResourceManager.Instance?.Add(ResourceType.Food, startFood);

            if (applyVisualToSceneAnts)
                foreach (var a in Ant.All)
                    if (a != null && !a.IsQueen && a.Caste != null) Nursery.ApplyCasteVisual(a, a.Caste);
        }

        private void OnGUI()
        {
            int lf = GUI.skin.label.fontSize, bf = GUI.skin.button.fontSize;
            GUI.skin.label.fontSize = 11; GUI.skin.button.fontSize = 11;
            try { DrawPanel(); }
            finally { GUI.skin.label.fontSize = lf; GUI.skin.button.fontSize = bf; }
        }

        private bool collapsed;

        private void DrawPanel()
        {
            var rm = ResourceManager.Instance;
            var nursery = Nursery.Instance;

            GUILayout.BeginArea(new Rect(Screen.width - 270, 10, 260, collapsed ? 30 : Screen.height * 0.55f - 10), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>D2 · Reina y cría</b>", new GUIStyle(GUI.skin.label) { richText = true });
            if (GUILayout.Button(collapsed ? "+" : "–", GUILayout.Width(22))) collapsed = !collapsed;
            GUILayout.EndHorizontal();
            if (collapsed) { GUILayout.EndArea(); return; }

            if (rm == null) { GUILayout.Label("⚠ No hay ResourceManager"); GUILayout.EndArea(); return; }
            if (nursery == null) { GUILayout.Label("⚠ No hay Nursery"); GUILayout.EndArea(); return; }

            int workers = 0;
            foreach (var a in Ant.All) if (a != null && a.IsAlive && !a.IsQueen) workers++;
            GUILayout.Label($"Comida {rm.Get(ResourceType.Food)} · Obreras {workers}");
            GUILayout.Label($"Guardería {nursery.AllBrood.Count}/{nursery.Capacity} · cría x{nursery.SpeedMultiplier:0.00}");

            if (queen == null) queen = FindAnyObjectByType<Queen>();
            if (queen == null) GUILayout.Label("⚠ No hay reina (¿ha muerto o falta el componente Queen?)");
            else
            {
                string state = queen.LayProgress01 < 1f ? $"{queen.LayProgress01 * 100f:0}%" : Blocked(queen.Blocked);
                GUILayout.Label($"Reina: huevo {state} (-{queen.EggFoodCost} comida)");
            }

            GUILayout.Space(6);
            foreach (var b in nursery.AllBrood)
            {
                if (b.NeedsFood)
                {
                    GUILayout.Label($"#{b.Id} Larva · ¡HAMBRE! ({b.WaitingFood:0}s)");
                    GUILayout.BeginHorizontal();
                    foreach (var c in nursery.Castes)
                    {
                        if (c == null) continue;
                        GUI.enabled = nursery.CanAfford(c);
                        if (GUILayout.Button($"{Label(c)} {c.foodCostToRaise}")) nursery.Feed(b, c);
                        GUI.enabled = true;
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    string caste = b.Caste != null ? $" · {Label(b.Caste)}" : "";
                    GUILayout.Label($"#{b.Id} {Stage(b.Stage)}{caste} · {b.Progress01 * 100f:0}%");
                }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+{foodPerClick} comida")) rm.Add(ResourceType.Food, foodPerClick);
            if (GUILayout.Button("Huevo ya")) nursery.AddEgg();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string Label(Core.Data.CasteData c) =>
            !string.IsNullOrEmpty(c.displayName) ? c.displayName : c.caste.ToString();

        private static string Stage(BroodStage s) =>
            s == BroodStage.Egg ? "Huevo" : s == BroodStage.Larva ? "Larva" : "Pupa";

        private static string Blocked(Queen.LayBlock b)
        {
            switch (b)
            {
                case Queen.LayBlock.NoFood: return "ESPERANDO COMIDA";
                case Queen.LayBlock.NurseryFull: return "GUARDERÍA LLENA";
                case Queen.LayBlock.NoNursery: return "SIN GUARDERÍA";
                case Queen.LayBlock.Winter: return "INVIERNO";
                case Queen.LayBlock.Dead: return "MUERTA";
                default: return "lista";
            }
        }
    }
}
