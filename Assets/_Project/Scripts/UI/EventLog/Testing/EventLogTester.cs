using System;
using Myrmutation.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myrmutation.UI.Testing
{
    /// <summary>
    /// SOLO PRUEBAS. Teclas 1-8: publica un LogMessage de cada categoría (como harán C8, D9…).
    /// Si hay hormigas o salas en la escena, el mensaje lleva foco en una al azar
    /// (para probar que tocar la entrada centra la cámara). Tecla 0: diez mensajes seguidos.
    /// </summary>
    public class EventLogTester : MonoBehaviour
    {
        private static readonly Key[] Keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
        private int counter;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var categories = (LogCategory[])Enum.GetValues(typeof(LogCategory));
            for (int i = 0; i < Keys.Length && i < categories.Length; i++)
                if (kb[Keys[i]].wasPressedThisFrame) Publish(categories[i]);

            if (kb.digit0Key.wasPressedThisFrame)
                for (int i = 0; i < 10; i++) Publish(categories[i % categories.Length]);
        }

        private void Publish(LogCategory c)
        {
            counter++;
            EventBus.Publish(new LogMessage(c, $"Mensaje de prueba nº {counter} ({c})", RandomFocus()));
        }

        private static Transform RandomFocus()
        {
            if (Ant.All.Count > 0 && UnityEngine.Random.value < 0.6f)
                return Ant.All[UnityEngine.Random.Range(0, Ant.All.Count)].transform;
            if (Room.All.Count > 0)
                return Room.All[UnityEngine.Random.Range(0, Room.All.Count)].transform;
            return null;
        }
    }
}
