using System.Collections.Generic;
using MarbleSync.Simulation;
using NUnit.Framework;

namespace MarbleSync.Tests
{
    public class MarbleSimulationTests
    {
        private const int MaxSteps = 60 * 60;

        private static TrackDef Track(float length, params ElementDef[] elements)
        {
            return new TrackDef
            {
                Segments = new[] { new SegmentDef(length, 4f) },
                Elements = elements
            };
        }

        private static LevelSimDef Level(params TrackDef[] tracks)
        {
            return new LevelSimDef { Tracks = tracks, Tuning = new SimTuning() };
        }

        private static MarbleSimulation RunWithReleases(LevelSimDef level, params int[] releaseSteps)
        {
            var sim = new MarbleSimulation(level);
            sim.StartAttempt();
            for (int i = 0; i < releaseSteps.Length; i++)
            {
                sim.QueueInput(new InputRecord(releaseSteps[i], InputType.Release, i));
            }
            sim.RunToEnd(MaxSteps);
            Assert.IsTrue(sim.Finished, "Attempt should finish.");
            return sim;
        }

        private static float SoloEntryTime(TrackDef track)
        {
            MarbleSimulation sim = RunWithReleases(Level(track), 1);
            return sim.Outcome.Marbles[0].EntryTime;
        }

        [Test]
        public void SameInputs_ProduceIdenticalTimings()
        {
            LevelSimDef level = Level(
                Track(10f, new ElementDef(ElementKind.Curve, 3f), new ElementDef(ElementKind.Honey, 6f)),
                Track(12f, new ElementDef(ElementKind.Oil, 2f)));

            AttemptOutcome a = RunWithReleases(level, 1, 20).Outcome;
            AttemptOutcome b = RunWithReleases(level, 1, 20).Outcome;

            Assert.AreEqual(a.Marbles[0].EntryTime, b.Marbles[0].EntryTime);
            Assert.AreEqual(a.Marbles[1].EntryTime, b.Marbles[1].EntryTime);
            Assert.AreEqual(a.FinishStep, b.FinishStep);
        }

        [Test]
        public void TwoMarblesInsideWindow_Succeed()
        {
            LevelSimDef level = Level(Track(10f), Track(10f));
            AttemptOutcome outcome = RunWithReleases(level, 1, 10).Outcome;

            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(9f / 60f, outcome.Spread, 0.02f);
        }

        [Test]
        public void MarbleLaterThanWindow_IsBlockedWithLateness()
        {
            LevelSimDef level = Level(Track(10f), Track(10f));
            AttemptOutcome outcome = RunWithReleases(level, 1, 1 + 42).Outcome; // 0.7 s later

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(FailureReason.BlockedBySeesaw, outcome.Marbles[1].Failure);
            Assert.AreEqual(0.2f, outcome.Marbles[1].Lateness, 0.02f);
        }

        [Test]
        public void HoneyEarly_CostsMoreThanHoneyLate()
        {
            float early = SoloEntryTime(Track(20f, new ElementDef(ElementKind.Honey, 2f)));
            float late = SoloEntryTime(Track(20f, new ElementDef(ElementKind.Honey, 18f)));
            float none = SoloEntryTime(Track(20f));

            Assert.Greater(early, late);
            Assert.Greater(late, none);
        }

        [Test]
        public void Curve_SlowsTheMarble()
        {
            float curve = SoloEntryTime(Track(10f, new ElementDef(ElementKind.Curve, 5f)));
            float straight = SoloEntryTime(Track(10f));
            Assert.Greater(curve, straight);
        }

        [Test]
        public void Flame_MeltsUncoatedMarble()
        {
            MarbleSimulation sim = RunWithReleases(Level(Track(10f, new ElementDef(ElementKind.Flame, 4f))), 1);
            Assert.AreEqual(FailureReason.Melted, sim.Outcome.Marbles[0].Failure);
        }

        [Test]
        public void Water_ProtectsOnlyOnce()
        {
            LevelSimDef once = Level(Track(12f,
                new ElementDef(ElementKind.Water, 2f),
                new ElementDef(ElementKind.Flame, 5f)));
            Assert.IsTrue(RunWithReleases(once, 1).Outcome.Success);

            LevelSimDef twice = Level(Track(12f,
                new ElementDef(ElementKind.Water, 2f),
                new ElementDef(ElementKind.Water, 3f),
                new ElementDef(ElementKind.Flame, 5f),
                new ElementDef(ElementKind.Flame, 8f)));
            Assert.AreEqual(FailureReason.Melted, RunWithReleases(twice, 1).Outcome.Marbles[0].Failure);

            LevelSimDef alternating = Level(Track(12f,
                new ElementDef(ElementKind.Water, 2f),
                new ElementDef(ElementKind.Flame, 4f),
                new ElementDef(ElementKind.Water, 6f),
                new ElementDef(ElementKind.Flame, 8f)));
            Assert.IsTrue(RunWithReleases(alternating, 1).Outcome.Success);
        }

