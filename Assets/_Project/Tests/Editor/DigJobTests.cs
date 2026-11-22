using System;
using Myrmutation.Building;
using Myrmutation.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Myrmutation.Tests
{
    /// <summary>EditMode (Window → General → Test Runner). Las Ant son componentes sueltos: Unity no llama a su Awake fuera de Play.</summary>
    public class DigJobTests
    {
        private const float Eps = 1e-4f;

        private Ant ant1, ant2, ant3;

        [SetUp]
        public void SetUp()
        {
            ant1 = new GameObject("Ant 1 (test)").AddComponent<Ant>();
            ant2 = new GameObject("Ant 2 (test)").AddComponent<Ant>();
            ant3 = new GameObject("Ant 3 (test)").AddComponent<Ant>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ant1.gameObject);
            Object.DestroyImmediate(ant2.gameObject);
            Object.DestroyImmediate(ant3.gameObject);
        }

        private static DigJob NewJob(float cost = 2f) => new DigJob(Vector2Int.zero, Vector3.zero, true, cost);

        [Test]
        public void Work_LlegaA1YTerminaConElCosteDado()
        {
            var job = NewJob(2f);

            job.Work(1f, 1f);
            Assert.AreEqual(0.5f, job.Progress, Eps);
            Assert.IsFalse(job.IsDone);

            job.Work(1f, 1f);
            Assert.AreEqual(1f, job.Progress, Eps);
            Assert.IsTrue(job.IsDone);
        }

        [Test]
        public void Release_BajaObrerasYConservaProgreso()
        {
            var job = NewJob(2f);
            job.Claim(ant1);
            job.Work(1f, 0.5f);

            job.Release(ant1);

            Assert.AreEqual(0, job.Workers);
            Assert.AreEqual(0.25f, job.Progress, Eps);
        }

        [Test]
        public void Work_ConDtCeroNoAvanza()
        {
            var job = NewJob();
            job.Work(1f, 0f);
            Assert.AreEqual(0f, job.Progress, Eps);
        }

        [Test]
        public void Claim_DosVecesSiYALaTerceraNo()
        {
            var job = NewJob();
            Assert.IsTrue(job.Claim(ant1));
            Assert.IsTrue(job.Claim(ant2));
            Assert.IsFalse(job.Claim(ant3));
            Assert.AreEqual(DigJob.MaxWorkers, job.Workers);
        }

        [Test]
        public void Claim_FallaConElTrabajoTerminado()
        {
            var job = NewJob(1f);
            job.Work(1f, 1f);
            Assert.IsFalse(job.Claim(ant1));
            Assert.AreEqual(0, job.Workers);
        }

        [Test]
        public void Claim_FallaConNull()
        {
            var job = NewJob();
            Assert.IsFalse(job.Claim(null));
            Assert.AreEqual(0, job.Workers);
        }

        [Test]
        public void Claim_LaMismaHormigaNoReservaDosVeces()
        {
            var job = NewJob();
            Assert.IsTrue(job.Claim(ant1));
            Assert.IsFalse(job.Claim(ant1));
            Assert.AreEqual(1, job.Workers);
        }

        [Test]
        public void Release_SinObrerasNoBajaDe0()
        {
            var job = NewJob();
            job.Release(ant1);
            Assert.AreEqual(0, job.Workers);
        }

        [Test]
        public void Release_DosVecesConLaMismaHormigaSoloRestaUna()
        {
            var job = NewJob();
            job.Claim(ant1);
            job.Claim(ant2);

            job.Release(ant1);
            job.Release(ant1);

            Assert.AreEqual(1, job.Workers);
        }

        [Test]
        public void Release_ConHormigaQueNoLoTeniaNoCambiaNada()
        {
            var job = NewJob();
            job.Claim(ant1);
            job.Work(1f, 0.5f);

            job.Release(ant2);

            Assert.AreEqual(1, job.Workers);
            Assert.AreEqual(0.25f, job.Progress, Eps);
        }

        [Test]
        public void Claim_HormigaDestruidaLiberaSuHueco()
        {
            var job = NewJob();
            var dead = new GameObject("Ant muerta (test)").AddComponent<Ant>();
            job.Claim(ant1);
            job.Claim(dead);
            Assert.IsFalse(job.CanClaim);

            Object.DestroyImmediate(dead.gameObject);

            Assert.AreEqual(1, job.Workers);
            Assert.IsTrue(job.CanClaim);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_ConCosteNoPositivoLanza(float cost)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NewJob(cost));
        }
    }
}
