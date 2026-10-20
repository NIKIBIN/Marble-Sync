using System.Collections.Generic;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// The final seesaw: one half-pipe track per marble on a shared deck. Trembles while open,
    /// shuts its gate at 50%, balances on success or flips up on a miss.
    /// </summary>
    public sealed class SeesawView : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private TrackView trackView;
        [SerializeField] private Transform deck;
        [SerializeField] private Transform plank;
        [SerializeField] private Transform laneRoot;
        [SerializeField] private HalfPipeView pipePrefab;
        [SerializeField] private Transform gate;
        [SerializeField] private Transform halfMarker;
        [SerializeField] private Transform fulcrum;
        [SerializeField] private float plankMargin = 0.25f;
        [SerializeField] private float plankThickness = 0.15f;
        [SerializeField] private float shakeDegrees = 0.8f;
        [SerializeField] private float shakeFrequency = 18f;
        [SerializeField] private float successTilt = 6f;
        [SerializeField] private float failTilt = -18f;

        private ComponentPool<HalfPipeView> _pipes;
        private readonly List<Vector3> _points = new List<Vector3>(2);
        private Vector3 _gateOpen;
        private Vector3 _gateClosed;
        private float _gateBlend;
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
            for (int lane = 0; lane < level.MarbleCount; lane++)
            {
                _points.Clear();
                _points.Add(toLocal * (level.SeesawLaneStart(lane) - transform.position));
                _points.Add(toLocal * (level.SeesawLaneEnd(lane) - transform.position));
                HalfPipeView pipe = _pipes.Get();
                pipe.name = $"SeesawTrack_{level.Lane(lane).MarbleName}";
                pipe.transform.localPosition = Vector3.zero;
                pipe.transform.localRotation = Quaternion.identity;
                pipe.Build(_points, trackView.LaneTint(level, lane), false);
            }

            float rimHeight = pipePrefab.Radius * 2f;
            halfMarker.localPosition = new Vector3(0f, rimHeight + 0.02f, 0f);
            halfMarker.localScale = new Vector3(width, 0.04f, 0.08f);
            fulcrum.position = transform.position - Vector3.up * 0.6f;
            fulcrum.rotation = Quaternion.identity;

            _gateOpen = new Vector3(0f, -rimHeight - 0.1f, -length * 0.5f + 0.05f);
            _gateClosed = new Vector3(0f, rimHeight * 0.5f, -length * 0.5f + 0.05f);
            gate.localScale = new Vector3(width, rimHeight + 0.1f, 0.08f);
            ResetPose();
        }

        private void OnStateChanged()
        {
            if (controller.Phase == AttemptPhase.Setup)
            {
                ResetPose();
            }
        }

        private void ResetPose()
        {
            _gateBlend = 0f;
            _targetTilt = 0f;
            _tilt = 0f;
            gate.localPosition = _gateOpen;
        }

        private void OnSimEvent(SimEvent e)
        {
            if (e.Type == SimEventType.SeesawClosed)
            {
                _gateBlend = 0.001f;
            }
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            _targetTilt = record.Outcome.Success ? successTilt : failTilt;
        }

        private void Update()
        {
            MarbleSimulation sim = controller.Sim;
            bool open = sim.Started && !sim.SeesawClosed && !sim.Finished;

            if (_gateBlend > 0f)
            {
                _gateBlend = Mathf.MoveTowards(_gateBlend, 1f, Time.deltaTime * 8f);
            }
            gate.localPosition = Vector3.Lerp(_gateOpen, _gateClosed, _gateBlend);

            _tilt = Mathf.Lerp(_tilt, _targetTilt, Time.deltaTime * 4f);
            float shake = open
                ? (Mathf.PerlinNoise(Time.time * shakeFrequency, 0.3f) - 0.5f) * 2f * shakeDegrees
                : 0f;
            deck.localRotation = Quaternion.Euler(_tilt + shake, 0f, shake * 0.5f);
        }
    }
}
