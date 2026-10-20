using System;
using System.Collections.Generic;

namespace MarbleSync.Simulation
{
    /// <summary>Live state of one marble. Plain data so the whole simulation can be cloned for prediction.</summary>
    public struct MarbleState
    {
        public MarbleStatus Status;
        public Coating Coating;
        public Condition Condition;
        public float Distance;
        public float Speed;
        public float BurnTimer;
        public int NextElement;
        public float SeesawPos;
        public float SeesawCloseStartPos;
        public bool Entered;
        public float EntryTime;
        public bool MeltedAtFinish;
    }

    /// <summary>
    /// Deterministic fixed-step marble simulation (no physics engine).
    /// The same level + the same inputs always produce the same timings.
    /// </summary>
    public sealed class MarbleSimulation
    {
        private readonly LevelSimDef _level;
        private readonly SimTuning _tuning;
        private readonly float _dt;
        private readonly float[] _trackLengths;
        private readonly float[][] _segmentStarts;

        private MarbleState[] _marbles;
        private bool[] _removed; // flattened [track][element]
        private int[] _elementOffset;
        private bool[] _logicInverted;
        private List<InputRecord> _pendingInputs = new List<InputRecord>();
        private List<InputRecord> _scheduledReleases = new List<InputRecord>();
        private List<TimerRule> _timers = new List<TimerRule>();
        private bool[] _timerFired;
        private readonly List<SimEvent> _events = new List<SimEvent>();

        private int _step;
        private bool _started;
        private bool _finished;
        private bool _seesawClosed;
        private int _closeStep;
        private float _closeTime;
        private AttemptOutcome _outcome;

        public bool RecordEvents { get; set; } = true;
        public int CurrentStep => _step;
        public float Time => _step * _dt;
        public float Dt => _dt;
        public bool Started => _started;
        public bool Finished => _finished;
        public bool SeesawClosed => _seesawClosed;
        public float SeesawCloseTime => _closeTime;
        public int MarbleCount => _marbles.Length;
        public LevelSimDef Level => _level;
        public IReadOnlyList<SimEvent> Events => _events;

        public AttemptOutcome Outcome => _finished
            ? _outcome
            : throw new InvalidOperationException("Outcome requested before the attempt finished.");

        public MarbleSimulation(LevelSimDef level)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _level.Validate();
            _tuning = level.Tuning ?? throw new ArgumentException("Level tuning is null.", nameof(level));
            _dt = _tuning.Dt;

            int count = level.MarbleCount;
            _trackLengths = new float[count];
            _segmentStarts = new float[count][];
            _elementOffset = new int[count];
            int totalElements = 0;
            for (int t = 0; t < count; t++)
            {
                TrackDef track = level.Tracks[t];
                _trackLengths[t] = track.TotalLength;
                _segmentStarts[t] = new float[track.Segments.Length];
                float acc = 0f;
                for (int s = 0; s < track.Segments.Length; s++)
                {
                    _segmentStarts[t][s] = acc;
                    acc += track.Segments[s].Length;
                }
                _elementOffset[t] = totalElements;
                totalElements += track.Elements.Length;
            }

            _removed = new bool[totalElements];
            _logicInverted = new bool[Enum.GetValues(typeof(LogicColor)).Length];
            _marbles = new MarbleState[count];
            _timerFired = Array.Empty<bool>();
        }

        private MarbleSimulation(MarbleSimulation source)
        {
            _level = source._level;
            _tuning = source._tuning;
            _dt = source._dt;
            _trackLengths = source._trackLengths;
            _segmentStarts = source._segmentStarts;
            _elementOffset = source._elementOffset;
            _marbles = (MarbleState[])source._marbles.Clone();
            _removed = (bool[])source._removed.Clone();
            _logicInverted = (bool[])source._logicInverted.Clone();
            _pendingInputs = new List<InputRecord>(source._pendingInputs);
            _scheduledReleases = new List<InputRecord>(source._scheduledReleases);
            _timers = new List<TimerRule>(source._timers);
            _timerFired = (bool[])source._timerFired.Clone();
            _step = source._step;
            _started = source._started;
            _finished = source._finished;
            _seesawClosed = source._seesawClosed;
            _closeStep = source._closeStep;
            _closeTime = source._closeTime;
            _outcome = source._outcome;
            RecordEvents = false;
        }

