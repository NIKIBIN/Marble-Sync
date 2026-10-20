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
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float ghostAlpha = 0.35f;

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

        public float Radius => radius;

        public void Configure(int lane, Color color, bool ghost)
        {
            _block ??= new MaterialPropertyBlock();
            _ghost = ghost;
            _baseColor = ghost ? new Color(color.r, color.g, color.b, ghostAlpha) : color;
            clickTarget.BindMarble(lane);
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

        public void Place(Vector3 surfacePoint, bool snap)
        {
            Vector3 position = surfacePoint + Vector3.up * radius;
            Vector3 delta = position - _lastPosition;
            transform.position = position;
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
            body.localScale = Vector3.Scale(Vector3.one * (radius * 2f), new Vector3(1f + _meltBlend * 0.6f, 1f - _meltBlend * 0.8f, 1f + _meltBlend * 0.6f));
            ApplyBodyColor(Color.Lerp(_baseColor, new Color(0.15f, 0.15f, 0.15f, _baseColor.a), _meltBlend));

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
