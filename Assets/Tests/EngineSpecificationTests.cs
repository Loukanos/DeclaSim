using System.Collections.Generic;
using NUnit.Framework;
using ParticleSim.Core;
using UnityEngine;

namespace Tests
{
    public class EngineSpecificationTests
    {
        private static string Rule(string type, string actions, string condition = null) =>
            $"`{type}`: {{ `onStep`: [ {{ {(condition == null ? "" : $"`condition`: `{condition}`, ")}`actions`: [ {actions} ] }} ] }}";

        private static string Scenario(int size, int maxCycles, int idleCyclesLimit, params (int id, string type, int x, int y, int z)[] particles)
        {
            var entries = new List<string>();
            foreach (var p in particles)
                entries.Add($"{{\"id\": {p.id}, \"type\": \"{p.type}\", \"position\": [{p.x}, {p.y}, {p.z}]}}");
            return $"{{\"gridSize\": [{size}, {size}, {size}], \"maxCycles\": {maxCycles}, \"idleCyclesLimit\": {idleCyclesLimit}, \"particles\": [{string.Join(", ", entries)}]}}";
        }

        [Test]
        public void TwoParticlesWantingTheSameCell_NeitherMoves_EachCountsACollision()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "A", 0, 0, 0), (2, "B", 2, 0, 0)),
                Json.Of("{ " + Rule("A", "`move(X+)`") + ", " + Rule("B", "`move(X-)`") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(2, 0, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(2));
            Assert.That(sim.Particle(1).LastMoveResult, Is.EqualTo(MoveOutcome.BlockedByCollision));
        }

        [Test]
        public void MovingIntoAStationaryParticle_IsBlocked_OneCollision()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "A", 0, 0, 0), (2, "S", 1, 0, 0)),
                Json.Of("{ " + Rule("A", "`move(X+)`") + ", `S`: { } }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(1));
        }

        [Test]
        public void BlockedParticle_BlocksTheOneBehindIt()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(4, (1, "A", 0, 0, 0), (2, "A", 1, 0, 0), (3, "S", 2, 0, 0)),
                Json.Of("{ " + Rule("A", "`move(X+)`") + ", `S`: { } }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Particle(2).GridPosition, Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(2));
        }

        [Test]
        public void MovingOffTheGrid_IsAnInvalidMove_NotACollision()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "A", 0, 0, 0)), Json.Of("{ " + Rule("A", "`move(X-)`") + " }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(0, 0, 0)));
            Assert.That(sim.Metrics.TotalInvalidMovements, Is.EqualTo(1));
            Assert.That(sim.Metrics.TotalCollisions, Is.EqualTo(0));
            Assert.That(sim.Particle(1).LastMoveResult, Is.EqualTo(MoveOutcome.Invalid));
        }

        [Test]
        public void OnlyTheFirstMoveOfACycle_IsExecuted()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "A", 0, 0, 0)), Json.Of(
                "{ `A`: { `onStep`: [ { `actions`: [ `move(X+)`, `move(Y+)` ] }, { `actions`: [ `move(Z+)` ] } ] } }"));

            sim.Step();

            Assert.That(sim.Particle(1).GridPosition, Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(sim.Metrics.TotalMovements, Is.EqualTo(1));
        }

        [Test]
        public void AMessage_IsOnlyDeliveredInTheNextCycle()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "E", 0, 0, 0), (2, "R", 1, 0, 0)), Json.Of(
                "{ " + Rule("E", "`sendMessage(X+, 'oi')`, `setState('enviou', true)`", "checkState('enviou', '!=', true)") +
                ", " + Rule("R", "`setState('recebeu', +1)`", "checkMessage('oi')") + " }"));

            sim.Step();
            Assert.That(sim.State(2, "recebeu"), Is.Null);

            sim.Step();
            Assert.That(sim.State(2, "recebeu"), Is.EqualTo(1.0));
        }

        [Test]
        public void Events_RunInTheOrder_OnMessage_OnTouch_OnStep()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "E", 0, 0, 0), (2, "R", 1, 0, 0)), Json.Of(
                "{ `E`: { `onInit`: [ { `actions`: [ `sendMessage(X+, 'm')` ] } ] }, `R`: {" +
                " `onStep`: [ { `condition`: `checkState('ordem', '==', 2)`, `actions`: [ `setState('ordem', 3)` ] } ]," +
                " `onTouch`: [ { `condition`: `checkState('ordem', '==', 1)`, `actions`: [ `setState('ordem', 2)` ] } ]," +
                " `onMessage`: [ { `actions`: [ `setState('ordem', 1)` ] } ] } }"));

            sim.Step();

            Assert.That(sim.State(2, "ordem"), Is.EqualTo(3.0));
        }

        [Test]
        public void MoveInOnInit_IsRejectedAtLoad()
        {
            string errors = TestSimulation.BehaviorErrors(Json.Scenario(3, (1, "A", 0, 0, 0)),
                Json.Of("{ `A`: { `onInit`: [ { `actions`: [ `move(X+)` ] } ] } }"));

            Assert.That(errors, Does.Contain("ações de movimento não são permitidas em onInit"));
        }

        [Test]
        public void StateAndColour_AreAppliedImmediately_WithinTheCycle()
        {
            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, (1, "A", 0, 0, 0)), Json.Of(
                "{ `A`: { `onStep`: [ { `actions`: [ `setColor('#FF0000')`, `setState('x', 1)` ] }," +
                " { `condition`: `checkState('x', '==', 1)`, `actions`: [ `setState('viuNoMesmoCiclo', true)` ] } ] } }"));
            var events = new List<string>();
            sim.Engine.OnColorChanged += (_, __) => events.Add("cor");
            sim.Engine.OnCycleCompleted += (_, __) => events.Add("fim do ciclo");

            sim.Step();

            Assert.That(sim.State(1, "viuNoMesmoCiclo"), Is.EqualTo(true));
            Assert.That(events, Is.EqualTo(new[] { "cor", "fim do ciclo" }));
        }

        [Test]
        public void MaxCycles_EndsTheRun()
        {
            TestSimulation sim = TestSimulation.Load(Scenario(3, 4, 0, (1, "A", 0, 0, 0)), Json.Of("{ " + Rule("A", "`setState('t', +1)`") + " }"));

            sim.Step(10);

            Assert.That(sim.Engine.IsFinished, Is.True);
            Assert.That(sim.Engine.CurrentCycle, Is.EqualTo(4));
            Assert.That(sim.Metrics.FinishReason, Is.EqualTo("atingiu o limite de 4 ciclos"));
        }

        [Test]
        public void Inactivity_EndsTheRun()
        {
            TestSimulation sim = TestSimulation.Load(Scenario(3, 0, 3, (1, "A", 0, 0, 0)), Json.Of("{ " + Rule("A", "`wait`") + " }"));

            sim.Step(10);

            Assert.That(sim.Engine.CurrentCycle, Is.EqualTo(3));
            Assert.That(sim.Metrics.FinishReason, Is.EqualTo("sem atividade por 3 ciclo(s)"));
        }

        [Test]
        public void MaxCyclesWins_WhenBothCriteriaAreMetInTheSameCycle()
        {
            TestSimulation sim = TestSimulation.Load(Scenario(3, 3, 3, (1, "A", 0, 0, 0)), Json.Of("{ " + Rule("A", "`wait`") + " }"));

            sim.Step(10);

            Assert.That(sim.Engine.CurrentCycle, Is.EqualTo(3));
            Assert.That(sim.Metrics.FinishReason, Is.EqualTo("atingiu o limite de 3 ciclos"));
        }

        // S sits on A's Y+ face. With idleCyclesLimit 2, a run still going after 5 cycles had activity every cycle.
        [TestCase("move(X+)", true)]
        [TestCase("setState('x', 1)", true)]
        [TestCase("setColor('#00FF00')", true)]
        [TestCase("sendMessage(Y+, 'oi')", true)]
        [TestCase("sendMessage(Y-, 'oi')", false)]
        [TestCase("wait", false)]
        [TestCase("move(Y-)", false)]
        [TestCase("move(Y+)", false)]
        public void ActivityCriterion(string action, bool countsAsActivity)
        {
            TestSimulation sim = TestSimulation.Load(Scenario(8, 0, 2, (1, "A", 0, 0, 0), (2, "S", 0, 1, 0)),
                Json.Of("{ " + Rule("A", "`" + action + "`") + ", `S`: { } }"));

            sim.Step(5);

            Assert.That(sim.Engine.IsFinished, Is.EqualTo(!countsAsActivity));
        }
    }
}
