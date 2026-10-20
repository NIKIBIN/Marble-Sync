using System;
using System.Collections.Generic;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    [Serializable]
    public sealed class MarbleLane
    {
        public string MarbleName = "Azul";
        public Color MarbleColor = Color.blue;
        public Vector3[] Waypoints = Array.Empty<Vector3>();
        [Tooltip("Hand-placed elements by distance along the lane. Square curves are added automatically at sharp turns.")]
        public ElementDef[] Elements = Array.Empty<ElementDef>();
    }

    /// <summary>
    /// Authoring data for one level: lanes (geometry + elements), seesaw placement, tools and tuning.
    /// Converted into a pure <see cref="LevelSimDef"/> for the deterministic simulation.
    /// </summary>
    [CreateAssetMenu(menuName = "Marble Sync/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string levelId = "1-1";
        [SerializeField] private string displayName = "Tutorial";
        [SerializeField] private int version = 1;
        [SerializeField, TextArea] private string introText = string.Empty;
        [SerializeField, TextArea] private string timerHint = string.Empty;
        [SerializeField] private int spongeCount;
        [SerializeField] private bool timersUnlocked;
        [SerializeField] private MarbleLane[] lanes = Array.Empty<MarbleLane>();
        [SerializeField] private Vector3 seesawStart = Vector3.zero;
        [SerializeField] private Vector3 seesawDirection = new Vector3(1f, -0.12f, 0f);

        [Header("Slope -> acceleration")]
        [SerializeField] private float baseAcceleration = 3f;
        [SerializeField] private float accelerationPerDrop = 10f;
        [SerializeField] private float curveAngleThreshold = 60f;

        [SerializeField] private SimTuning tuning = new SimTuning();

        [NonSerialized] private LevelSimDef _simDef;
        [NonSerialized] private TrackPath[] _paths;

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public int Version => version;
        public string IntroText => introText;
        public string TimerHint => timerHint;
        public int SpongeCount => spongeCount;
        public bool TimersUnlocked => timersUnlocked;
        public int MarbleCount => lanes.Length;
        public SimTuning Tuning => tuning;
        public Vector3 SeesawStart => seesawStart;
        public Vector3 SeesawEnd => seesawStart + seesawDirection.normalized * tuning.SeesawLength;

        public MarbleLane Lane(int index) => lanes[index];

        public TrackPath Path(int lane)
        {
            EnsureBuilt();
            return _paths[lane];
        }

        public LevelSimDef SimDef
        {
            get
            {
                EnsureBuilt();
                return _simDef;
            }
        }

        /// <summary>Editor-only authoring entry point used by the level generator.</summary>
        public void Author(string id, string title, int levelVersion, string intro, string hint, int sponges,
            bool timers, MarbleLane[] marbleLanes, Vector3 start, Vector3 direction, SimTuning simTuning)
        {
            levelId = id;
            displayName = title;
            version = levelVersion;
            introText = intro;
            timerHint = hint;
            spongeCount = sponges;
            timersUnlocked = timers;
            lanes = marbleLanes;
            seesawStart = start;
            seesawDirection = direction;
            tuning = simTuning;
            _simDef = null;
            _paths = null;
        }

        private void OnValidate()
        {
            _simDef = null;
            _paths = null;
        }

        private void EnsureBuilt()
        {
            if (_simDef != null)
            {
                return;
            }

            if (lanes.Length == 0)
            {
                throw new InvalidOperationException($"Level {levelId} has no lanes.");
            }

            var tracks = new TrackDef[lanes.Length];
            var paths = new TrackPath[lanes.Length];
            for (int l = 0; l < lanes.Length; l++)
            {
                MarbleLane lane = lanes[l];
                if (lane.Waypoints.Length < 2)
                {
                    throw new InvalidOperationException($"Level {levelId} lane {l} needs at least 2 waypoints.");
                }
                if ((lane.Waypoints[lane.Waypoints.Length - 1] - seesawStart).sqrMagnitude > 0.0001f)
                {
                    throw new InvalidOperationException($"Level {levelId} lane {l} must end at the seesaw start.");
                }

                var path = new TrackPath(lane.Waypoints);
                paths[l] = path;

                var segments = new SegmentDef[lane.Waypoints.Length - 1];
                var elements = new List<ElementDef>(lane.Elements);
                for (int s = 0; s < segments.Length; s++)
                {
                    Vector3 a = lane.Waypoints[s];
                    Vector3 b = lane.Waypoints[s + 1];
                    float length = Vector3.Distance(a, b);
                    float drop = Mathf.Max(0f, (a.y - b.y) / Mathf.Max(0.0001f, length));
                    segments[s] = new SegmentDef(length, baseAcceleration + accelerationPerDrop * drop);

                    if (s > 0)
                    {
                        Vector3 inDir = Flat(a - lane.Waypoints[s - 1]);
                        Vector3 outDir = Flat(b - a);
                        if (Vector3.Angle(inDir, outDir) >= curveAngleThreshold)
                        {
                            elements.Add(new ElementDef(ElementKind.Curve, path.DistanceAtPoint(s)));
                        }
                    }
                }

                elements.Sort((x, y) => x.Distance.CompareTo(y.Distance));
                tracks[l] = new TrackDef { Segments = segments, Elements = elements.ToArray() };
            }

            var def = new LevelSimDef { Tracks = tracks, Tuning = tuning };
            def.Validate();
            _simDef = def;
            _paths = paths;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
