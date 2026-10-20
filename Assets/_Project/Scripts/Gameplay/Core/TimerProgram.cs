using System;
using System.Collections.Generic;
using System.Globalization;
using MarbleSync.Simulation;

namespace MarbleSync.Gameplay
{
    /// <summary>The player's programmed timers: at most one WHEN -> WAIT -> RELEASE rule per marble.</summary>
    public sealed class TimerProgram
    {
        private readonly TimerRule?[] _rules;
        private readonly float _stepSeconds;

        public TimerProgram(int marbleCount, float stepSeconds)
        {
            _rules = new TimerRule?[marbleCount];
            _stepSeconds = stepSeconds;
        }

        public int MarbleCount => _rules.Length;

        public bool TryGet(int marble, out TimerRule rule)
        {
            TimerRule? value = _rules[marble];
            rule = value.GetValueOrDefault();
            return value.HasValue;
        }

        public void AdjustDelay(int marble, int direction)
        {
            if (!TryGet(marble, out TimerRule rule))
            {
                if (direction <= 0)
                {
                    return;
                }
                _rules[marble] = new TimerRule(marble, TimerTrigger.AttemptStart, 0f);
                return;
            }

            float delay = rule.DelaySeconds + direction * _stepSeconds;
            if (delay < -0.001f)
            {
                _rules[marble] = null; // stepping below zero switches the timer off
                return;
            }
            rule.DelaySeconds = Math.Max(0f, delay);
            _rules[marble] = rule;
        }

        /// <summary>Start of attempt -> "when marble k is released" for every other marble -> back to start.</summary>
        public void CycleTrigger(int marble)
        {
            TimerRule rule = TryGet(marble, out TimerRule existing)
                ? existing
                : new TimerRule(marble, TimerTrigger.AttemptStart, 0f);

            int next;
            if (rule.Trigger != TimerTrigger.MarbleReleased)
            {
                next = NextOther(marble, -1);
            }
            else
            {
                next = NextOther(marble, rule.SourceMarble);
            }

            if (next < 0)
            {
                rule.Trigger = TimerTrigger.AttemptStart;
                rule.SourceMarble = 0;
            }
            else
            {
                rule.Trigger = TimerTrigger.MarbleReleased;
                rule.SourceMarble = next;
            }
            rule.SourceElementId = 0;
            _rules[marble] = rule;
        }

        /// <summary>"Dragging" the timer onto a sensor or button: count from when that lane's marble passes it.</summary>
        public void AttachToElement(int marble, TimerTrigger trigger, int sourceMarble, int elementId)
        {
            if (trigger != TimerTrigger.SensorPassed && trigger != TimerTrigger.ButtonPressed)
            {
                throw new ArgumentException("Only sensors and buttons can anchor a timer.", nameof(trigger));
            }
            TimerRule rule = TryGet(marble, out TimerRule existing)
                ? existing
                : new TimerRule(marble, trigger, 0f);
            rule.Trigger = trigger;
            rule.SourceMarble = sourceMarble;
            rule.SourceElementId = elementId;
            _rules[marble] = rule;
        }

        public void Clear(int marble)
        {
            _rules[marble] = null;
        }

        public void Load(IReadOnlyList<TimerRule> rules)
        {
            Array.Clear(_rules, 0, _rules.Length);
            for (int i = 0; i < rules.Count; i++)
            {
                _rules[rules[i].TargetMarble] = rules[i];
            }
        }

        public List<TimerRule> ToList()
        {
            var list = new List<TimerRule>();
            for (int i = 0; i < _rules.Length; i++)
            {
                if (_rules[i].HasValue)
                {
                    list.Add(_rules[i].Value);
                }
            }
            return list;
        }

        private int NextOther(int marble, int after)
        {
            for (int i = after + 1; i < _rules.Length; i++)
            {
                if (i != marble)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    /// <summary>Player-facing Portuguese strings.</summary>
    public static class GameText
    {
        private static readonly NumberFormatInfo PtBr = new NumberFormatInfo { NumberDecimalSeparator = "," };

        public static string Seconds(float seconds)
        {
            return seconds.ToString("0.00", PtBr) + " s";
        }

        public static string ColorName(LogicColor color)
        {
            switch (color)
            {
                case LogicColor.Blue: return "azul";
                case LogicColor.Red: return "vermelho";
                case LogicColor.Green: return "verde";
                case LogicColor.Yellow: return "amarelo";
                default: throw new ArgumentOutOfRangeException(nameof(color), color, null);
            }
        }

        public static string DescribeTimer(TimerRule rule, LevelDefinition level)
        {
            string target = level.Lane(rule.TargetMarble).MarbleName;
            string delay = rule.DelaySeconds.ToString("0.##", PtBr) + " s";
            string when;
            switch (rule.Trigger)
            {
                case TimerTrigger.AttemptStart:
                    when = "No início da tentativa";
                    break;
                case TimerTrigger.MarbleReleased:
                    when = $"Quando a {level.Lane(rule.SourceMarble).MarbleName} for solta";
                    break;
                case TimerTrigger.SensorPassed:
                    when = $"Quando a {level.Lane(rule.SourceMarble).MarbleName} passar pelo Sensor {rule.SourceElementId}";
                    break;
                case TimerTrigger.ButtonPressed:
                    when = $"Quando a {level.Lane(rule.SourceMarble).MarbleName} apertar o Botão {rule.SourceElementId}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rule), rule.Trigger, null);
            }
            return $"{when}, aguarde {delay} e solte a {target}.";
        }

        public static string DescribeFailure(int marble, MarbleResult result, LevelDefinition level)
        {
            string name = level.Lane(marble).MarbleName;
            switch (result.Failure)
            {
                case FailureReason.BlockedBySeesaw:
                    return $"{name} chegou {Seconds(result.Lateness)} atrasada";
                case FailureReason.Melted:
                    return $"{name} derreteu";
                case FailureReason.NeverArrived:
                    return $"{name} não chegou à gangorra";
                default:
                    return string.Empty;
            }
        }
    }
}
