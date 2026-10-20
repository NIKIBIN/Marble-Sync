using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Visual for one marble: body colour, coating shell, fire, melt and selection ring.</summary>
    public sealed class MarbleView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform body;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Renderer shellRenderer;
        [SerializeField] private GameObject fireRoot;
        [SerializeField] private Light fireLight;
        [SerializeField] private GameObject selectionRing;
        [SerializeField] private ClickTarget clickTarget;
        [SerializeField] private Collider clickCollider;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private AudioSource rollSource;
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float ghostAlpha = 0.35f;
        [SerializeField] private float trailMinSpeed = 3.5f;
        [SerializeField, Range(0f, 1f)] private float rollVolume = 0.3f;

        [Header("Coating colours")]
        [SerializeField] private Color honeyColor = new Color(0.95f, 0.65f, 0.1f, 0.7f);
        [SerializeField] private Color oilColor = new Color(0.15f, 0.12f, 0.05f, 0.55f);
        [SerializeField] private Color wetColor = new Color(0.4f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color caramelColor = new Color(0.55f, 0.3f, 0.08f, 0.9f);

        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private bool _ghost;
        private Vector3 _lastPosition;
        private float _meltBlend;
        private float _speed;
        private float _punch;
        private float _punchTime;
        private int _lane;

        public float Radius => radius;

        public void Configure(int lane, Color color, bool ghost, AudioClip rollClip)
        {
            _block ??= new MaterialPropertyBlock();
            _ghost = ghost;
            _baseColor = ghost ? new Color(color.r, color.g, color.b, ghostAlpha) : color;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(ghost ? 0.15f : 0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.emitting = false;
            trail.Clear();
            _speed = 0f;
            _punch = 0f;

            rollSource.clip = rollClip;
            rollSource.loop = true;
            rollSource.volume = 0f;
            if (ghost)
            {
                rollSource.Stop();
            }
            else
            {
                rollSource.time = Random.value * rollClip.length; // de-phase the loops between marbles
                rollSource.Play();
            }
            clickTarget.BindMarble(lane);
            _lane = lane;
            clickCollider.enabled = !ghost;
            selectionRing.SetActive(false);
            fireRoot.SetActive(false);
            shellRenderer.enabled = false;
            body.localScale = Vector3.one * (radius * 2f);
            _meltBlend = 0f;
            _lastPosition = transform.position;
            ApplyBodyColor(_baseColor);
        }

        public void SetSelected(bool selected)
        {
            selectionRing.SetActive(selected);
        }

        /// <summary>Kick off a squash-and-stretch wobble (0..1).</summary>
        public void Punch(float amount)
        {
            _punch = Mathf.Max(_punch, amount);
            _punchTime = 0f;
        }

        public float Speed => _speed;

        public void Place(Vector3 surfacePoint, bool snap)
        {
            Vector3 position = surfacePoint + Vector3.up * radius;
            Vector3 delta = position - _lastPosition;
            transform.position = position;
            if (snap)
            {
                trail.Clear();
                _speed = 0f;
            }
            else if (Time.deltaTime > 0f)
            {
                _speed = Mathf.Lerp(_speed, delta.magnitude / Time.deltaTime, 0.25f);
            }
            if (!snap && delta.sqrMagnitude > 1e-8f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, delta.normalized);
                float angle = delta.magnitude / radius * Mathf.Rad2Deg;
                body.Rotate(axis, angle, Space.World);
            }
            _lastPosition = position;
        }

        public void ShowState(MarbleState state)
        {
            bool melted = state.Status == MarbleStatus.Melted || state.MeltedAtFinish;
            _meltBlend = Mathf.MoveTowards(_meltBlend, melted ? 1f : 0f, Time.deltaTime * 3f);

            // Squash & stretch: a damped wobble after releases, curves and landings.
            _punch = Mathf.MoveTowards(_punch, 0f, Time.deltaTime * 4f);
            _punchTime += Time.deltaTime;
            float wobble = _punch * Mathf.Sin(_punchTime * 32f);
            // Waiting marbles breathe gently, inviting the click.
            if (state.Status == MarbleStatus.Waiting && !_ghost)
            {
                wobble += Mathf.Sin(Time.time * 3f + _lane * 1.3f) * 0.08f;
            }
            var squash = new Vector3(1f + wobble * 0.25f, 1f - wobble * 0.35f, 1f + wobble * 0.25f);
            var melt = new Vector3(1f + _meltBlend * 0.6f, 1f - _meltBlend * 0.8f, 1f + _meltBlend * 0.6f);
            body.localScale = Vector3.Scale(Vector3.one * (radius * 2f), Vector3.Scale(melt, squash));
            ApplyBodyColor(Color.Lerp(_baseColor, new Color(0.15f, 0.15f, 0.15f, _baseColor.a), _meltBlend));

            bool moving = (state.Status == MarbleStatus.Rolling || state.Status == MarbleStatus.OnSeesaw) && !melted;
            trail.emitting = moving && _speed > trailMinSpeed;
            if (!_ghost)
            {
                float target = moving ? Mathf.Clamp01(_speed / 9f) * rollVolume : 0f;
                rollSource.volume = Mathf.MoveTowards(rollSource.volume, target, Time.deltaTime * 3f);
                rollSource.pitch = (0.75f + Mathf.Clamp(_speed, 0f, 12f) * 0.06f) * Mathf.Max(0.4f, Time.timeScale);
            }

            bool burning = state.Condition == Condition.Burning && !melted;
            fireRoot.SetActive(burning);
            if (burning)
            {
                fireLight.intensity = 2f + Mathf.PerlinNoise(Time.time * 12f, 0f) * 2f;
            }

            Color shell;
            bool showShell = true;
            if (state.Condition == Condition.Caramelized)
            {
                shell = caramelColor;
            }
            else
            {
                switch (state.Coating)
                {
                    case Coating.Honey: shell = honeyColor; break;
                    case Coating.Oil: shell = oilColor; break;
                    case Coating.Wet: shell = wetColor; break;
                    default:
                        shell = Color.clear;
                        showShell = false;
                        break;
                }
            }

            shellRenderer.enabled = showShell && !melted;
            if (showShell)
            {
                if (_ghost)
                {
                    shell.a *= ghostAlpha;
                }
                shellRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, shell);
                shellRenderer.SetPropertyBlock(_block);
            }
        }

        private void ApplyBodyColor(Color color)
        {
            bodyRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 0.15f);
            bodyRenderer.SetPropertyBlock(_block);
        }
    }
}
