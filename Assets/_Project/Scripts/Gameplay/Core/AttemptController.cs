using System;
using System.Collections.Generic;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    public enum AttemptPhase
    {
        Setup,
        Running,
        Finished,
        Replay
    }

    /// <summary>
    /// Owns the deterministic simulation for the current level and translates player intent into inputs.
    /// Views and juice listen to its events; it only drives the views it needs to render the simulation.
    /// </summary>
    public sealed class AttemptController : MonoBehaviour
    {
        private const int PredictionBudgetSteps = 60 * 120;
        private const int HintUnlockFailures = 5;
        private const int MaxHistory = 12;

        [SerializeField] private LevelLibrary library;
        [SerializeField] private int startLevelIndex;
        [SerializeField] private TrackView trackView;
        [SerializeField] private MarbleSetView marbles;
        [SerializeField] private MarbleSetView ghostMarbles;
        [SerializeField] private float[] replaySpeeds = { 0.25f, 0.5f, 1f, 2f };

        private readonly List<AttemptRecord> _history = new List<AttemptRecord>();
        private readonly List<PuddleRef> _removedPuddles = new List<PuddleRef>();
        private readonly List<InputRecord> _plannedInputs = new List<InputRecord>();

        private int _levelIndex;
        private MarbleSimulation _sim;
        private MarbleSimulation _ghostSim;
        private AttemptRecord _ghostRecord;
        private AttemptRecord _record;
        private AttemptRecord _replayRecord;
        private TimerProgram _timers;
        private int _spongesLeft;
        private int _failures;
        private int _attemptCounter;
        private int _selectedTimerMarble = -1;
        private float _accumulator;
        private int _replaySpeedIndex = 2;
        private bool _predictionDirty;
        private bool _wasAllReleased;

        public event Action LevelLoaded;
        public event Action StateChanged;
        public event Action<SimEvent> SimEventRaised;
        public event Action<AttemptRecord> AttemptFinished;
        public event Action<AttemptOutcome> PredictionUpdated;

        public LevelDefinition Level { get; private set; }
        public int LevelIndex => _levelIndex;
        public int LevelCount => library.Count;
        public AttemptPhase Phase { get; private set; }
        public MarbleSimulation Sim => _sim;
        public TimerProgram Timers => _timers;
        public int SpongesLeft => _spongesLeft;
        public int SelectedTimerMarble => _selectedTimerMarble;
        public bool HintUnlocked => Level.TimersUnlocked && _failures >= HintUnlockFailures;
        public IReadOnlyList<AttemptRecord> History => _history;
        public AttemptRecord GhostRecord => _ghostRecord;
        public AttemptRecord ReplayRecord => _replayRecord;
        public AttemptRecord LastRecord => _history.Count > 0 ? _history[_history.Count - 1] : null;
        public bool HasPlannedInputs => _plannedInputs.Count > 0;
        public float ReplaySpeed => replaySpeeds[_replaySpeedIndex];
        public float Window => Level.Tuning.SeesawHalfSeconds;

        /// <summary>The restart button appears only after the first marble is released.</summary>
        public bool CanRestart => Phase == AttemptPhase.Finished
                                  || Phase == AttemptPhase.Replay
                                  || (Phase == AttemptPhase.Running && AnyReleased());

        public bool IsReplay => Phase == AttemptPhase.Replay;

        /// <summary>Holding is only required mid-attempt while it can still succeed; otherwise restart is instant.</summary>
        public bool RestartNeedsHold => Phase == AttemptPhase.Running && !AlreadyLost();

        private bool AlreadyLost()
        {
            for (int i = 0; i < _sim.MarbleCount; i++)
            {
                MarbleStatus s = _sim.GetMarble(i).Status;
                if (s == MarbleStatus.Melted || s == MarbleStatus.Blocked)
                {
                    return true;
                }
            }
            return false;
        }

        private void Start()
        {
            LoadLevel(startLevelIndex);
        }

        // ---------------------------------------------------------------- level flow

        public void LoadLevel(int index)
        {
            _levelIndex = (index % library.Count + library.Count) % library.Count;
            Level = library.Get(_levelIndex);
            _history.Clear();
            _failures = 0;
            _attemptCounter = 0;
            _ghostRecord = null;
            _plannedInputs.Clear();
            _timers = new TimerProgram(Level.MarbleCount, Level.Tuning.TimerStepSeconds);

            trackView.Build(Level);
            marbles.Build(Level);
            ghostMarbles.Build(Level);
            ResetAttempt();
            LevelLoaded?.Invoke();
        }

        public void NextLevel() => LoadLevel(_levelIndex + 1);

        public void PreviousLevel() => LoadLevel(_levelIndex - 1);

        /// <summary>Back to setup. Removed puddles and sponges come back; programmed timers stay.</summary>
        public void Restart()
        {
            ResetAttempt();
        }

        private void ResetAttempt()
        {
            _sim = new MarbleSimulation(Level.SimDef);
            _ghostSim = null;
            _record = null;
            _replayRecord = null;
            _removedPuddles.Clear();
            _spongesLeft = Level.SpongeCount;
            _selectedTimerMarble = -1;
            _accumulator = 0f;
            _predictionDirty = false;
            _wasAllReleased = false;
            Phase = AttemptPhase.Setup;

            marbles.Sync(_sim);
            marbles.SetSelected(-1);
            ghostMarbles.SetVisible(false);
            trackView.Show(_sim);
            StateChanged?.Invoke();
        }

        public void BeginAttempt()
        {
            if (Phase != AttemptPhase.Setup)
            {
                return;
            }

            _attemptCounter++;
            _record = new AttemptRecord
            {
                LevelId = Level.LevelId,
                LevelVersion = Level.Version,
                AttemptNumber = _attemptCounter,
                Timers = _timers.ToList(),
                RemovedPuddles = new List<PuddleRef>(_removedPuddles)
            };

            _sim.SetTimers(_record.Timers);
            _sim.StartAttempt();
            DispatchEvents(_sim);

            for (int i = 0; i < _plannedInputs.Count; i++)
            {
                _sim.QueueInput(_plannedInputs[i]);
                _record.Inputs.Add(_plannedInputs[i]);
            }

            if (_ghostRecord != null)
            {
                _ghostSim = CreateReplaySim(_ghostRecord);
                ghostMarbles.Sync(_ghostSim);
                ghostMarbles.SetVisible(true);
            }

            _selectedTimerMarble = -1;
            marbles.SetSelected(-1);
            Phase = AttemptPhase.Running;
            StateChanged?.Invoke();
        }

        // ---------------------------------------------------------------- player input

        public void OnMarbleClicked(int marble)
        {
            switch (Phase)
            {
                case AttemptPhase.Setup:
                    if (Level.TimersUnlocked)
                    {
                        _selectedTimerMarble = _selectedTimerMarble == marble ? -1 : marble;
                        marbles.SetSelected(_selectedTimerMarble);
                        StateChanged?.Invoke();
                        return;
                    }
                    BeginAttempt();
                    QueuePlayerInput(InputType.Release, marble);
                    return;
                case AttemptPhase.Running:
                    if (_sim.CanRelease(marble))
                    {
                        QueuePlayerInput(InputType.Release, marble);
                    }
                    else if (_sim.CanResume(marble))
                    {
                        QueuePlayerInput(InputType.Resume, marble);
                    }
                    return;
                case AttemptPhase.Finished:
                case AttemptPhase.Replay:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnElementClicked(int lane, int element)
        {
            if (Phase != AttemptPhase.Setup)
            {
                return;
            }

            ElementDef def = Level.SimDef.Tracks[lane].Elements[element];

            if (_selectedTimerMarble >= 0 && (def.Kind == ElementKind.Sensor || def.Kind == ElementKind.Button))
            {
                TimerTrigger trigger = def.Kind == ElementKind.Sensor ? TimerTrigger.SensorPassed : TimerTrigger.ButtonPressed;
                _timers.AttachToElement(_selectedTimerMarble, trigger, lane, def.Id);
                StateChanged?.Invoke();
                return;
            }

            if (def.IsPuddle && _spongesLeft > 0 && !_sim.IsElementRemoved(lane, element))
            {
                _sim.RemovePuddle(lane, element);
                _removedPuddles.Add(new PuddleRef(lane, element));
                _spongesLeft--;
                trackView.Show(_sim);
                StateChanged?.Invoke();
            }
        }

        public void AdjustSelectedTimer(int direction)
        {
            if (Phase != AttemptPhase.Setup || _selectedTimerMarble < 0)
            {
                return;
            }
            _timers.AdjustDelay(_selectedTimerMarble, direction);
            StateChanged?.Invoke();
        }

        public void CycleSelectedTrigger()
        {
            if (Phase != AttemptPhase.Setup || _selectedTimerMarble < 0)
            {
                return;
            }
            _timers.CycleTrigger(_selectedTimerMarble);
            StateChanged?.Invoke();
        }

        public void ClearSelectedTimer()
        {
            if (Phase != AttemptPhase.Setup || _selectedTimerMarble < 0)
            {
                return;
            }
            _timers.Clear(_selectedTimerMarble);
            StateChanged?.Invoke();
        }

        private void QueuePlayerInput(InputType type, int marble)
        {
            _sim.QueueInput(type, marble);
            _record.Inputs.Add(new InputRecord(_sim.CurrentStep + 1, type, marble));
            if (type == InputType.Resume)
            {
                _predictionDirty = true;
            }
            StateChanged?.Invoke();
        }

        // ---------------------------------------------------------------- replay history

        public void WatchReplay(AttemptRecord record)
        {
            ResetAttempt();
            _replayRecord = record;
            _sim = CreateReplaySim(record);
            _sim.RecordEvents = true;
            marbles.Sync(_sim);
            trackView.Show(_sim);
            Phase = AttemptPhase.Replay;
            StateChanged?.Invoke();
        }

        public void CycleReplaySpeed()
        {
            _replaySpeedIndex = (_replaySpeedIndex + 1) % replaySpeeds.Length;
            StateChanged?.Invoke();
        }

        public void ToggleGhost(AttemptRecord record)
        {
            _ghostRecord = _ghostRecord == record ? null : record;
            StateChanged?.Invoke();
        }

        /// <summary>Start the next attempt from an old one: same timers, same sponge use, same releases.</summary>
        public void LoadAsBase(AttemptRecord record)
        {
            ResetAttempt();
            _timers.Load(record.Timers);
            for (int i = 0; i < record.RemovedPuddles.Count; i++)
            {
                PuddleRef p = record.RemovedPuddles[i];
                if (_spongesLeft > 0)
                {
                    _sim.RemovePuddle(p.Track, p.Element);
                    _removedPuddles.Add(p);
                    _spongesLeft--;
                }
            }
            _plannedInputs.Clear();
            _plannedInputs.AddRange(record.Inputs);
            trackView.Show(_sim);
            StateChanged?.Invoke();
        }

        public void ClearPlannedInputs()
        {
            _plannedInputs.Clear();
            StateChanged?.Invoke();
        }

        private MarbleSimulation CreateReplaySim(AttemptRecord record)
        {
            var sim = new MarbleSimulation(Level.SimDef) { RecordEvents = false };
            for (int i = 0; i < record.RemovedPuddles.Count; i++)
            {
                sim.RemovePuddle(record.RemovedPuddles[i].Track, record.RemovedPuddles[i].Element);
            }
            sim.SetTimers(record.Timers);
            sim.StartAttempt();
            for (int i = 0; i < record.Inputs.Count; i++)
            {
                sim.QueueInput(record.Inputs[i]);
            }
            return sim;
        }

        // ---------------------------------------------------------------- stepping

        private void Update()
        {
            bool stepping = Phase == AttemptPhase.Running || (Phase == AttemptPhase.Replay && !_sim.Finished);
            if (stepping)
            {
                float speed = Phase == AttemptPhase.Replay ? ReplaySpeed : 1f;
                _accumulator += Time.deltaTime * speed;
                float dt = _sim.Dt;
                int guard = 0;
                while (_accumulator >= dt && guard < 16)
                {
                    _accumulator -= dt;
                    guard++;
                    StepOnce();
                    if (Phase != AttemptPhase.Running && Phase != AttemptPhase.Replay)
                    {
                        break;
                    }
                }
            }

            float alpha = stepping ? Mathf.Clamp01(_accumulator / _sim.Dt) : 1f;
            marbles.Render(_sim, alpha);
            if (_ghostSim != null)
            {
                ghostMarbles.Render(_ghostSim, alpha);
            }
            trackView.Show(_sim);
        }

        private void StepOnce()
        {
            _sim.Step();
            marbles.Capture(_sim);
            if (_ghostSim != null)
            {
                _ghostSim.Step();
                ghostMarbles.Capture(_ghostSim);
            }

            DispatchEvents(_sim);

            if (Phase == AttemptPhase.Running)
            {
                bool allReleased = _sim.AllReleased;
                if (allReleased && !_wasAllReleased)
                {
                    _predictionDirty = true;
                }
                _wasAllReleased = allReleased;

                if (_predictionDirty && allReleased && !_sim.Finished)
                {
                    _predictionDirty = false;
                    MarbleSimulation prediction = _sim.Clone();
                    prediction.RunToEnd(PredictionBudgetSteps);
                    if (prediction.Finished)
                    {
                        PredictionUpdated?.Invoke(prediction.Outcome);
                    }
                }

                if (_sim.Finished)
                {
                    FinishAttempt();
                }
            }
            else if (Phase == AttemptPhase.Replay && _sim.Finished)
            {
                StateChanged?.Invoke();
            }
        }

        private void FinishAttempt()
        {
            _record.Outcome = _sim.Outcome;
            if (!_record.Outcome.Success)
            {
                _failures++;
            }
            _history.Add(_record);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
            Phase = AttemptPhase.Finished;
            AttemptFinished?.Invoke(_record);
            StateChanged?.Invoke();
        }

        private void DispatchEvents(MarbleSimulation sim)
        {
            IReadOnlyList<SimEvent> events = sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Type == SimEventType.ButtonPressed || e.Type == SimEventType.SensorPassed)
                {
                    trackView.FindElement(e.Marble, e.Element).Pulse();
                }
                SimEventRaised?.Invoke(e);
            }
        }

        private bool AnyReleased()
        {
            for (int i = 0; i < _sim.MarbleCount; i++)
            {
                if (_sim.GetMarble(i).Status != MarbleStatus.Waiting)
                {
                    return true;
                }
            }
            return false;
        }

        public Bounds ActiveMarbleBounds() => marbles.MarbleBounds(_sim);

        public Bounds LevelBounds() => trackView.LevelBounds;
    }
}
