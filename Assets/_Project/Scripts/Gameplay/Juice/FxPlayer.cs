using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// Visual juice driven by simulation events: splashes, sparks, steam, goo, confetti,
    /// squash-and-stretch on marbles, coating drips and the occasional camera punch.
    /// Purely cosmetic: it never feeds back into the deterministic simulation.
    /// </summary>
    public sealed class FxPlayer : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private TrackView trackView;
        [SerializeField] private MarbleSetView marbles;
        [SerializeField] private SeesawView seesaw;
        [SerializeField] private GameCamera gameCamera;

        [Header("Particle systems (world space, emitted by script)")]
        [SerializeField] private ParticleSystem droplets;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem confetti;

        [Header("Colours")]
        [SerializeField] private Color honeyDrip = new Color(1f, 0.7f, 0.15f, 0.9f);
        [SerializeField] private Color wetDrip = new Color(0.45f, 0.75f, 1f, 0.8f);
        [SerializeField] private Color fireColor = new Color(1f, 0.55f, 0.12f, 1f);
        [SerializeField] private Color steamColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color sootColor = new Color(0.2f, 0.2f, 0.22f, 0.6f);
        [SerializeField] private Color caramelColor = new Color(0.85f, 0.55f, 0.15f, 1f);

        private float[] _dripTimer = new float[0];
        private float[] _fireTimer = new float[0];

        private void OnEnable()
        {
            controller.LevelLoaded += OnLevelLoaded;
            controller.SimEventRaised += OnSimEvent;
            controller.AttemptFinished += OnAttemptFinished;
            controller.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            controller.LevelLoaded -= OnLevelLoaded;
            controller.SimEventRaised -= OnSimEvent;
            controller.AttemptFinished -= OnAttemptFinished;
            controller.StateChanged -= OnStateChanged;
        }

        private void OnLevelLoaded()
        {
            _dripTimer = new float[controller.Level.MarbleCount];
            _fireTimer = new float[controller.Level.MarbleCount];
            ClearAll();
        }

        private void OnStateChanged()
        {
            if (controller.Phase == AttemptPhase.Setup)
            {
                ClearAll();
            }
        }

        private void ClearAll()
        {
            droplets.Clear();
            sparks.Clear();
            smoke.Clear();
            confetti.Clear();
        }

        // ------------------------------------------------------------ events

        private void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Released:
                case SimEventType.Resumed:
                    marbles.Get(e.Marble).Punch(0.9f);
                    Burst(smoke, MarblePos(e.Marble), 6, steamColor, 0.6f, 0.25f, 0.6f, 0.4f);
                    break;
                case SimEventType.CoatingChanged:
                {
                    ElementView view = trackView.FindElement(e.Marble, e.Element);
                    view.Splash();
                    marbles.Get(e.Marble).Punch(0.5f);
                    Splash(view.transform.position, view.FxColor, 26, marbles.Get(e.Marble).Speed);
                    break;
                }
                case SimEventType.CurvePassed:
                {
                    MarbleView marble = marbles.Get(e.Marble);
                    marble.Punch(0.45f);
                    int count = Mathf.Clamp(Mathf.RoundToInt(marble.Speed * 1.5f), 3, 14);
                    Burst(sparks, trackView.FindElement(e.Marble, e.Element).transform.position + Vector3.up * 0.15f,
                        count, new Color(1f, 0.95f, 0.75f, 1f), 2.2f, 0.06f, 0.3f, 1.2f);
                    break;
                }
                case SimEventType.Ignited:
                    Burst(sparks, MarblePos(e.Marble), 36, fireColor, 3.5f, 0.1f, 0.5f, 1.5f);
                    Burst(smoke, MarblePos(e.Marble), 8, sootColor, 0.8f, 0.35f, 1f, 0.6f);
                    marbles.Get(e.Marble).Punch(0.8f);
                    break;
                case SimEventType.Extinguished:
                case SimEventType.Evaporated:
                    Burst(smoke, MarblePos(e.Marble), 20, steamColor, 1.4f, 0.35f, 1.1f, 1.2f);
                    Splash(MarblePos(e.Marble), wetDrip, 10, 2f);
                    break;
                case SimEventType.Melted:
                case SimEventType.MeltedAtFinish:
                {
                    Color goo = controller.Level.Lane(e.Marble).MarbleColor * 0.6f;
                    goo.a = 1f;
                    Splash(MarblePos(e.Marble), goo, 28, 1f);
                    Burst(smoke, MarblePos(e.Marble), 16, sootColor, 1f, 0.4f, 1.4f, 0.8f);
                    break;
                }
                case SimEventType.Caramelized:
                    Burst(sparks, MarblePos(e.Marble), 22, caramelColor, 2f, 0.08f, 0.6f, 1f);
                    Burst(smoke, MarblePos(e.Marble), 6, steamColor, 0.6f, 0.3f, 0.8f, 0.4f);
                    marbles.Get(e.Marble).Punch(1f);
                    break;
                case SimEventType.ButtonPressed:
                {
                    ElementView view = trackView.FindElement(e.Marble, e.Element);
                    Ring(view.transform.position + Vector3.up * 0.05f, view.FxColor, 18, 2.5f);
                    break;
                }
                case SimEventType.SensorPassed:
                {
                    ElementView view = trackView.FindElement(e.Marble, e.Element);
                    Burst(sparks, view.transform.position + Vector3.up * 0.7f, 10, Color.white, 1.5f, 0.06f, 0.35f, 1f);
                    break;
                }
                case SimEventType.EnteredSeesaw:
                    marbles.Get(e.Marble).Punch(0.6f);
                    break;
                case SimEventType.SeesawClosed:
                    gameCamera.Punch(0.12f);
                    for (int lane = 0; lane < controller.Level.MarbleCount; lane++)
                    {
                        Burst(smoke, seesaw.LanePoint(lane, 0f), 4, new Color(0.9f, 0.85f, 0.75f, 0.5f), 0.8f, 0.3f, 0.7f, 0.6f);
                    }
                    break;
                case SimEventType.Blocked:
                    marbles.Get(e.Marble).Punch(1f);
                    Burst(smoke, MarblePos(e.Marble), 10, new Color(0.9f, 0.85f, 0.75f, 0.5f), 0.9f, 0.3f, 0.8f, 0.6f);
                    break;
                case SimEventType.Arrived:
                {
                    marbles.Get(e.Marble).Punch(0.7f);
                    Color c = controller.Level.Lane(e.Marble).MarbleColor;
                    Burst(sparks, MarblePos(e.Marble), 10, c, 2f, 0.07f, 0.45f, 1f);
                    break;
                }
            }
        }

        private void OnAttemptFinished(AttemptRecord record)
        {
            LevelDefinition level = controller.Level;
            if (record.Outcome.Success)
            {
                gameCamera.Punch(0.18f);
                for (int lane = 0; lane < level.MarbleCount; lane++)
                {
                    Vector3 origin = seesaw.LanePoint(lane, 1f) + Vector3.up * 0.3f;
                    Color c = level.Lane(lane).MarbleColor;
                    for (int k = 0; k < 40; k++)
                    {
                        Vector3 v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 8f), Random.Range(-2.5f, 2.5f));
                        Color cc = k % 3 == 0 ? Color.white : c;
                        Emit(confetti, origin, v, cc, Random.Range(0.08f, 0.16f), Random.Range(1.6f, 2.6f));
                    }
                }
            }
            else
            {
                for (int lane = 0; lane < level.MarbleCount; lane++)
                {
                    Burst(smoke, seesaw.LanePoint(lane, 0.5f), 5, sootColor, 0.7f, 0.35f, 1.2f, 0.5f);
                }
            }
        }

        // ------------------------------------------------------------ continuous effects

        private void Update()
        {
            AttemptPhase phase = controller.Phase;
            if (phase == AttemptPhase.Setup)
            {
                return;
            }

            MarbleSimulation sim = controller.Sim;
            float dt = Time.deltaTime;
            for (int i = 0; i < sim.MarbleCount; i++)
            {
                MarbleState m = sim.GetMarble(i);
                bool moving = m.Status == MarbleStatus.Rolling || m.Status == MarbleStatus.OnSeesaw;
                Vector3 pos = MarblePos(i);

                if (m.Condition == Condition.Burning && m.Status != MarbleStatus.Melted)
                {
                    _fireTimer[i] -= dt;
                    while (_fireTimer[i] <= 0f)
                    {
                        _fireTimer[i] += 1f / 45f;
                        Vector3 v = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1f, 2.2f), Random.Range(-0.4f, 0.4f));
                        Emit(sparks, pos + Random.insideUnitSphere * 0.15f, v, Color.Lerp(fireColor, Color.yellow, Random.value * 0.5f),
                            Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.45f));
                    }
                }

                float dripRate = 0f;
                Color dripColor = Color.clear;
                if (m.Condition != Condition.Burning)
                {
                    switch (m.Coating)
                    {
                        case Coating.Honey: dripRate = 7f; dripColor = honeyDrip; break;
                        case Coating.Wet: dripRate = 9f; dripColor = wetDrip; break;
                        case Coating.Oil: dripRate = 4f; dripColor = new Color(1f, 1f, 0.9f, 0.9f); break;
                    }
                }
                if (m.Condition == Condition.Caramelized)
                {
                    dripRate = 2f;
                    dripColor = caramelColor;
                }
                if (dripRate <= 0f || !(moving || m.Status == MarbleStatus.Caramelized))
                {
                    continue;
                }

                _dripTimer[i] -= dt;
                while (_dripTimer[i] <= 0f)
                {
                    _dripTimer[i] += 1f / dripRate;
                    bool glint = m.Coating == Coating.Oil;
                    ParticleSystem ps = glint ? sparks : droplets;
                    Vector3 v = glint ? Vector3.up * 0.3f : new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.8f), Random.Range(-0.3f, 0.3f));
                    Emit(ps, pos + Random.insideUnitSphere * 0.2f, v, dripColor, glint ? 0.07f : Random.Range(0.05f, 0.09f), glint ? 0.3f : 0.6f);
                }
            }
        }

        // ------------------------------------------------------------ helpers

        private Vector3 MarblePos(int marble) => marbles.Get(marble).transform.position;

        private void Splash(Vector3 origin, Color color, int count, float marbleSpeed)
        {
            float energy = Mathf.Clamp(0.8f + marbleSpeed * 0.15f, 0.8f, 2.2f);
            for (int k = 0; k < count; k++)
            {
                Vector2 h = Random.insideUnitCircle * 1.3f * energy;
                Vector3 v = new Vector3(h.x, Random.Range(1.8f, 3.6f) * energy, h.y);
                Emit(droplets, origin + Vector3.up * 0.1f, v, color, Random.Range(0.09f, 0.18f), Random.Range(0.5f, 0.9f));
            }
        }

        private void Ring(Vector3 origin, Color color, int count, float speed)
        {
            for (int k = 0; k < count; k++)
            {
                float a = k / (float)count * Mathf.PI * 2f;
                Vector3 v = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)) * speed;
                Emit(sparks, origin, v, color, 0.1f, 0.35f);
            }
        }

        private void Burst(ParticleSystem ps, Vector3 origin, int count, Color color, float speed, float size, float life, float spread)
        {
            for (int k = 0; k < count; k++)
            {
                Vector3 v = Random.insideUnitSphere * spread + Vector3.up * speed * Random.Range(0.5f, 1f);
                Emit(ps, origin + Random.insideUnitSphere * 0.1f, v, color, size * Random.Range(0.7f, 1.3f), life * Random.Range(0.7f, 1.3f));
            }
        }

        private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, Color color, float size, float lifetime)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = lifetime,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false
            };
            ps.Emit(p, 1);
        }
    }
}
