using System;

namespace MarbleSync.Simulation
{
    /// <summary>A straight stretch of track. Acceleration comes from its slope.</summary>
    [Serializable]
    public struct SegmentDef
    {
        public float Length;
        public float Acceleration;

        public SegmentDef(float length, float acceleration)
        {
            Length = length;
            Acceleration = acceleration;
        }
    }

    /// <summary>
    /// A point element at a distance along the track.
    /// Id: sensor/button number shown to the player. StartsActive: door starts closed / flame starts lit.
    /// </summary>
    [Serializable]
    public struct ElementDef
    {
        public ElementKind Kind;
        public float Distance;
        public LogicColor Color;
        public int Id;
        public bool StartsActive;

        public ElementDef(ElementKind kind, float distance, LogicColor color = LogicColor.Blue, int id = 0, bool startsActive = true)
        {
            Kind = kind;
            Distance = distance;
            Color = color;
            Id = id;
            StartsActive = startsActive;
        }

        public bool IsPuddle => Kind == ElementKind.Honey || Kind == ElementKind.Oil || Kind == ElementKind.Water;
    }

    [Serializable]
    public class TrackDef
    {
        public SegmentDef[] Segments = Array.Empty<SegmentDef>();
        public ElementDef[] Elements = Array.Empty<ElementDef>();

        public float TotalLength
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Segments.Length; i++)
                {
                    total += Segments[i].Length;
                }
                return total;
            }
        }
    }

    /// <summary>Every tunable number that shapes the timing. Kept in one place so design can tweak it.</summary>
    [Serializable]
    public class SimTuning
    {
        public float StepsPerSecond = 60f;
        public float BaseMaxSpeed = 5f;
        public float Deceleration = 6f;
        public float CurveSpeedFactor = 0.3f;
        public float HoneyMaxSpeedFactor = 0.5f;
        public float HoneyAccelFactor = 0.6f;
        public float OilMaxSpeedFactor = 1.5f;
        public float OilAccelFactor = 1.5f;
        public float BurningMaxSpeedFactor = 2.2f;
        public float BurningAccelFactor = 2.5f;
        public float BurnFuseSeconds = 1f;
        public float SeesawLength = 3f;
        public float SeesawHalfSeconds = 0.5f;
        public float SeesawFinishSeconds = 0.75f; // GDD says 0.25 s; lengthened in playtest so the final rush is not a sudden jump
        public float TimerStepSeconds = 0.25f;

        public float Dt => 1f / StepsPerSecond;

        public int SecondsToSteps(float seconds)
        {
            return (int)Math.Round(seconds * StepsPerSecond, MidpointRounding.AwayFromZero);
        }
    }

    [Serializable]
    public class LevelSimDef
    {
        public TrackDef[] Tracks = Array.Empty<TrackDef>();
        public SimTuning Tuning = new SimTuning();

        public int MarbleCount => Tracks.Length;

        public void Validate()
        {
            if (Tracks.Length == 0)
            {
                throw new InvalidOperationException("Level has no tracks.");
            }

            for (int t = 0; t < Tracks.Length; t++)
            {
                TrackDef track = Tracks[t] ?? throw new InvalidOperationException($"Track {t} is null.");
                if (track.Segments.Length == 0)
                {
                    throw new InvalidOperationException($"Track {t} has no segments.");
                }

                float length = track.TotalLength;
                float previous = float.NegativeInfinity;
                for (int e = 0; e < track.Elements.Length; e++)
                {
                    float d = track.Elements[e].Distance;
                    if (d < previous)
                    {
                        throw new InvalidOperationException($"Track {t} elements must be sorted by distance (element {e}).");
                    }
                    if (d <= 0f || d >= length)
                    {
                        throw new InvalidOperationException($"Track {t} element {e} at {d} is outside the track (length {length}).");
                    }
                    previous = d;
                }
            }
        }
    }
}