        /// <summary>Deep copy used for invisible look-ahead prediction.</summary>
        public MarbleSimulation Clone()
        {
            return new MarbleSimulation(this);
        }

        public MarbleState GetMarble(int index)
        {
            return _marbles[index];
        }

        public float TrackLength(int track)
        {
            return _trackLengths[track];
        }

        public bool IsElementRemoved(int track, int element)
        {
            return _removed[_elementOffset[track] + element];
        }

        public bool IsLogicActive(ElementDef element)
        {
            return element.StartsActive ^ _logicInverted[(int)element.Color];
        }

        public bool AllReleased
        {
            get
            {
                for (int i = 0; i < _marbles.Length; i++)
                {
                    if (_marbles[i].Status == MarbleStatus.Waiting)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        // ---------------------------------------------------------------- setup (before the attempt starts)

        public void RemovePuddle(int track, int element)
        {
            if (_started)
            {
                throw new InvalidOperationException("Sponge can only be used before the first release.");
            }
            ElementDef def = _level.Tracks[track].Elements[element];
            if (!def.IsPuddle)
            {
                throw new InvalidOperationException($"Element {element} on track {track} is not a puddle.");
            }
            _removed[_elementOffset[track] + element] = true;
        }

        public void SetTimers(IReadOnlyList<TimerRule> timers)
        {
            if (_started)
            {
                throw new InvalidOperationException("Timers are programmed before the attempt starts.");
            }
            _timers = new List<TimerRule>(timers);
            _timerFired = new bool[_timers.Count];
        }

        public void StartAttempt()
        {
            if (_started)
            {
                throw new InvalidOperationException("Attempt already started.");
            }
            _started = true;
            Emit(SimEventType.AttemptStarted, -1, -1);
            FireTimers(TimerTrigger.AttemptStart, -1, 0);
        }

        // ---------------------------------------------------------------- inputs

        /// <summary>Queue an input; it takes effect on the next step (the click is rounded to the step grid).</summary>
        public void QueueInput(InputType type, int marble)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Start the attempt before queuing inputs.");
            }
            _pendingInputs.Add(new InputRecord(_step + 1, type, marble));
        }

        /// <summary>Queue an input recorded earlier (replays).</summary>
        public void QueueInput(InputRecord input)
        {
            if (input.Step <= _step)
            {
                throw new InvalidOperationException($"Input for step {input.Step} is in the past (now {_step}).");
            }
            _pendingInputs.Add(input);
        }

        public bool CanRelease(int marble)
        {
            return _marbles[marble].Status == MarbleStatus.Waiting;
        }

        public bool CanResume(int marble)
        {
            return _marbles[marble].Status == MarbleStatus.Caramelized;
        }

        // ---------------------------------------------------------------- stepping

        public void Step()
        {
            if (!_started)
            {
                throw new InvalidOperationException("Attempt not started.");
            }
            if (_finished)
            {
                return;
            }

            _events.Clear();
            _step++;

            ApplyInputs(_pendingInputs, true);
            ApplyInputs(_scheduledReleases, false);

            for (int i = 0; i < _marbles.Length; i++)
            {
                StepMarble(i);
            }

            UpdateSeesaw();
            CheckFinished();
        }

        /// <summary>Runs ahead until the attempt ends or the step budget runs out.</summary>
        public void RunToEnd(int maxSteps)
        {
            int limit = _step + maxSteps;
            while (!_finished && _step < limit)
            {
                Step();
            }
        }

        private void ApplyInputs(List<InputRecord> queue, bool manual)
        {
            for (int q = 0; q < queue.Count; q++)
            {
                InputRecord input = queue[q];
                if (input.Step > _step)
                {
                    continue;
                }
                queue.RemoveAt(q);
                q--;

                ref MarbleState m = ref _marbles[input.Marble];
                if (input.Type == InputType.Release && m.Status == MarbleStatus.Waiting)
                {
                    m.Status = MarbleStatus.Rolling;
                    m.Speed = 0f;
                    m.Distance = 0f;
                    Emit(SimEventType.Released, input.Marble, manual ? 1 : 0);
                    FireTimers(TimerTrigger.MarbleReleased, input.Marble, -1);
                }
                else if (input.Type == InputType.Resume && m.Status == MarbleStatus.Caramelized)
                {
                    m.Status = MarbleStatus.Rolling;
                    m.Condition = Condition.Normal;
                    m.Coating = Coating.None;
                    m.Speed = 0f;
                    Emit(SimEventType.Resumed, input.Marble, -1);
                }
            }
        }

        private void FireTimers(TimerTrigger trigger, int sourceMarble, int sourceElementId)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timerFired[i])
                {
                    continue;
                }
                TimerRule rule = _timers[i];
                if (rule.Trigger != trigger)
                {
                    continue;
                }
                bool matches = trigger switch
                {
                    TimerTrigger.AttemptStart => true,
                    TimerTrigger.MarbleReleased => rule.SourceMarble == sourceMarble,
                    _ => rule.SourceMarble == sourceMarble && rule.SourceElementId == sourceElementId
                };
                if (!matches)
                {
                    continue;
                }
                _timerFired[i] = true;
                // A zero delay still lands on the next step so ordering stays deterministic.
                int delaySteps = Math.Max(1, _tuning.SecondsToSteps(rule.DelaySeconds));
                _scheduledReleases.Add(new InputRecord(_step + delaySteps, InputType.Release, rule.TargetMarble));
            }
        }

        private void StepMarble(int index)
        {
            ref MarbleState m = ref _marbles[index];

            if (m.Status == MarbleStatus.StoppedAtDoor)
            {
                ElementDef door = _level.Tracks[index].Elements[m.NextElement];
                if (IsLogicActive(door))
                {
                    return;
                }
                m.Status = MarbleStatus.Rolling;
                Emit(SimEventType.LeftDoor, index, m.NextElement);
            }

            if (m.Status != MarbleStatus.Rolling)
            {
                return;
            }

            TrackDef track = _level.Tracks[index];
            float trackLength = _trackLengths[index];

            // 1. speed approaches maxSpeed using acceleration
            GetSpeedProfile(index, m, out float maxSpeed, out float accel);
            if (m.Speed < maxSpeed)
            {
                m.Speed = Math.Min(maxSpeed, m.Speed + accel * _dt);
            }
            else if (m.Speed > maxSpeed)
            {
                m.Speed = Math.Max(maxSpeed, m.Speed - _tuning.Deceleration * _dt);
            }

            if (m.Condition == Condition.Burning)
            {
                m.BurnTimer += _dt;
                if (m.BurnTimer >= _tuning.BurnFuseSeconds)
                {
                    Melt(index, ref m);
                    return;
                }
            }

            // 2. advance speed * dt, 3. apply every element crossed on the way
            float start = m.Distance;
            float target = m.Distance + m.Speed * _dt;
            while (m.NextElement < track.Elements.Length && track.Elements[m.NextElement].Distance <= target)
            {
                int elementIndex = m.NextElement;
                ElementDef e = track.Elements[elementIndex];

                if (_removed[_elementOffset[index] + elementIndex])
                {
                    m.NextElement++;
                    continue;
                }

                if (e.Kind == ElementKind.Door && IsLogicActive(e))
                {
                    m.Distance = e.Distance;
                    m.Speed = 0f;
                    m.Status = MarbleStatus.StoppedAtDoor;
                    Emit(SimEventType.StoppedAtDoor, index, elementIndex);
                    return;
                }

                m.NextElement++;
                if (!ApplyElement(index, ref m, e, elementIndex, ref target))
                {
                    return;
                }
            }

            m.Distance = target;

            // 4. reaching the seesaw records the arrival time
            if (m.Distance >= trackLength)
            {
                float travelled = target - start;
                float fraction = travelled > 0f ? (trackLength - start) / travelled : 1f;
                fraction = Math.Max(0f, Math.Min(1f, fraction));
                float entryTime = (_step - 1 + fraction) * _dt;
                float overflowSeconds = (1f - fraction) * _dt;
                EnterSeesaw(index, ref m, entryTime, overflowSeconds);
            }
        }

        /// <summary>Returns false when the marble stops moving this step (melted / caramelized).</summary>
        private bool ApplyElement(int index, ref MarbleState m, ElementDef e, int elementIndex, ref float target)
        {
            switch (e.Kind)
            {
                case ElementKind.Curve:
                {
                    float remaining = target - e.Distance;
                    m.Speed *= _tuning.CurveSpeedFactor;
                    target = e.Distance + remaining * _tuning.CurveSpeedFactor;
                    Emit(SimEventType.CurvePassed, index, elementIndex);
                    return true;
                }
                case ElementKind.Honey:
                    if (m.Condition == Condition.Burning)
                    {
                        Caramelize(index, ref m, e, elementIndex);
                        return false;
                    }
                    SetCoating(index, ref m, Coating.Honey, elementIndex);
                    return true;
                case ElementKind.Oil:
                    if (m.Condition == Condition.Burning)
                    {
                        return true; // no change, the fuse keeps counting
                    }
                    SetCoating(index, ref m, Coating.Oil, elementIndex);
                    return true;
                case ElementKind.Water:
                    if (m.Condition == Condition.Burning)
                    {
                        m.Condition = Condition.Normal;
                        m.BurnTimer = 0f;
                        Emit(SimEventType.Extinguished, index, elementIndex);
                    }
                    SetCoating(index, ref m, Coating.Wet, elementIndex);
                    return true;
                case ElementKind.Flame:
                    return ApplyFlame(index, ref m, e, elementIndex);
                case ElementKind.Button:
                    Emit(SimEventType.ButtonPressed, index, elementIndex);
                    _logicInverted[(int)e.Color] = !_logicInverted[(int)e.Color];
                    Emit(SimEventType.LogicToggled, index, (int)e.Color);
                    FireTimers(TimerTrigger.ButtonPressed, index, e.Id);
                    return true;
                case ElementKind.Door:
                    return true; // open door: pass through
                case ElementKind.Sensor:
                    Emit(SimEventType.SensorPassed, index, elementIndex);
                    FireTimers(TimerTrigger.SensorPassed, index, e.Id);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e), e.Kind, "Unknown element kind.");
            }
        }

        private bool ApplyFlame(int index, ref MarbleState m, ElementDef e, int elementIndex)
        {
            if (!IsLogicActive(e))
            {
                return true; // flame switched off
            }

            if (m.Condition == Condition.Burning)
            {
                return true;
            }

            switch (m.Coating)
            {
                case Coating.None:
                    m.Distance = e.Distance;
                    Melt(index, ref m);
                    return false;
                case Coating.Honey:
                    Caramelize(index, ref m, e, elementIndex);
                    return false;
                case Coating.Oil:
                    m.Coating = Coating.None;
                    m.Condition = Condition.Burning;
                    m.BurnTimer = 0f;
                    Emit(SimEventType.Ignited, index, elementIndex);
                    return true;
                case Coating.Wet:
                    m.Coating = Coating.None;
                    Emit(SimEventType.Evaporated, index, elementIndex);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(m), m.Coating, "Unknown coating.");
            }
        }

        private void Caramelize(int index, ref MarbleState m, ElementDef e, int elementIndex)
        {
            m.Distance = e.Distance;
            m.Speed = 0f;
            m.Condition = Condition.Caramelized;
            m.BurnTimer = 0f;
            m.Status = MarbleStatus.Caramelized;
            Emit(SimEventType.Caramelized, index, elementIndex);
        }

        private void SetCoating(int index, ref MarbleState m, Coating coating, int elementIndex)
        {
            if (m.Coating == coating)
            {
                return;
            }
            m.Coating = coating;
            Emit(SimEventType.CoatingChanged, index, elementIndex);
        }

        private void Melt(int index, ref MarbleState m)
        {
            m.Status = MarbleStatus.Melted;
            m.Speed = 0f;
            Emit(SimEventType.Melted, index, -1);
        }

        private void GetSpeedProfile(int index, MarbleState m, out float maxSpeed, out float accel)
        {
            float segmentAccel = SegmentAcceleration(index, m.Distance);
            maxSpeed = _tuning.BaseMaxSpeed;
            accel = segmentAccel;

            if (m.Condition == Condition.Burning)
            {
                maxSpeed *= _tuning.BurningMaxSpeedFactor;
                accel *= _tuning.BurningAccelFactor;
                return;
            }

            switch (m.Coating)
            {
                case Coating.Honey:
                    maxSpeed *= _tuning.HoneyMaxSpeedFactor;
                    accel *= _tuning.HoneyAccelFactor;
                    break;
                case Coating.Oil:
                    maxSpeed *= _tuning.OilMaxSpeedFactor;
                    accel *= _tuning.OilAccelFactor;
                    break;
            }
        }

        private float SegmentAcceleration(int track, float distance)
        {
            float[] starts = _segmentStarts[track];
            SegmentDef[] segments = _level.Tracks[track].Segments;
            int s = segments.Length - 1;
            while (s > 0 && starts[s] > distance)
            {
                s--;
            }
            return segments[s].Acceleration;
        }

        // ---------------------------------------------------------------- seesaw

        private float SeesawHalf => _tuning.SeesawLength * 0.5f;
        private float SeesawFixedSpeed => SeesawHalf / _tuning.SeesawHalfSeconds;
        private int FinishSteps => Math.Max(1, _tuning.SecondsToSteps(_tuning.SeesawFinishSeconds));

        private void EnterSeesaw(int index, ref MarbleState m, float entryTime, float overflowSeconds)
        {
            m.Distance = _trackLengths[index];
            m.Entered = true;
            m.EntryTime = entryTime;

            if (_seesawClosed)
            {
                m.Status = MarbleStatus.Blocked;
                m.Speed = 0f;
                Emit(SimEventType.Blocked, index, -1);
                return;
            }

            m.Status = MarbleStatus.OnSeesaw;
            m.SeesawPos = -overflowSeconds * SeesawFixedSpeed; // advanced by UpdateSeesaw this same step
            Emit(SimEventType.EnteredSeesaw, index, -1);
        }

        private void UpdateSeesaw()
        {
            if (!_seesawClosed)
            {
                float half = SeesawHalf;
                float step = SeesawFixedSpeed * _dt;
                float earliestCross = float.MaxValue;
                for (int i = 0; i < _marbles.Length; i++)
                {
                    ref MarbleState m = ref _marbles[i];
                    if (m.Status != MarbleStatus.OnSeesaw)
                    {
                        continue;
                    }
                    m.Speed = SeesawFixedSpeed;
                    m.SeesawPos += step;
                    if (m.SeesawPos >= half)
                    {
                        earliestCross = Math.Min(earliestCross, m.EntryTime + _tuning.SeesawHalfSeconds);
                    }
                }

                if (earliestCross < float.MaxValue)
                {
                    _seesawClosed = true;
                    _closeStep = _step;
                    _closeTime = earliestCross;
                    for (int i = 0; i < _marbles.Length; i++)
                    {
                        ref MarbleState m = ref _marbles[i];
                        if (m.Status != MarbleStatus.OnSeesaw)
                        {
                            continue;
                        }
                        if (m.EntryTime > _closeTime)
                        {
                            // Entered during the same step the seesaw shut, but after the exact closing instant.
                            m.Status = MarbleStatus.Blocked;
                            m.Speed = 0f;
                            m.SeesawPos = 0f;
                            Emit(SimEventType.Blocked, i, -1);
                            continue;
                        }
                        m.SeesawCloseStartPos = m.SeesawPos;
                    }
                    Emit(SimEventType.SeesawClosed, -1, -1);
                }
                return;
            }

            float length = _tuning.SeesawLength;
            int finishSteps = FinishSteps;
            float progress = Math.Min(1f, (float)(_step - _closeStep) / finishSteps);
            for (int i = 0; i < _marbles.Length; i++)
            {
                ref MarbleState m = ref _marbles[i];
                if (m.Status != MarbleStatus.OnSeesaw)
                {
                    continue;
                }
                float remaining = length - m.SeesawCloseStartPos;
                m.Speed = remaining / _tuning.SeesawFinishSeconds;
                m.SeesawPos = m.SeesawCloseStartPos + remaining * progress;
                if (_step - _closeStep >= finishSteps)
                {
                    m.SeesawPos = length;
                    m.Status = MarbleStatus.Arrived;
                    m.Speed = 0f;
                    Emit(SimEventType.Arrived, i, -1);
                    if (m.Condition == Condition.Burning)
                    {
                        m.MeltedAtFinish = true;
                        Emit(SimEventType.MeltedAtFinish, i, -1);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- end of attempt

        private void CheckFinished()
        {
            if (_pendingInputs.Count > 0 || _scheduledReleases.Count > 0)
            {
                return;
            }

            for (int i = 0; i < _marbles.Length; i++)
            {
                MarbleStatus s = _marbles[i].Status;
                if (s == MarbleStatus.OnSeesaw || s == MarbleStatus.Rolling)
                {
                    return; // still in motion: the attempt is never cut short
                }
                if (!_seesawClosed && (s == MarbleStatus.Waiting || s == MarbleStatus.Caramelized))
                {
                    return; // the player can still act while the seesaw is open
                }
            }

            _finished = true;
            _outcome = BuildOutcome();
            Emit(SimEventType.AttemptFinished, -1, -1);
        }

        private AttemptOutcome BuildOutcome()
        {
            var outcome = new AttemptOutcome
            {
                CloseTime = _closeTime,
                FinishStep = _step,
                Marbles = new MarbleResult[_marbles.Length]
            };

            bool success = true;
            float first = float.MaxValue;
            float last = float.MinValue;
            for (int i = 0; i < _marbles.Length; i++)
            {
                MarbleState m = _marbles[i];
                var result = new MarbleResult
                {
                    FinalStatus = m.Status,
                    Entered = m.Entered,
                    EntryTime = m.EntryTime
                };

                switch (m.Status)
                {
                    case MarbleStatus.Arrived:
                        result.Failure = FailureReason.None;
                        break;
                    case MarbleStatus.Blocked:
                        result.Failure = FailureReason.BlockedBySeesaw;
                        result.Lateness = m.EntryTime - _closeTime;
                        break;
                    case MarbleStatus.Melted:
                        result.Failure = FailureReason.Melted;
                        break;
                    default:
                        result.Failure = FailureReason.NeverArrived;
                        break;
                }

                if (result.Failure != FailureReason.None)
                {
                    success = false;
                }
                if (m.Entered)
                {
                    first = Math.Min(first, m.EntryTime);
                    last = Math.Max(last, m.EntryTime);
                }
                outcome.Marbles[i] = result;
            }

            outcome.Success = success;
            outcome.Spread = last >= first ? last - first : 0f;
            return outcome;
        }

        private void Emit(SimEventType type, int marble, int element)
        {
            if (RecordEvents)
            {
                _events.Add(new SimEvent(type, marble, element, _step));
            }
        }
    }
}
