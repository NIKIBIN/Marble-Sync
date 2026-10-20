using System.Text;
using MarbleSync.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarbleSync.Gameplay
{
    /// <summary>Screen-space HUD. Pure presentation: reads controller state and forwards button presses.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;

        [Header("Texts")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text introText;
        [SerializeField] private Text statusText;
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
            Keyboard kb = Keyboard.current;
            restartButton.SetExternalHold(kb != null && kb.rKey.isPressed && controller.CanRestart);

            // Caramelized marbles can appear mid-attempt: keep the status line live while running.
            if (controller.Phase == AttemptPhase.Running)
            {
                statusText.text = StatusLine();
                restartButton.gameObject.SetActive(controller.CanRestart);
            }
        }

        private void Refresh()
        {
            LevelDefinition level = controller.Level;
            AttemptPhase phase = controller.Phase;

            titleText.text = $"{level.LevelId}  ·  {level.DisplayName}";
            introText.text = level.IntroText;
            statusText.text = StatusLine();

            startButton.gameObject.SetActive(phase == AttemptPhase.Setup);
            restartButton.gameObject.SetActive(controller.CanRestart && phase != AttemptPhase.Replay);
            continueButton.gameObject.SetActive(phase == AttemptPhase.Finished && controller.LastRecord.Outcome.Success);
            clearPlanButton.gameObject.SetActive(phase == AttemptPhase.Setup && controller.HasPlannedInputs);

            spongeText.gameObject.SetActive(level.SpongeCount > 0);
            spongeText.text = $"Esponjas: {controller.SpongesLeft}/{level.SpongeCount}";

            RefreshTimers(level, phase);
            RefreshOutcome(level, phase);
            RefreshReplay(phase);
            RefreshHistory();

            hintButton.gameObject.SetActive(controller.HintUnlocked && level.TimerHint.Length > 0);
            hintText.gameObject.SetActive(controller.HintUnlocked && _hintOpen);
            hintText.text = "Dica: " + level.TimerHint;
        }

        private string StatusLine()
        {
            LevelDefinition level = controller.Level;
            switch (controller.Phase)
            {
                case AttemptPhase.Setup:
                    _sb.Clear();
                    if (level.TimersUnlocked)
                    {
                        _sb.Append("Clique numa bolinha para programar o timer dela. Espaço ou \"Começar\" inicia a tentativa.");
                    }
                    else
                    {
                        _sb.Append($"Clique numa bolinha (ou tecle 1–{level.MarbleCount}) para soltá-la. A tentativa começa no primeiro clique.");
                    }
                    if (controller.SpongesLeft > 0)
                    {
                        _sb.Append("\nClique numa poça para usar a esponja (só antes de soltar).");
                    }
                    if (controller.HasPlannedInputs)
                    {
                        _sb.Append("\nTentativa carregada como base: as solturas dela vão se repetir.");
                    }
                    if (controller.GhostRecord != null)
                    {
                        _sb.Append($"\nFantasma: tentativa #{controller.GhostRecord.AttemptNumber}.");
                    }
                    return _sb.ToString();
                case AttemptPhase.Running:
                    for (int i = 0; i < controller.Sim.MarbleCount; i++)
                    {
                        if (controller.Sim.CanResume(i))
                        {
                            return $"A {level.Lane(i).MarbleName} caramelizou! Clique nela para voltar a rolar.";
                        }
                    }
                    return controller.Sim.AllReleased ? string.Empty : "Clique nas bolinhas para soltá-las.";
                case AttemptPhase.Finished:
                    return "Segure Reiniciar (ou R) para tentar de novo.";
                case AttemptPhase.Replay:
                    return string.Empty;
                default:
                    throw new System.ArgumentOutOfRangeException();
            }
        }

        private void RefreshTimers(LevelDefinition level, AttemptPhase phase)
        {
            bool show = level.TimersUnlocked && (phase == AttemptPhase.Setup || controller.Timers.ToList().Count > 0);
            timerPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            bool editing = phase == AttemptPhase.Setup && controller.SelectedTimerMarble >= 0;
            timerUpButton.interactable = editing;
            timerDownButton.interactable = editing;
            timerTriggerButton.interactable = editing;
            timerClearButton.interactable = editing;

            _sb.Clear();
            _sb.Append("<b>Timers</b>");
            if (phase == AttemptPhase.Setup)
            {
                _sb.Append(editing
                    ? $"  (editando: {level.Lane(controller.SelectedTimerMarble).MarbleName} — ▲▼ 0,25 s, T gatilho, clique num sensor/botão para ancorar)"
                    : "  (clique numa bolinha para editar)");
            }
            var rules = controller.Timers.ToList();
            if (rules.Count == 0)
            {
                _sb.Append("\nNenhum timer: todas as solturas são manuais.");
            }
            for (int i = 0; i < rules.Count; i++)
            {
                _sb.Append("\n• ").Append(GameText.DescribeTimer(rules[i], level));
            }
            timersText.text = _sb.ToString();
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
                _sb.Append("<size=44><b>Sincronizadas!</b></size>\n");
                _sb.Append($"Diferença entre a primeira e a última: {GameText.Seconds(o.Spread)}");
            }
            else
            {
                _sb.Append("<size=40><b>Quase!</b></size>");
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
                replayText.text = $"Replay da tentativa #{controller.ReplayRecord.AttemptNumber}  ·  {controller.ReplaySpeed:0.##}x";
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
