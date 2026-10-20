using System.Text;
using MarbleSync.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// Screen-space HUD. Pure presentation: reads controller state and forwards button presses.
    /// Deliberately quiet: no instructions and no arrival times, the player works the timing out.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;

        [Header("Texts")]
        [SerializeField] private Text outcomeText;
        [SerializeField] private Text timersText;
        [SerializeField] private Text spongeText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text replayText;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button prevLevelButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private HoldButton restartButton;
        [SerializeField] private Button timerUpButton;
        [SerializeField] private Button timerDownButton;
        [SerializeField] private Button timerTriggerButton;
        [SerializeField] private Button timerClearButton;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button replaySpeedButton;
        [SerializeField] private Button exitReplayButton;
        [SerializeField] private Button clearPlanButton;

        [Header("Panels")]
        [SerializeField] private GameObject toolsPanel;
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private GameObject replayPanel;
        [SerializeField] private GameObject historyPanel;
        [SerializeField] private RectTransform historyContainer;
        [SerializeField] private HistoryEntryView historyEntryPrefab;

        private ComponentPool<HistoryEntryView> _historyPool;
        private bool _hintOpen;
        private readonly StringBuilder _sb = new StringBuilder();

        private void Awake()
        {
            _historyPool = new ComponentPool<HistoryEntryView>(historyEntryPrefab, historyContainer, 12);
            startButton.onClick.AddListener(controller.BeginAttempt);
            prevLevelButton.onClick.AddListener(controller.PreviousLevel);
            nextLevelButton.onClick.AddListener(controller.NextLevel);
            continueButton.onClick.AddListener(controller.NextLevel);
            restartButton.Completed += controller.Restart;
            timerUpButton.onClick.AddListener(() => controller.AdjustSelectedTimer(1));
            timerDownButton.onClick.AddListener(() => controller.AdjustSelectedTimer(-1));
            timerTriggerButton.onClick.AddListener(controller.CycleSelectedTrigger);
            timerClearButton.onClick.AddListener(controller.ClearSelectedTimer);
            hintButton.onClick.AddListener(() =>
            {
                _hintOpen = !_hintOpen;
                Refresh();
            });
            replaySpeedButton.onClick.AddListener(controller.CycleReplaySpeed);
            exitReplayButton.onClick.AddListener(controller.Restart);
            clearPlanButton.onClick.AddListener(controller.ClearPlannedInputs);
        }

        private void OnEnable()
        {
            controller.LevelLoaded += OnLevelLoaded;
            controller.StateChanged += Refresh;
            controller.AttemptFinished += OnAttemptFinished;
        }

        private void OnDisable()
        {
            controller.LevelLoaded -= OnLevelLoaded;
            controller.StateChanged -= Refresh;
            controller.AttemptFinished -= OnAttemptFinished;
        }

        private void OnLevelLoaded()
        {
            _hintOpen = false;
            Refresh();
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            Refresh();
        }

        private void Update()
        {
            // A marble can melt or be blocked mid-attempt: from then on restart no longer needs a hold.
            restartButton.RequireHold = controller.RestartNeedsHold;
            if (controller.Phase == AttemptPhase.Running)
            {
                restartButton.gameObject.SetActive(controller.CanRestart);
            }

            Keyboard kb = Keyboard.current;
            restartButton.SetExternalHold(kb != null && kb.rKey.isPressed && controller.CanRestart);
        }

        private void Refresh()
        {
            LevelDefinition level = controller.Level;
            AttemptPhase phase = controller.Phase;

            startButton.gameObject.SetActive(phase == AttemptPhase.Setup && level.TimersUnlocked);
            restartButton.gameObject.SetActive(controller.CanRestart && phase != AttemptPhase.Replay);
            restartButton.RequireHold = controller.RestartNeedsHold;
            continueButton.gameObject.SetActive(phase == AttemptPhase.Finished && controller.LastRecord.Outcome.Success);
            clearPlanButton.gameObject.SetActive(phase == AttemptPhase.Setup && controller.HasPlannedInputs);

            bool sponges = level.SpongeCount > 0;
            spongeText.gameObject.SetActive(sponges);
            spongeText.text = $"Esponjas: {controller.SpongesLeft}/{level.SpongeCount}";

            bool hintAvailable = controller.HintUnlocked && level.TimerHint.Length > 0;
            hintButton.gameObject.SetActive(hintAvailable);
            hintText.gameObject.SetActive(hintAvailable && _hintOpen);
            hintText.text = level.TimerHint;

            bool timers = RefreshTimers(level, phase);
            toolsPanel.SetActive(sponges || timers || hintAvailable);

            RefreshOutcome(level, phase);
            RefreshReplay(phase);
            RefreshHistory();
        }

        private bool RefreshTimers(LevelDefinition level, AttemptPhase phase)
        {
            var rules = controller.Timers.ToList();
            bool show = level.TimersUnlocked && (phase == AttemptPhase.Setup || rules.Count > 0);
            timerPanel.SetActive(show);
            if (!show)
            {
                return false;
            }

            bool editing = phase == AttemptPhase.Setup && controller.SelectedTimerMarble >= 0;
            timerUpButton.interactable = editing;
            timerDownButton.interactable = editing;
            timerTriggerButton.interactable = editing;
            timerClearButton.interactable = editing;

            _sb.Clear();
            for (int i = 0; i < rules.Count; i++)
            {
                if (i > 0)
                {
                    _sb.Append('\n');
                }
                _sb.Append("• ").Append(GameText.DescribeTimer(rules[i], level));
            }
            timersText.text = _sb.ToString();
            return true;
        }

        private void RefreshOutcome(LevelDefinition level, AttemptPhase phase)
        {
            AttemptRecord record = phase == AttemptPhase.Finished ? controller.LastRecord
                : phase == AttemptPhase.Replay && controller.Sim.Finished ? controller.ReplayRecord
                : null;

            outcomeText.gameObject.SetActive(record != null);
            if (record == null)
            {
                return;
            }

            AttemptOutcome o = record.Outcome;
            _sb.Clear();
            if (o.Success)
            {
                _sb.Append("<size=48><b>Sincronizadas!</b></size>");
            }
            else
            {
                _sb.Append("<size=44><b>Quase!</b></size>");
                for (int i = 0; i < o.Marbles.Length; i++)
                {
                    string line = GameText.DescribeFailure(i, o.Marbles[i], level);
                    if (line.Length > 0)
                    {
                        _sb.Append('\n').Append(line);
                    }
                }
            }
            outcomeText.text = _sb.ToString();
            outcomeText.color = o.Success ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.8f, 0.55f);
        }

        private void RefreshReplay(AttemptPhase phase)
        {
            replayPanel.SetActive(phase == AttemptPhase.Replay);
            if (phase == AttemptPhase.Replay)
            {
                replayText.text = $"Replay #{controller.ReplayRecord.AttemptNumber}  ·  {controller.ReplaySpeed:0.##}x";
            }
        }

        private void RefreshHistory()
        {
            _historyPool.ReleaseAll();
            var history = controller.History;
            historyPanel.SetActive(history.Count > 0);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                HistoryEntryView entry = _historyPool.Get();
                entry.transform.SetAsLastSibling();
                entry.Bind(history[i], controller.GhostRecord == history[i], controller.WatchReplay, controller.ToggleGhost, controller.LoadAsBase);
            }
        }
    }
}
