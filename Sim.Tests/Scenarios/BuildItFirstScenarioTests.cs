using NUnit.Framework;
using LordsLegacy.Sim;

namespace LordsLegacy.Sim.Tests.Scenarios
{
    [TestFixture]
    public class BuildItFirstScenarioTests
    {
        [Test]
        public void Test_BuildItFirstScenario()
        {
            // Setup
            var game = new Game();
            game.StartLayout = new StartLayout
            {
                UnassignedFamilies = 6,
                Oxen = 2,
                Logs = 20
            };
            game.MaxBuildersPerSite = 4;
            game.PaceTuning = new PaceTuning
            {
                WoodcuttingSpeed = 1.04,
                WalkingSpeed = 1.74
            };

            // Place hut and storage
            var hut = new Hut
            {
                Position = new Vector2(0, 0),
                AccessPoint = new Vector2(0, 60)
            };
            game.Places.Add(hut);

            var storage = new Storage
            {
                Position = new Vector2(0, 0)
            };
            game.Places.Add(storage);

            // Advance time to 1 March 06:00
            game.CurrentTime = new DateTime(2023, 3, 1, 6, 0, 0);

            // Place a second hut with < 8 available
            var secondHut = new Hut
            {
                Position = new Vector2(0, 0)
            };
            Assert.Throws<Exception>(() => game.Places.Add(secondHut));

            // Advance time to sunset on 2 March
            game.CurrentTime = new DateTime(2023, 3, 2, 18, 0, 0);

            // Check completion tick
            var completionTick = game.CompletionTick;
            Assert.Greater(completionTick, 0);

            // Check stockpile
            Assert.AreEqual(8, game.Stockpile);

            // Check ledger audit
            game.AuditLedger();

            // Check AssignFamily before and after hut is built
            Assert.Throws<Exception>(() => game.AssignFamily(new Family()));
            game.BuildHut(hut);
            game.AssignFamily(new Family());

            // Check logs felled
            Assert.Greater(game.LogsFelled, 0);
        }
    }
}