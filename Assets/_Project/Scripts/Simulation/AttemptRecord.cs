using System;
using System.Collections.Generic;

namespace MarbleSync.Simulation
{
    /// <summary>WHEN (trigger) -> WAIT (delay) -> RELEASE (target marble).</summary>
    [Serializable]
    public struct TimerRule
    {
        public int TargetMarble;
        public TimerTrigger Trigger;
        public int SourceMarble;
        public int SourceElementId;
        public float DelaySeconds;

        public TimerRule(int targetMarble, TimerTrigger trigger, float delaySeconds, int sourceMarble = 0, int sourceElementId = 0)
        {
            TargetMarble = targetMarble;
            Trigger = trigger;
            DelaySeconds = delaySeconds;
            SourceMarble = sourceMarble;
            SourceElementId = sourceElementId;
        }
    }

    public enum InputType
    {
        Release,
        Resume
    }

    /// <summary>A player input, quantised to the simulation step it takes effect on.</summary>
    [Serializable]
    public struct InputRecord
    {
        public int Step;
        public InputType Type;
        public int Marble;

        public InputRecord(int step, InputType type, int marble)
        {
            Step = step;
            Type = type;
            Marble = marble;
        }
    }

    [Serializable]
    public struct PuddleRef
    {
        public int Track;
        public int Element;

        public PuddleRef(int track, int element)
        {
            Track = track;
            Element = element;
        }
    }

    [Serializable]
    public class MarbleResult
    {
        public MarbleStatus FinalStatus;
        public FailureReason Failure;
        public bool Entered;
        public float EntryTime;
        public float Lateness;
    }

    [Serializable]
    public class AttemptOutcome
    {
        public bool Success;
        public float Spread;
        public float CloseTime;
        public int FinishStep;
        public MarbleResult[] Marbles = Array.Empty<MarbleResult>();
    }

    /// <summary>
    /// Everything needed to reproduce an attempt exactly: inputs, timers and sponge use. A few bytes per attempt.
    /// </summary>
    [Serializable]
    public class AttemptRecord
    {
        public string LevelId = string.Empty;
        public int LevelVersion;
        public int AttemptNumber;
        public List<InputRecord> Inputs = new List<InputRecord>();
        public List<TimerRule> Timers = new List<TimerRule>();
        public List<PuddleRef> RemovedPuddles = new List<PuddleRef>();
        public AttemptOutcome Outcome = new AttemptOutcome();
    }
}