        [Test]
        public void OilFlame_BurnsOutWithoutWater_ButWaterSavesIt()
        {
            LevelSimDef noWater = Level(Track(30f,
                new ElementDef(ElementKind.Oil, 1f),
                new ElementDef(ElementKind.Flame, 3f)));
            Assert.AreEqual(FailureReason.Melted, RunWithReleases(noWater, 1).Outcome.Marbles[0].Failure);

            LevelSimDef withWater = Level(Track(30f,
                new ElementDef(ElementKind.Oil, 1f),
                new ElementDef(ElementKind.Flame, 3f),
                new ElementDef(ElementKind.Water, 6f)));
            Assert.IsTrue(RunWithReleases(withWater, 1).Outcome.Success);
        }

        [Test]
        public void BurningMarbleReachingSeesaw_DoesNotFail()
        {
            LevelSimDef level = Level(Track(6f,
                new ElementDef(ElementKind.Oil, 1f),
                new ElementDef(ElementKind.Flame, 4f)));
            MarbleSimulation sim = RunWithReleases(level, 1);
            Assert.IsTrue(sim.Outcome.Success);
            Assert.IsTrue(sim.GetMarble(0).MeltedAtFinish);
        }

        [Test]
        public void HoneyFlame_CaramelizesUntilClicked()
        {
            LevelSimDef level = Level(Track(10f,
                new ElementDef(ElementKind.Honey, 1f),
                new ElementDef(ElementKind.Flame, 3f)));
            var sim = new MarbleSimulation(level);
            sim.StartAttempt();
            sim.QueueInput(InputType.Release, 0);
            for (int i = 0; i < 300; i++)
            {
                sim.Step();
            }
            Assert.AreEqual(MarbleStatus.Caramelized, sim.GetMarble(0).Status);
            Assert.IsFalse(sim.Finished);

            sim.QueueInput(InputType.Resume, 0);
            sim.RunToEnd(MaxSteps);
            Assert.IsTrue(sim.Outcome.Success);
            Assert.AreEqual(Coating.None, sim.GetMarble(0).Coating);
        }

        [Test]
        public void Button_TogglesDoorsOfSameColour()
        {
            // Track 0 has a closed blue door; track 1 presses the blue button and opens it.
            LevelSimDef level = Level(
                Track(10f, new ElementDef(ElementKind.Door, 2f, LogicColor.Blue, 0, true)),
                Track(10f, new ElementDef(ElementKind.Button, 1f, LogicColor.Blue, 1)));
            var sim = new MarbleSimulation(level);
            sim.StartAttempt();
            sim.QueueInput(InputType.Release, 0);
            for (int i = 0; i < 120; i++)
            {
                sim.Step();
            }
            Assert.AreEqual(MarbleStatus.StoppedAtDoor, sim.GetMarble(0).Status);

            sim.QueueInput(InputType.Release, 1);
            sim.RunToEnd(MaxSteps);
            Assert.AreEqual(MarbleStatus.Arrived, sim.GetMarble(0).Status);
        }

        [Test]
        public void SensorTimer_ReleasesTargetAfterDelay()
        {
            LevelSimDef level = Level(
                Track(10f, new ElementDef(ElementKind.Sensor, 5f, LogicColor.Blue, 2)),
                Track(10f));
            var sim = new MarbleSimulation(level);
            sim.SetTimers(new List<TimerRule>
            {
                new TimerRule(0, TimerTrigger.AttemptStart, 0f),
                new TimerRule(1, TimerTrigger.SensorPassed, 0.5f, 0, 2)
            });
            sim.StartAttempt();
            sim.RunToEnd(MaxSteps);

            Assert.AreEqual(FailureReason.None, sim.Outcome.Marbles[0].Failure);
            Assert.AreNotEqual(FailureReason.NeverArrived, sim.Outcome.Marbles[1].Failure);
            Assert.Greater(sim.Outcome.Marbles[1].EntryTime, sim.Outcome.Marbles[0].EntryTime);
        }

        [Test]
        public void Sponge_RemovesPuddle()
        {
            LevelSimDef level = Level(Track(20f, new ElementDef(ElementKind.Honey, 2f)));
            float withHoney = SoloEntryTime(level.Tracks[0]);

            var sim = new MarbleSimulation(level);
            sim.RemovePuddle(0, 0);
            sim.StartAttempt();
            sim.QueueInput(InputType.Release, 0);
            sim.RunToEnd(MaxSteps);

            Assert.Less(sim.Outcome.Marbles[0].EntryTime, withHoney);
        }

        [Test]
        public void Prediction_MatchesActualRun()
        {
            LevelSimDef level = Level(
                Track(10f, new ElementDef(ElementKind.Curve, 4f)),
                Track(14f, new ElementDef(ElementKind.Oil, 3f)));
            var sim = new MarbleSimulation(level);
            sim.StartAttempt();
            sim.QueueInput(InputType.Release, 0);
            sim.Step();
            sim.QueueInput(InputType.Release, 1);
            sim.Step();

            MarbleSimulation prediction = sim.Clone();
            prediction.RunToEnd(MaxSteps);
            sim.RunToEnd(MaxSteps);

            Assert.AreEqual(prediction.Outcome.Spread, sim.Outcome.Spread);
            Assert.AreEqual(prediction.Outcome.FinishStep, sim.Outcome.FinishStep);
        }
    }
}
