using System;
using MarbleSync.Simulation;

namespace MarbleSync.EditorTools
{
    /// <summary>
    /// Brute-force design aid: searches manual release steps (and resume delays for caramelized marbles)
    /// that solve a level. Used to prove every generated level is solvable and to write timer hints.
    /// </summary>
    public static class LevelSolver
    {
        private const int MaxSteps = 60 * 90;

        public sealed class Solution
        {
            public bool Solved;
            public int[] ReleaseSteps = Array.Empty<int>();
            public int[] ResumeDelays = Array.Empty<int>();
            public float Spread;
            public float Cost;
        }

        public static Solution Solve(LevelSimDef level, int iterations = 4000, int seed = 7)
        {
            int n = level.MarbleCount;
            var release = new int[n];
            var resume = new int[n];

            // Start from "everybody released so they would enter together if lanes were independent".
            var solo = new float[n];
            float slowest = 0f;
            for (int i = 0; i < n; i++)
            {
                solo[i] = SoloTravel(level, i);
                slowest = Math.Max(slowest, solo[i]);
            }
            for (int i = 0; i < n; i++)
            {
                release[i] = 1 + (int)Math.Round((slowest - solo[i]) * level.Tuning.StepsPerSecond);
                resume[i] = 20;
            }

            var best = new Solution { ReleaseSteps = (int[])release.Clone(), ResumeDelays = (int[])resume.Clone() };
            best.Cost = Evaluate(level, release, resume, out bool ok, out float spread);
            best.Solved = ok;
            best.Spread = spread;

            var rng = new Random(seed);
            for (int it = 0; it < iterations && !(best.Solved && best.Spread < 0.1f); it++)
            {
                int[] candRelease = (int[])best.ReleaseSteps.Clone();
                int[] candResume = (int[])best.ResumeDelays.Clone();
                int m = rng.Next(n);
                int scale = it < iterations / 2 ? 60 : 12;
                candRelease[m] = Math.Max(1, candRelease[m] + rng.Next(-scale, scale + 1));
                if (rng.NextDouble() < 0.3)
                {
                    candResume[m] = Math.Max(1, candResume[m] + rng.Next(-scale, scale + 1));
                }

                float cost = Evaluate(level, candRelease, candResume, out ok, out spread);
                if (cost < best.Cost)
                {
                    best.ReleaseSteps = candRelease;
                    best.ResumeDelays = candResume;
                    best.Cost = cost;
                    best.Solved = ok;
                    best.Spread = spread;
                }
            }

            // Normalise so the first release happens on step 1.
            int min = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                min = Math.Min(min, best.ReleaseSteps[i]);
            }
            for (int i = 0; i < n; i++)
            {
                best.ReleaseSteps[i] -= min - 1;
            }
            return best;
        }

        public static float SoloTravel(LevelSimDef level, int marble)
        {
            var sim = new MarbleSimulation(level) { RecordEvents = true };
            sim.StartAttempt();
            sim.QueueInput(new InputRecord(1, InputType.Release, marble));
            int guard = 0;
            while (!sim.GetMarble(marble).Entered && guard++ < MaxSteps)
            {
                MarbleStatus s = sim.GetMarble(marble).Status;
                if (s == MarbleStatus.Caramelized)
                {
                    sim.QueueInput(InputType.Resume, marble);
                }
                else if (s == MarbleStatus.Melted || s == MarbleStatus.StoppedAtDoor)
                {
                    return 0f; // depends on other lanes; let the search handle it
                }
                sim.Step();
            }
            return sim.GetMarble(marble).EntryTime;
        }

        private static float Evaluate(LevelSimDef level, int[] release, int[] resume, out bool ok, out float spread)
        {
            var sim = new MarbleSimulation(level) { RecordEvents = false };
            sim.StartAttempt();
            for (int i = 0; i < release.Length; i++)
            {
                sim.QueueInput(new InputRecord(release[i], InputType.Release, i));
            }

            var resumeQueued = new bool[release.Length];
            int guard = 0;
            while (!sim.Finished && guard++ < MaxSteps)
            {
                for (int i = 0; i < release.Length; i++)
                {
                    if (!resumeQueued[i] && sim.CanResume(i))
                    {
                        resumeQueued[i] = true;
                        sim.QueueInput(new InputRecord(sim.CurrentStep + resume[i], InputType.Resume, i));
                    }
                }
                sim.Step();
            }

            if (!sim.Finished)
            {
                ok = false;
                spread = float.MaxValue;
                return 1000f;
            }

            AttemptOutcome o = sim.Outcome;
            ok = o.Success;
            spread = o.Spread;
            if (o.Success)
            {
                return o.Spread;
            }

            float penalty = 10f;
            for (int i = 0; i < o.Marbles.Length; i++)
            {
                MarbleResult r = o.Marbles[i];
                switch (r.Failure)
                {
                    case FailureReason.BlockedBySeesaw: penalty += 1f + r.Lateness; break;
                    case FailureReason.Melted: penalty += 20f; break;
                    case FailureReason.NeverArrived: penalty += 30f; break;
                }
            }
            return penalty;
        }
    }
}
