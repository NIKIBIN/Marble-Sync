using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>The shared final ramp: trembles while open, shuts its gate at 50%, balances or flips at the end.</summary>
    public sealed class SeesawView : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private Transform plank;
        [SerializeField] private Transform gate;
        [SerializeField] private Transform halfMarker;
        [SerializeField] private Transform fulcrum;
        [SerializeField] private float plankWidth = 1.4f;
        [SerializeField] private float plankThickness = 0.15f;
        [SerializeField] private float shakeDegrees = 0.8f;
        [SerializeField] private float shakeFrequency = 18f;
        [SerializeField] private float successTilt = 6f;
        [SerializeField] private float failTilt = -18f;

        private Quaternion _baseRotation;
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
            LevelDefinition level = controller.Level;
            Vector3 start = level.SeesawStart;
            Vector3 end = level.SeesawEnd;
            Vector3 dir = (end - start).normalized;
            float length = Vector3.Distance(start, end);

            transform.position = (start + end) * 0.5f;
            _baseRotation = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = _baseRotation;

            plank.localPosition = new Vector3(0f, -plankThickness * 0.5f, 0f);
            plank.localScale = new Vector3(plankWidth, plankThickness, length);
            halfMarker.localPosition = new Vector3(0f, 0.01f, 0f);
            halfMarker.localScale = new Vector3(plankWidth, 0.02f, 0.06f);
            fulcrum.position = transform.position - Vector3.up * 0.6f;
            fulcrum.rotation = Quaternion.identity;

            _gateOpen = new Vector3(0f, -0.6f, -length * 0.5f + 0.05f);
            _gateClosed = new Vector3(0f, 0.3f, -length * 0.5f + 0.05f);
            gate.localScale = new Vector3(plankWidth, 0.6f, 0.08f);
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
            plank.localRotation = Quaternion.Euler(_tilt + shake, 0f, shake * 0.5f);
        }
    }
}
