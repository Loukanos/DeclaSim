using NUnit.Framework;
using ParticleSim.Core;
using UnityEngine;

namespace Tests
{
    public class SwapCollisionTests
    {
        private static string Mover(string type, string direction) =>
            $"`{type}`: {{ `onStep`: [ {{ `actions`: [ `move({direction})` ] }} ] }}";

        [TestCase("X")]
        [TestCase("Y")]
        [TestCase("Z")]
        public void HeadOnSwap_LeavesBothInPlace_WithOneCollisionEach(string axis)
        {
            int ax = axis == "X" ? 1 : 0, ay = axis == "Y" ? 1 : 0, az = axis == "Z" ? 1 : 0;
            TestSimulation sim = TestSimulation.Load(
                Json.Scenario(3, (1, "P", 0, 0, 0), (2, "N", ax, ay, az)),
                Json.Of("{ " + Mover("P", axis + "+") + ", " + Mover("N", axis + "-") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(ax, ay, az)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(2));
            Assert.That(sim.Metrics.TotalMovements, Is.EqualTo(0));
            Assert.That(sim.Particle(1).LastMoveResult, Is.EqualTo(MoveOutcome.BlockedByCollision));
            Assert.That(sim.Particle(2).LastMoveResult, Is.EqualTo(MoveOutcome.BlockedByCollision));
        }

        [Test]
        public void BlockedSwap_PropagatesToParticlesQueuedBehindIt()
        {
            // A (1) and B (2) swap; C (3) heads for A's cell and D (4) follows C.
            TestSimulation sim = TestSimulation.Load(
                Json.Scenario(5, (1, "P", 1, 0, 0), (2, "N", 2, 0, 0), (3, "P", 0, 0, 0), (4, "Q", 0, 1, 0)),
                Json.Of("{ " + Mover("P", "X+") + ", " + Mover("N", "X-") + ", " + Mover("Q", "Y-") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(2, 0, 0)));
            Assert.That(sim.Particle(3).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Particle(4).GridPosition, Is.EqualTo(new Vector3Int(0, 1, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(4));
            Assert.That(sim.Particle(4).LastMoveResult, Is.EqualTo(MoveOutcome.BlockedByCollision));
        }

        [Test]
        public void FourParticleRotation_InATwoByTwoSquare_StillMoves()
        {
            TestSimulation sim = TestSimulation.Load(
                Json.Scenario(2, (1, "A", 0, 0, 0), (2, "B", 1, 0, 0), (3, "C", 1, 1, 0), (4, "D", 0, 1, 0)),
                Json.Of("{ " + Mover("A", "X+") + ", " + Mover("B", "Y+") + ", " + Mover("C", "X-") + ", " + Mover("D", "Y-") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(1, 1, 0)));
            Assert.That(sim.Particle(3).GridPosition, Is.EqualTo(new Vector3Int(0, 1, 0)));
            Assert.That(sim.Particle(4).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(0));
        }

        [Test]
        public void QueueMovingTogether_StillMoves()
        {
            TestSimulation sim = TestSimulation.Load(
                Json.Scenario(5, (1, "P", 0, 0, 0), (2, "P", 1, 0, 0), (3, "P", 2, 0, 0)),
                Json.Of("{ " + Mover("P", "X+") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(2, 0, 0)));
            Assert.That(sim.Particle(3).GridPosition, Is.EqualTo(new Vector3Int(3, 0, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(0));
        }
    }
}
