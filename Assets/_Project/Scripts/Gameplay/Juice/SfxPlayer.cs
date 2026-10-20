using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Placeholder audio synthesised at startup (no audio assets yet). Reacts to simulation events.</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private AttemptController controller;
        [SerializeField] private AudioSource oneShots;
        [SerializeField] private AudioSource creakLoop;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;

        private AudioClip _click;
        private AudioClip _drum;
        private AudioClip _cymbal;
        private AudioClip _creak;
        private AudioClip _splash;
        private AudioClip _whoosh;
        private AudioClip _melt;
        private AudioClip _clank;
        private AudioClip _fail;
        private AudioClip _blip;
        private System.Random _noise;

        private void Awake()
        {
            _noise = new System.Random(1234);
            _click = Make("click", 0.05f, (t, d) => Mathf.Sin(t * 2f * Mathf.PI * 900f) * Mathf.Exp(-t * 80f));
            _drum = Make("drum", 0.12f, (t, d) => Noise() * Mathf.Exp(-t * 35f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 180f) * Mathf.Exp(-t * 30f) * 0.4f);
            _cymbal = Make("cymbal", 1.8f, (t, d) => Noise() * Mathf.Exp(-t * 2.2f) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 5200f)));
            _creak = Make("creak", 0.6f, (t, d) => Saw(t * (110f + Mathf.Sin(t * 9f) * 25f)) * 0.25f * Mathf.Sin(Mathf.PI * t / d));
            _splash = Make("splash", 0.25f, (t, d) => Noise() * Mathf.Exp(-t * 14f) * 0.5f);
            _whoosh = Make("whoosh", 0.4f, (t, d) => Noise() * Mathf.Sin(Mathf.PI * t / d) * 0.6f);
            _melt = Make("melt", 0.7f, (t, d) => Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(500f, 90f, t / d)) * (1f - t / d) * 0.5f);
            _clank = Make("clank", 0.3f, (t, d) => (Mathf.Sin(t * 2f * Mathf.PI * 320f) + Mathf.Sin(t * 2f * Mathf.PI * 515f)) * Mathf.Exp(-t * 18f) * 0.4f);
            _fail = Make("fail", 0.8f, (t, d) => Saw(t * Mathf.Lerp(220f, 110f, t / d)) * (1f - t / d) * 0.35f);
            _blip = Make("blip", 0.08f, (t, d) => Mathf.Sin(t * 2f * Mathf.PI * 1400f) * Mathf.Exp(-t * 50f) * 0.5f);

            creakLoop.clip = _creak;
            creakLoop.loop = true;
            creakLoop.volume = 0f;
            creakLoop.Play();
        }

        private void OnEnable()
        {
            controller.SimEventRaised += OnSimEvent;
            controller.AttemptFinished += OnAttemptFinished;
        }

        private void OnDisable()
        {
            controller.SimEventRaised -= OnSimEvent;
            controller.AttemptFinished -= OnAttemptFinished;
        }

        /// <summary>Single drum stroke; the tension director calls this faster as the finish gets closer.</summary>
        public void DrumHit(float volume)
        {
            oneShots.pitch = 0.9f + (float)_noise.NextDouble() * 0.2f;
            oneShots.PlayOneShot(_drum, volume * masterVolume);
            oneShots.pitch = 1f;
        }

        private void Update()
        {
            MarbleSimulation sim = controller.Sim;
            bool open = sim.Started && !sim.SeesawClosed && !sim.Finished;
            creakLoop.volume = Mathf.MoveTowards(creakLoop.volume, open ? 0.25f * masterVolume : 0f, Time.unscaledDeltaTime * 2f);
        }

        private void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Released:
                case SimEventType.Resumed:
                    Play(_click, 0.8f);
                    break;
                case SimEventType.CoatingChanged:
                    Play(_splash, 0.6f);
                    break;
                case SimEventType.Extinguished:
                case SimEventType.Evaporated:
                    Play(_splash, 0.9f);
                    break;
                case SimEventType.Ignited:
                    Play(_whoosh, 0.9f);
                    break;
                case SimEventType.Melted:
                case SimEventType.MeltedAtFinish:
                case SimEventType.Caramelized:
                    Play(_melt, 0.8f);
                    break;
                case SimEventType.ButtonPressed:
                case SimEventType.SensorPassed:
                    Play(_blip, 0.7f);
                    break;
                case SimEventType.LogicToggled:
                case SimEventType.SeesawClosed:
                case SimEventType.Blocked:
                    Play(_clank, 0.8f);
                    break;
            }
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            Play(record.Outcome.Success ? _cymbal : _fail, 1f);
        }

        private void Play(AudioClip clip, float volume)
        {
            oneShots.PlayOneShot(clip, volume * masterVolume);
        }

        private float Noise() => (float)(_noise.NextDouble() * 2.0 - 1.0);

        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

        private AudioClip Make(string clipName, float seconds, System.Func<float, float, float> wave)
        {
            int samples = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(wave(t, seconds), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
