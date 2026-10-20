using System;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// Placeholder audio synthesised at startup (no audio assets yet). Reacts to simulation events.
    /// Every one-shot gets a slightly random pitch from a small voice pool so repeats never sound canned.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int VoiceCount = 10;

        [SerializeField] private AttemptController controller;
        [SerializeField] private AudioSource oneShots;
        [SerializeField] private AudioSource creakLoop;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.08f;

        private AudioClip _pop;
        private AudioClip _drum;
        private AudioClip _cymbal;
        private AudioClip _creak;
        private AudioClip _honey;
        private AudioClip _oil;
        private AudioClip _water;
        private AudioClip _steam;
        private AudioClip _whoosh;
        private AudioClip _melt;
        private AudioClip _crackle;
        private AudioClip _clank;
        private AudioClip _fail;
        private AudioClip _blip;
        private AudioClip _tok;
        private AudioClip _clink;
        private AudioClip _chime;
        private AudioClip _roll;
        private AudioSource[] _voices;
        private int _nextVoice;
        private float _pitchBias = 1f;
        private System.Random _noise;

        /// <summary>Seamless rolling loop; each marble plays it with volume and pitch following its speed.</summary>
        public AudioClip RollClip => _roll;

        private void Awake()
        {
            _noise = new System.Random(1234);
            _voices = new AudioSource[VoiceCount];
            _voices[0] = oneShots;
            for (int i = 1; i < VoiceCount; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                _voices[i] = voice;
            }

            _pop = Make("pop", 0.09f, (t, d) => Mathf.Sin(Phase(t, 300f, 950f, d)) * Mathf.Exp(-t * 45f));
            _drum = Make("drum", 0.12f, (t, d) => Noise() * Mathf.Exp(-t * 35f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 180f) * Mathf.Exp(-t * 30f) * 0.4f);
            _cymbal = Make("cymbal", 1.8f, (t, d) => Noise() * Mathf.Exp(-t * 2.2f) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 5200f)));
            _creak = Make("creak", 0.6f, (t, d) => Saw(t * (110f + Mathf.Sin(t * 9f) * 25f)) * 0.25f * Mathf.Sin(Mathf.PI * t / d));
            // Honey: a thick, low "blub" with a bubbly wobble.
            _honey = Make("honey", 0.4f, (t, d) => Mathf.Sin(Phase(t, 240f, 110f, d)) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 16f)) * Mathf.Exp(-t * 7f) * 0.8f);
            // Oil: a slick rising "shlick".
            float oilLow = 0f;
            _oil = Make("oil", 0.28f, (t, d) =>
            {
                oilLow += (Noise() - oilLow) * Mathf.Lerp(0.05f, 0.6f, t / d);
                return oilLow * Mathf.Sin(Mathf.PI * t / d) * 1.4f;
            });
            // Water: a splash burst with a few bright droplets on top.
            _water = Make("water", 0.45f, (t, d) =>
            {
                float splash = Noise() * Mathf.Exp(-t * 11f) * 0.6f;
                float drops = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float start = 0.05f + k * 0.07f;
                    if (t > start)
                    {
                        float tt = t - start;
                        drops += Mathf.Sin(tt * 2f * Mathf.PI * (1300f + k * 260f)) * Mathf.Exp(-tt * 60f) * 0.25f;
                    }
                }
                return splash + drops;
            });
            float steamPrev = 0f;
            _steam = Make("steam", 0.7f, (t, d) =>
            {
                float n = Noise();
                float high = n - steamPrev;
                steamPrev = n;
                return high * 0.35f * Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.4f));
            });
            _whoosh = Make("whoosh", 0.45f, (t, d) => Noise() * Mathf.Sin(Mathf.PI * t / d) * 0.6f);
            _melt = Make("melt", 0.7f, (t, d) => Mathf.Sin(Phase(t, 500f, 90f, d)) * (1f - t / d) * 0.5f);
            _crackle = Make("crackle", 0.6f, (t, d) => (_noise.NextDouble() < 0.004 ? 1f : 0f) * (1f - t / d) + Noise() * 0.05f * (1f - t / d));
            _clank = Make("clank", 0.3f, (t, d) => (Mathf.Sin(t * 2f * Mathf.PI * 320f) + Mathf.Sin(t * 2f * Mathf.PI * 515f)) * Mathf.Exp(-t * 18f) * 0.4f);
            _fail = Make("fail", 0.8f, (t, d) => Saw(t * Mathf.Lerp(220f, 110f, t / d)) * (1f - t / d) * 0.35f);
            _blip = Make("blip", 0.08f, (t, d) => Mathf.Sin(t * 2f * Mathf.PI * 1400f) * Mathf.Exp(-t * 50f) * 0.5f);
            _tok = Make("tok", 0.06f, (t, d) => (Mathf.Sin(t * 2f * Mathf.PI * 620f) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * 70f));
            _clink = Make("clink", 0.35f, (t, d) => (Mathf.Sin(t * 2f * Mathf.PI * 1568f) + 0.6f * Mathf.Sin(t * 2f * Mathf.PI * 2349f)) * Mathf.Exp(-t * 14f) * 0.35f);
            // Success: a bright C-major arpeggio.
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            _chime = Make("chime", 1.4f, (t, d) =>
            {
                float sum = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float start = k * 0.08f;
                    if (t > start)
                    {
                        float tt = t - start;
                        sum += (Mathf.Sin(tt * 2f * Mathf.PI * notes[k]) + 0.3f * Mathf.Sin(tt * 4f * Mathf.PI * notes[k])) * Mathf.Exp(-tt * 3.5f);
                    }
                }
                return sum * 0.22f;
            });
            _roll = MakeRollLoop();

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
            Play(_drum, volume, 0.12f);
        }

        private void Update()
        {
            MarbleSimulation sim = controller.Sim;
            bool open = sim.Started && !sim.SeesawClosed && !sim.Finished;
            creakLoop.volume = Mathf.MoveTowards(creakLoop.volume, open ? 0.18f * masterVolume : 0f, Time.unscaledDeltaTime * 2f);
        }

        private void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Released:
                case SimEventType.Resumed:
                    Play(_pop, 0.8f);
                    break;
                case SimEventType.CoatingChanged:
                    PlayLiquid(e);
                    break;
                case SimEventType.Extinguished:
                case SimEventType.Evaporated:
                    Play(_steam, 0.9f);
                    Play(_water, 0.5f);
                    break;
                case SimEventType.Ignited:
                    Play(_whoosh, 1f);
                    Play(_crackle, 0.6f);
                    break;
                case SimEventType.Melted:
                case SimEventType.MeltedAtFinish:
                    Play(_melt, 0.9f);
                    Play(_steam, 0.5f);
                    break;
                case SimEventType.Caramelized:
                    Play(_crackle, 0.9f);
                    Play(_melt, 0.5f);
                    break;
                case SimEventType.CurvePassed:
                    // Faster marbles hit the corner harder: louder and higher.
                    float speed = controller.Sim.GetMarble(e.Marble).Speed / 0.3f;
                    _pitchBias = Mathf.Clamp(0.8f + speed * 0.06f, 0.8f, 1.5f);
                    Play(_tok, Mathf.Clamp01(0.35f + speed * 0.08f));
                    _pitchBias = 1f;
                    break;
                case SimEventType.ButtonPressed:
                case SimEventType.SensorPassed:
                    Play(_blip, 0.7f);
                    break;
                case SimEventType.LogicToggled:
                case SimEventType.Blocked:
                    Play(_clank, 0.8f);
                    break;
                case SimEventType.SeesawClosed:
                    Play(_clank, 1f, 0.02f);
                    break;
                case SimEventType.EnteredSeesaw:
                    Play(_tok, 0.6f);
                    break;
                case SimEventType.Arrived:
                    Play(_clink, 0.7f, 0.15f);
                    break;
            }
        }


        private void PlayLiquid(SimEvent e)
        {
            ElementKind kind = controller.Level.SimDef.Tracks[e.Marble].Elements[e.Element].Kind;
            switch (kind)
            {
                case ElementKind.Honey: Play(_honey, 1f); break;
                case ElementKind.Oil: Play(_oil, 0.9f); break;
                case ElementKind.Water: Play(_water, 0.9f); break;
                default: throw new ArgumentOutOfRangeException(nameof(e), kind, "Coating change from a non-liquid element.");
            }
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            if (record.Outcome.Success)
            {
                Play(_cymbal, 0.9f, 0f);
                Play(_chime, 0.9f, 0f);
            }
            else
            {
                Play(_fail, 1f, 0f);
            }
        }

        private void Play(AudioClip clip, float volume) => Play(clip, volume, pitchJitter);

        private void Play(AudioClip clip, float volume, float jitter)
        {
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            float slowMo = Mathf.Max(0.5f, Time.timeScale);
            voice.pitch = (1f + ((float)_noise.NextDouble() * 2f - 1f) * jitter) * _pitchBias * slowMo;
            voice.PlayOneShot(clip, volume * masterVolume);
        }

        private float Noise() => (float)(_noise.NextDouble() * 2.0 - 1.0);

        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

        /// <summary>Phase of a sine whose frequency glides linearly from f0 to f1 over the clip.</summary>
        private static float Phase(float t, float f0, float f1, float duration)
        {
            float k = (f1 - f0) / duration;
            return 2f * Mathf.PI * (f0 * t + 0.5f * k * t * t);
        }

        /// <summary>Low rumbling noise, cross-faded at the seam so it loops without clicks.</summary>
        private AudioClip MakeRollLoop()
        {
            const float seconds = 1.2f;
            const float fade = 0.15f;
            int samples = Mathf.CeilToInt(seconds * SampleRate);
            int fadeSamples = Mathf.CeilToInt(fade * SampleRate);
            var raw = new float[samples + fadeSamples];
            float low = 0f;
            float lower = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                low += (Noise() - low) * 0.08f;
                lower += (low - lower) * 0.08f;
                float t = i / (float)SampleRate;
                float rumble = Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.15f;
                raw[i] = (lower * 3.5f + low * 0.6f) * (0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 7f)) + rumble * (0.6f + 0.4f * low);
            }
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                data[i] = raw[i];
            }
            for (int i = 0; i < fadeSamples; i++)
            {
                float w = i / (float)fadeSamples;
                data[i] = raw[samples + i] * (1f - w) + raw[i] * w;
            }
            for (int i = 0; i < samples; i++)
            {
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("roll", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip Make(string clipName, float seconds, Func<float, float, float> wave)
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
