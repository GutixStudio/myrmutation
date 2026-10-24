using System.Collections.Generic;
using System.Reflection;
using Myrmutation.Ants;
using Myrmutation.Building;
using Myrmutation.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>
    /// EditMode. Cuadrilla temporal de DigTestDriver (A12, mientras no está B6): a quién toma, cuándo devuelve el cerebro
    /// y cuándo la vuelve a tomar. Fuera del modo Play no hay Awake ni OnEnable: se llaman a mano y Ant.All se rellena aquí.
    /// </summary>
    public class DigTestDriverTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private DigTestDriver driver;
        private readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            driver = new GameObject("DigTestDriver (test)").AddComponent<DigTestDriver>();
            created.Add(driver.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) Object.DestroyImmediate(go);
            created.Clear();
            Ant.All.Clear();
        }

        [Test]
        public void TomaObrerasSinSala_NuncaLaReinaNiLasQueTienenSala()
        {
            var worker = NewAnt(false);
            var queen = NewAnt(true);
            var assigned = NewAnt(false);
            assigned.AssignedRoom = new GameObject("Room (test)").AddComponent<Room>();
            created.Add(assigned.AssignedRoom.gameObject);

            UpdateCrew();

            CollectionAssert.AreEquivalent(new[] { worker }, Crew());
            Assert.IsTrue(worker.GetComponent<AntBrain>().IsSuspended);
            Assert.IsFalse(queen.GetComponent<AntBrain>().IsSuspended);
            Assert.IsFalse(assigned.GetComponent<AntBrain>().IsSuspended);
        }

        [Test]
        public void ConHambre_DevuelveElCerebro_YNoLaRetomaHastaQueSeRecupera()
        {
            var ant = NewAnt(false);
            var needs = ant.GetComponent<AntNeeds>();
            UpdateCrew();
            Assert.AreEqual(1, Crew().Count);

            Set(needs, "hunger", 0.7f);   // por encima de hungryThreshold (0,6)
            UpdateCrew();
            Assert.AreEqual(0, Crew().Count);
            Assert.IsFalse(ant.GetComponent<AntBrain>().IsSuspended, "el cerebro la manda a comer");

            Set(needs, "hunger", 0f);
            Set(needs, "energy", 0.9f);   // ni cansada ni descansada (restedThreshold 0,95)
            UpdateCrew();
            Assert.AreEqual(0, Crew().Count, "aún no se ha recuperado");

            Set(needs, "energy", 1f);
            UpdateCrew();
            Assert.AreEqual(1, Crew().Count);
        }

        [Test]
        public void SiLeAsignanSala_SaleDeLaCuadrilla()
        {
            var ant = NewAnt(false);
            UpdateCrew();
            ant.AssignedRoom = new GameObject("Room (test)").AddComponent<Room>();
            created.Add(ant.AssignedRoom.gameObject);

            UpdateCrew();

            Assert.AreEqual(0, Crew().Count);
            Assert.IsFalse(ant.GetComponent<AntBrain>().IsSuspended);
        }

        [Test]
        public void NoPasaDeCrewSize()
        {
            typeof(DigTestDriver).GetField("crewSize", Private).SetValue(driver, 2);
            for (int i = 0; i < 4; i++) NewAnt(false);
            UpdateCrew();
            Assert.AreEqual(2, Crew().Count);
        }

        private Ant NewAnt(bool queen)
        {
            var go = new GameObject(queen ? "Queen (test)" : "Ant (test)");
            created.Add(go);
            var ant = go.AddComponent<Ant>();
            Set(ant, "isQueen", queen);
            go.AddComponent<AntBrain>();   // añade AntActuator, AntSensor y AntNeeds
            foreach (var c in new MonoBehaviour[] { ant, go.GetComponent<AntActuator>(), go.GetComponent<AntNeeds>(), go.GetComponent<AntSensor>(), go.GetComponent<AntBrain>() })
                c.GetType().GetMethod("Awake", Private)?.Invoke(c, null);
            Ant.All.Add(ant);
            return ant;
        }

        private void UpdateCrew() => typeof(DigTestDriver).GetMethod("UpdateCrew", Private).Invoke(driver, null);

        private HashSet<Ant> Crew() => (HashSet<Ant>)typeof(DigTestDriver).GetField("crew", Private).GetValue(driver);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
    }
}
