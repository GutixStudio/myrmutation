using System.Reflection;
using Myrmutation.Core;
using NUnit.Framework;
using UnityEngine;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode (Window → General → Test Runner). Unity no llama a Update fuera de Play,
    /// así que el test pasa los días a mano por reflexión.
    /// </summary>
    public class CalendarTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SeGanaJustoAlTerminarElInvierno()
        {
            var go = new GameObject("GameManager (test)");
            try
            {
                var gm = go.AddComponent<GameManager>();
                var type = typeof(GameManager);
                var dayTimer = type.GetField("dayTimer", Private);
                float secondsPerDay = (float)type.GetField("secondsPerDay", Private).GetValue(gm);
                int daysPerSeason = (int)type.GetField("daysPerSeason", Private).GetValue(gm);
                var update = type.GetMethod("Update", Private);

                void PassDay()
                {
                    dayTimer.SetValue(gm, secondsPerDay);
                    update.Invoke(gm, null);
                }

                // Primavera, verano, otoño e invierno: el último día de invierno todavía se juega.
                int totalDays = 4 * daysPerSeason;
                for (int i = 0; i < totalDays - 1; i++) PassDay();
                Assert.AreEqual(GameState.Playing, gm.State);
                Assert.AreEqual(Season.Winter, gm.CurrentSeason);

                PassDay();
                Assert.AreEqual(GameState.Victory, gm.State);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
