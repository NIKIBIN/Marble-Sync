using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// "The almost": once the last marble is released it reads the invisible look-ahead prediction.
    /// If the predicted spread is under ~2x the window, it starts the drumroll, zooms to keep every marble
    /// in frame and slows time slightly for the final instants. Closer = more intense.
    /// </summary>
    public sealed class TensionDirector : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private GameCamera gameCamera;
        [SerializeField] private SfxPlayer sfx;
        [SerializeField] private float windowMultiplier = 2f;
        [SerializeField] private float minDrumRate = 6f;
        [SerializeField] private float maxDrumRate = 30f;
        [SerializeField] private float slowMotionLead = 0.8f;
        [Tooltip("Drums and zoom start this many seconds before the first marble reaches the seesaw.")]
        [SerializeField] private float drumLeadSeconds = 1.2f;
        [SerializeField, Range(0.1f, 1f)] private float minTimeScale = 0.4f;
        [SerializeField] private bool slowMotionEnabled = true;

        private bool _tense;
        private float _intensity;
        private float _predictedFinishTime;
        private float _predictedFirstEntry;
        private float _drumTimer;

        public bool IsTense => _tense;
        public float Intensity => _intensity;

        private void OnEnable()
        {
            controller.LevelLoaded += OnLevelLoaded;
            controller.StateChanged += OnStateChanged;
            controller.PredictionUpdated += OnPrediction;
            controller.AttemptFinished += OnAttemptFinished;
        }

        private void OnDisable()
        {
            controller.LevelLoaded -= OnLevelLoaded;
            controller.StateChanged -= OnStateChanged;
            controller.PredictionUpdated -= OnPrediction;
            controller.AttemptFinished -= OnAttemptFinished;
            Time.timeScale = 1f;
        }

        private void OnLevelLoaded()
        {
            gameCamera.Frame(controller.LevelBounds(), true);
        }

        private void OnStateChanged()
        {
            if (controller.Phase == AttemptPhase.Setup || controller.Phase == AttemptPhase.Replay)
            {
                Calm();
            }
        }

        private void OnPrediction(AttemptOutcome predicted)
        {
            float first = float.MaxValue;
            float last = float.MinValue;
            int entered = 0;
            for (int i = 0; i < predicted.Marbles.Length; i++)
            {
                MarbleResult m = predicted.Marbles[i];
                if (!m.Entered)
                {
                    continue;
                }
                entered++;
                first = Mathf.Min(first, m.EntryTime);
                last = Mathf.Max(last, m.EntryTime);
            }

            float threshold = controller.Window * windowMultiplier;
            float spread = entered == predicted.Marbles.Length ? last - first : float.MaxValue;
            _tense = spread < threshold;
            _intensity = _tense ? Mathf.Clamp01(1f - spread / threshold) : 0f;
            _predictedFinishTime = predicted.FinishStep * controller.Sim.Dt;
            _predictedFirstEntry = first;
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            _tense = false;
            Time.timeScale = 1f;
        }

        private void Calm()
        {
            _tense = false;
            _intensity = 0f;
            Time.timeScale = 1f;
            gameCamera.Frame(controller.LevelBounds(), false);
        }

        private void Update()
        {
            AttemptPhase phase = controller.Phase;
            bool nearRamp = phase == AttemptPhase.Running && _tense
                            && controller.Sim.Time >= _predictedFirstEntry - drumLeadSeconds;

            if (nearRamp || phase == AttemptPhase.Finished)
            {
                // Keep every marble and the whole seesaw in frame, so the final rush stays on screen.
                gameCamera.Frame(FinishFocus(), false);
            }
            else
            {
                gameCamera.Frame(controller.LevelBounds(), false);
            }

            if (!nearRamp)
            {
                return;
            }

            _drumTimer -= Time.unscaledDeltaTime;
            if (_drumTimer <= 0f)
            {
                _drumTimer = 1f / Mathf.Lerp(minDrumRate, maxDrumRate, _intensity);
                sfx.DrumHit(Mathf.Lerp(0.35f, 1f, _intensity));
            }

            if (slowMotionEnabled)
            {
                float remaining = _predictedFinishTime - controller.Sim.Time;
                float slow = remaining < slowMotionLead ? Mathf.Lerp(1f, minTimeScale, _intensity) : 1f;
                Time.timeScale = slow;
            }
        }

        private Bounds FinishFocus()
        {
            LevelDefinition level = controller.Level;
            Bounds focus = controller.ActiveMarbleBounds();
            for (int lane = 0; lane < level.MarbleCount; lane++)
            {
                focus.Encapsulate(level.SeesawLaneStart(lane));
                focus.Encapsulate(level.SeesawLaneEnd(lane));
            }
            return focus;
        }
    }
}
