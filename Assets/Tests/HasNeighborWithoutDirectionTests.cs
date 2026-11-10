using NUnit.Framework;

namespace Tests
{
    public class HasNeighborWithoutDirectionTests
    {
        private static TestSimulation RunCentre(string onStep, params (int x, int y, int z)[] neighbours)
        {
            var particles = new (int, string, int, int, int)[neighbours.Length + 1];
            particles[0] = (1, "M", 1, 1, 1);
            for (int i = 0; i < neighbours.Length; i++)
                particles[i + 1] = (i + 2, "N", neighbours[i].x, neighbours[i].y, neighbours[i].z);

            TestSimulation sim = TestSimulation.Load(Json.Scenario(3, particles),
                Json.Of("{ `M`: { `onStep`: [ " + onStep + " ] }, `N`: { } }"));
            sim.Step();
            return sim;
        }

        private const string SawAnyNeighbour = "{ `condition`: `hasNeighbor()`, `actions`: [ `setState('viu', true)` ] }";

        [TestCase(2, 1, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(1, 2, 1)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 2)]
        [TestCase(1, 1, 0)]
        public void IsTrue_WithANeighbourOnAnyFace(int x, int y, int z)
        {
            TestSimulation sim = RunCentre(SawAnyNeighbour, (x, y, z));

            Assert.That(sim.State(1, "viu"), Is.EqualTo(true));
        }

        [Test]
        public void IsFalse_WithoutNeighbours()
        {
            TestSimulation sim = RunCentre(SawAnyNeighbour);

            Assert.That(sim.State(1, "viu"), Is.Null);
        }

        [Test]
        public void IsFalse_WithANeighbourOnlyOnTheDiagonal()
        {
            TestSimulation sim = RunCentre(SawAnyNeighbour, (2, 2, 1), (0, 0, 0));

            Assert.That(sim.State(1, "viu"), Is.Null);
        }

        [Test]
        public void WorksAsAValue()
        {
            TestSimulation alone = RunCentre("{ `actions`: [ `setState('v', hasNeighbor())` ] }");
            TestSimulation touching = RunCentre("{ `actions`: [ `setState('v', hasNeighbor())` ] }", (1, 2, 1));

            Assert.That(alone.State(1, "v"), Is.EqualTo(false));
            Assert.That(touching.State(1, "v"), Is.EqualTo(true));
        }

        [Test]
        public void WorksWithNot()
        {
            const string isolated = "{ `condition`: `Not hasNeighbor()`, `actions`: [ `setState('sozinha', true)` ] }";

            Assert.That(RunCentre(isolated).State(1, "sozinha"), Is.EqualTo(true));
            Assert.That(RunCentre(isolated, (0, 1, 1)).State(1, "sozinha"), Is.Null);
        }

        [Test]
        public void WithoutParentheses_InACondition_EqualsTheCallWithoutArguments()
        {
            const string bare = "{ `condition`: `hasNeighbor`, `actions`: [ `setState('viu', true)` ] }";

            Assert.That(RunCentre(bare, (1, 1, 2)).State(1, "viu"), Is.EqualTo(true));
            Assert.That(RunCentre(bare).State(1, "viu"), Is.Null);
        }

        [Test]
        public void WithoutParentheses_AsAValue_IsText_LikeMyId()
        {
            TestSimulation sim = RunCentre("{ `actions`: [ `setState('v', hasNeighbor)`, `setState('id', myId)` ] }", (1, 1, 2));

            Assert.That(sim.State(1, "v"), Is.EqualTo("hasNeighbor"));
            Assert.That(sim.State(1, "id"), Is.EqualTo("myId"));
        }

        [Test]
        public void WithADirection_StillChecksOnlyThatFace()
        {
            const string sawRight = "{ `condition`: `hasNeighbor('X+')`, `actions`: [ `setState('viu', true)` ] }";

            Assert.That(RunCentre(sawRight, (2, 1, 1)).State(1, "viu"), Is.EqualTo(true));
            Assert.That(RunCentre(sawRight, (0, 1, 1)).State(1, "viu"), Is.Null);
        }

        [Test]
        public void TwoArguments_AreStillRejected()
        {
            string errors = TestSimulation.BehaviorErrors(Json.Scenario(3, (1, "M", 1, 1, 1)),
                Json.Of("{ `M`: { `onStep`: [ { `condition`: `hasNeighbor('X+', 'Y+')`, `actions`: [ `wait` ] } ] } }"));

            Assert.That(errors, Does.Contain("'hasNeighbor' espera 0 ou 1 argumento(s), mas recebeu 2"));
        }
    }
}
