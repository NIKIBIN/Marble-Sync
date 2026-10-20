using System.Collections.Generic;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// The final seesaw: one half-pipe track per marble on a shared deck pivoting at its middle.
    /// When the first marble passes the middle the deck tips: the entry side rises and blocks
    /// everyone still outside. It levels out on success and stays raised on a miss.
    /// </summary>
    public sealed class SeesawView : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private TrackView trackView;
        [SerializeField] private Transform deck;
        [SerializeField] private Transform plank;
        [SerializeField] private Transform laneRoot;
        [SerializeField] private HalfPipeView pipePrefab;
        [SerializeField] private Transform halfMarker;
        [SerializeField] private Transform fulcrum;
        [SerializeField] private float plankMargin = 0.25f;
        [SerializeField] private float plankThickness = 0.15f;
        [Tooltip("Degrees the entry side rises when the seesaw closes.")]
        [SerializeField] private float closeTilt = 14f;
        [SerializeField] private float successTilt = 0f;
        [SerializeField] private float failTilt = 18f;
        [SerializeField] private float tiltSpeed = 10f;

        private ComponentPool<HalfPipeView> _pipes;
        private readonly List<Vector3> _points = new List<Vector3>(2);
        private Vector3[] _localStart = new Vector3[0];
        private Vector3[] _localEnd = new Vector3[0];
        private float _targetTilt;
        private float _tilt;

        private void OnEnable()
        {
            controller.LevelLoaded += Layout;
            controller.StateChanged += OnStateChanged;
            controller.SimEventRaised += OnSimEvent;
            controller.AttemptFinished += OnAttemptFinished;
        }

        private void OnDisable()
        {
            controller.LevelLoaded -= Layout;
            controller.StateChanged -= OnStateChanged;
            controller.SimEventRaised -= OnSimEvent;
            controller.AttemptFinished -= OnAttemptFinished;
        }

        /// <summary>World position on a marble's seesaw track, following the deck's current tilt.</summary>
        public Vector3 LanePoint(int lane, float t)
        {
            return deck.TransformPoint(Vector3.Lerp(_localStart[lane], _localEnd[lane], t));
        }

        private void Layout()
        {
            _pipes ??= new ComponentPool<HalfPipeView>(pipePrefab, laneRoot, 5);
            _pipes.ReleaseAll();

            LevelDefinition level = controller.Level;
            float length = level.Tuning.SeesawLength;
            float width = level.SeesawWidth + plankMargin;

            transform.position = (level.SeesawStart + level.SeesawEnd) * 0.5f;
            transform.rotation = Quaternion.LookRotation(level.SeesawDirection, Vector3.up);
            deck.localPosition = Vector3.zero;
            deck.localRotation = Quaternion.identity;
            laneRoot.localPosition = Vector3.zero;
            laneRoot.localRotation = Quaternion.identity;

            plank.localPosition = new Vector3(0f, -plankThickness * 0.5f, 0f);
            plank.localScale = new Vector3(width, plankThickness, length);

            Quaternion toLocal = Quaternion.Inverse(transform.rotation);
            _localStart = new Vector3[level.MarbleCount];
            _localEnd = new Vector3[level.MarbleCount];
            for (int lane = 0; lane < level.MarbleCount; lane++)
            {
                _localStart[lane] = toLocal * (level.SeesawLaneStart(lane) - transform.position);
                _localEnd[lane] = toLocal * (level.SeesawLaneEnd(lane) - transform.position);
                _points.Clear();
                _points.Add(_localStart[lane]);
                _points.Add(_localEnd[lane]);
                HalfPipeView pipe = _pipes.Get();
                pipe.name = $"SeesawTrack_{level.Lane(lane).MarbleName}";
                pipe.transform.localPosition = Vector3.zero;
                pipe.transform.localRotation = Quaternion.identity;
                pipe.Build(_points, trackView.LaneTint(level, lane), false);
            }

            // The middle mark sticks out on both sides of the deck instead of crossing over the tracks.
            halfMarker.localPosition = new Vector3(0f, -0.02f, 0f);
            halfMarker.localScale = new Vector3(width + 0.35f, 0.06f, 0.12f);
            fulcrum.position = transform.position - Vector3.up * 0.6f;
            fulcrum.rotation = Quaternion.identity;
            ResetPose();
        }

        private void OnStateChanged()
        {
            if (controller.Phase == AttemptPhase.Setup || controller.Phase == AttemptPhase.Replay)
            {
                ResetPose();
            }
        }

        private void ResetPose()
        {
            _targetTilt = 0f;
            _tilt = 0f;
            deck.localRotation = Quaternion.identity;
        }

        private void OnSimEvent(SimEvent e)
        {
            if (e.Type == SimEventType.SeesawClosed)
            {
                _targetTilt = closeTilt;
            }
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            _targetTilt = record.Outcome.Success ? successTilt : failTilt;
        }

        private void Update()
        {
            _tilt = Mathf.MoveTowards(_tilt, _targetTilt, Time.deltaTime * tiltSpeed * Mathf.Max(1f, Mathf.Abs(_targetTilt - _tilt)));
            // Positive pitch lowers the far end and raises the entry side.
            deck.localRotation = Quaternion.Euler(_tilt, 0f, 0f);
        }
    }
}
