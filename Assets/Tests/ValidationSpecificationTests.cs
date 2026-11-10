using NUnit.Framework;

namespace Tests
{
    public class ValidationSpecificationTests
    {
        private static readonly string Behavior = Json.Of("{ `A`: { `onStep`: [ { `actions`: [ `wait` ] } ] } }");
        private static readonly string Scenario = Json.Scenario(3, (1, "A", 0, 0, 0));

        private static string ScenarioErrors(string scenario) => TestSimulation.ScenarioErrors(Json.Of(scenario), Behavior);

        private static string RuleErrors(string rule) => TestSimulation.BehaviorErrors(Scenario, Json.Of("{ `A`: { `onStep`: [ " + rule + " ] } }"));

        [TestCase("{ `particles`: [ ] }", "Cenário: 'gridSize' é obrigatório e deve ser uma lista [x, y, z].")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `type`: `A`, `position`: [0, 0, 0] } ] }", "Cenário: partícula #1: 'id' é obrigatório.")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: 1, `position`: [0, 0, 0] } ] }", "Cenário: partícula id 1: 'type' é obrigatório.")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: 1, `type`: `A` } ] }", "Cenário: partícula id 1: 'position' é obrigatório e deve ser uma lista [x, y, z].")]
        public void MissingRequiredScenarioField_IsRejected(string scenario, string expected)
        {
            Assert.That(ScenarioErrors(scenario), Does.Contain(expected));
        }

        [Test]
        public void MissingActions_IsRejected()
        {
            Assert.That(RuleErrors("{ `condition`: `true` }"), Does.Contain("'actions' é obrigatório e deve ser uma lista de textos."));
        }

        [TestCase("{ `gridSize`: [3, `a`, 3] }", "'gridSize' eixo y deve ser um número inteiro")]
        [TestCase("{ `gridSize`: [3, 3] }", "'gridSize' deve ter exatamente 3 valores [x, y, z], mas tem 2")]
        [TestCase("{ `gridSize`: [3, 3, 3], `maxCycles`: `dez` }", "'maxCycles', quando presente, deve ser um número inteiro")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: `um`, `type`: `A`, `position`: [0, 0, 0] } ] }", "'id' deve ser um número inteiro")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: 1, `type`: 5, `position`: [0, 0, 0] } ] }", "'type' deve ser um texto não vazio")]
        [TestCase("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: 1, `type`: `A`, `position`: [0, 0.5, 0] } ] }", "'position' eixo y deve ser um número inteiro")]
        public void IncompatibleScenarioType_IsRejected(string scenario, string expected)
        {
            Assert.That(ScenarioErrors(scenario), Does.Contain(expected));
        }

        [TestCase("{ `condition`: 1, `actions`: [ `wait` ] }", "'condition' deve ser um texto.")]
        [TestCase("{ `actions`: `wait` }", "'actions' deve ser uma lista de textos.")]
        [TestCase("{ `actions`: [ 1 ] }", "ação 1: deve ser um texto.")]
        public void IncompatibleBehaviorType_IsRejected(string rule, string expected)
        {
            Assert.That(RuleErrors(rule), Does.Contain(expected));
        }

        [Test]
        public void TwoParticlesOnTheSameStartingCell_AreRejected()
        {
            string errors = ScenarioErrors("{ `gridSize`: [3, 3, 3], `particles`: [ { `id`: 1, `type`: `A`, `position`: [0, 0, 0] }, { `id`: 2, `type`: `A`, `position`: [0, 0, 0] } ] }");

            Assert.That(errors, Does.Contain("Cenário: partícula id 2: mais de uma partícula começa na posição [0, 0, 0]."));
        }

        [Test]
        public void ParticleTypeMissingFromTheBehavior_IsRejected()
        {
            string errors = TestSimulation.ScenarioErrors(Json.Scenario(3, (1, "B", 0, 0, 0)), Behavior);

            Assert.That(errors, Does.Contain("Cenário: tipo de partícula 'B' (partícula id 1) não existe no arquivo de comportamento."));
        }

        [Test]
        public void UnknownFunction_IsRejected()
        {
            Assert.That(RuleErrors("{ `condition`: `estaLivre('X+')`, `actions`: [ `wait` ] }"), Does.Contain("função desconhecida 'estaLivre'"));
            Assert.That(RuleErrors("{ `actions`: [ `andar(X+)` ] }"), Does.Contain("função desconhecida 'andar'"));
        }

        [TestCase("{ `actions`: [ `move(norte)` ] }", "argumento 1 de 'move' deve ser uma direção válida (X+, X-, Y+, Y-, Z+, Z-), mas é 'norte'")]
        [TestCase("{ `condition`: `hasNeighbor('W+')`, `actions`: [ `wait` ] }", "argumento 1 de 'hasNeighbor' deve ser uma direção válida")]
        public void InvalidDirection_IsRejected(string rule, string expected)
        {
            Assert.That(RuleErrors(rule), Does.Contain(expected));
        }

        [Test]
        public void InvalidDirectionInAnEventKey_IsRejected()
        {
            string errors = TestSimulation.BehaviorErrors(Scenario, Json.Of("{ `A`: { `onTouch(W+)`: [ { `actions`: [ `wait` ] } ] } }"));

            Assert.That(errors, Does.Contain("direção inválida 'W+'"));
        }

        // Each condition is true or false depending only on Not > And > Or precedence.
        [TestCase("Not false And false", false)]
        [TestCase("Not true Or true", true)]
        [TestCase("true Or false And false", true)]
        [TestCase("false And false Or true", true)]
        [TestCase("Not false And Not false Or false", true)]
        public void Precedence_IsNotThenAndThenOr(string condition, bool expected)
        {
            TestSimulation sim = TestSimulation.Load(Scenario,
                Json.Of("{ `A`: { `onStep`: [ { `condition`: `" + condition + "`, `actions`: [ `setState('vale', true)` ] } ] } }"));

            sim.Step();

            Assert.That(sim.State(1, "vale"), expected ? Is.EqualTo(true) : Is.Null);
        }

        [TestCase(">", "'alto'")]
        [TestCase("<", "alto")]
        [TestCase(">=", "true")]
        [TestCase("<=", "false")]
        public void MagnitudeOperator_WithATextOrBooleanLiteral_IsRejected(string op, string literal)
        {
            string errors = RuleErrors("{ `condition`: `checkState('x', '" + op + "', " + literal + ")`, `actions`: [ `wait` ] }");

            Assert.That(errors, Does.Contain($"o operador '{op}' em 'checkState' só compara valores numéricos"));
        }

        [Test]
        public void MagnitudeOperator_WithANumber_IsAccepted()
        {
            TestSimulation.Load(Scenario, Json.Of("{ `A`: { `onStep`: [ { `condition`: `checkState('x', '>', 5)`, `actions`: [ `wait` ] } ] } }"));
        }
    }
}
