using System.Collections.Generic;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.Ants.Testing
{
    /// <summary>
    /// SOLO PRUEBAS (Scenes/Test/Test_B8.unity). Prueba del reparto en las salas y de las obreras ociosas (B8).
    ///   1 → asigna una obrera libre a la Despensa      2 → a la Guardería      3 → a la Sala Real
    ///   0 → quita la sala a todas (vuelven a estar libres: ociosas en la Sala Real o excavando si hay planos)
    /// Muestra arriba a la izquierda cuántas hay en cada sala y cuántas están libres.
    /// </summary>
    public class RoomSpreadTester : MonoBehaviour
    {
        private readonly List<Ant> buffer = new List<Ant>();
        private GUIStyle style;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) AssignOne(RoomType.Storage);
            if (kb.digit2Key.wasPressedThisFrame) AssignOne(RoomType.Nursery);
            if (kb.digit3Key.wasPressedThisFrame) AssignOne(RoomType.Royal);
            if (kb.digit0Key.wasPressedThisFrame) UnassignAll();
        }

        private static void AssignOne(RoomType type)
        {
            var room = Room.FindBuilt(type);
            if (room == null) { Debug.LogWarning($"[RoomSpreadTester] No hay ninguna sala {type} construida."); return; }
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive || a.IsQueen || a.AssignedRoom != null) continue;
                a.AssignedRoom = room;
                Debug.Log($"[RoomSpreadTester] {a.Name} → {type}", a);
                return;
            }
            Debug.LogWarning("[RoomSpreadTester] No quedan obreras libres.");
        }

        private void UnassignAll()
        {
            buffer.Clear();
            buffer.AddRange(Ant.All);
            foreach (var a in buffer) if (a != null && !a.IsQueen) a.AssignedRoom = null;
        }

        private void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.UpperLeft };
            int storage = 0, nursery = 0, royal = 0, free = 0;
            foreach (var a in Ant.All)
            {
                if (a == null || !a.IsAlive || a.IsQueen) continue;
                var r = a.AssignedRoom;
                if (r == null || r.Data == null) { free++; continue; }
                if (r.Data.type == RoomType.Storage) storage++;
                else if (r.Data.type == RoomType.Nursery) nursery++;
                else if (r.Data.type == RoomType.Royal) royal++;
            }
            GUI.Box(new Rect(10, 10, 520, 120),
                $"B8 · 1 Despensa ({storage})  2 Guardería ({nursery})  3 Sala Real ({royal})\n" +
                $"0 quitar todas · Libres: {free}", style);
        }
    }
}
