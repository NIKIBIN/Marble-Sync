using System;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>One track element. A single pooled prefab carries a child visual per element kind.</summary>
    public sealed class ElementView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ClickTarget clickTarget;
        [SerializeField] private Collider clickCollider;

        [Header("Visual per kind")]
        [SerializeField] private Renderer puddle;
        [SerializeField] private GameObject flameRoot;
        [SerializeField] private Renderer flameNozzle;
        [SerializeField] private Transform flameJet;
        [SerializeField] private Light flameLight;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Renderer doorRenderer;
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private Renderer sensorRenderer;
        [SerializeField] private TextMesh label;
        [Tooltip("Labels face the fixed gameplay camera angle.")]
        [SerializeField] private Vector3 labelEuler = new Vector3(42f, -18f, 0f);

        [Header("Colours")]
        [SerializeField] private Color honeyColor = new Color(0.95f, 0.65f, 0.1f);
        [SerializeField] private Color oilColor = new Color(0.12f, 0.1f, 0.06f);
        [SerializeField] private Color waterColor = new Color(0.3f, 0.65f, 1f);
        [SerializeField] private Color[] logicColors =
        {
            new Color(0.2f, 0.45f, 1f),
            new Color(1f, 0.25f, 0.2f),
            new Color(0.25f, 0.85f, 0.35f),
            new Color(1f, 0.85f, 0.2f)
        };

        private MaterialPropertyBlock _block;
        private ElementDef _def;
        private float _doorOpenBlend;
        private float _pulse;

        public int Lane { get; private set; }
        public int Index { get; private set; }
        public ElementDef Def => _def;

        public void Configure(int lane, int index, ElementDef def, Vector3 position, Vector3 direction, bool showLabel)
        {
            _block ??= new MaterialPropertyBlock();
            Lane = lane;
            Index = index;
            _def = def;
            _pulse = 0f;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // lies on the slope
            label.transform.rotation = Quaternion.Euler(labelEuler);

            puddle.gameObject.SetActive(def.IsPuddle);
            flameRoot.SetActive(def.Kind == ElementKind.Flame);
            doorPanel.gameObject.SetActive(def.Kind == ElementKind.Door);
            buttonRenderer.gameObject.SetActive(def.Kind == ElementKind.Button);
            sensorRenderer.gameObject.SetActive(def.Kind == ElementKind.Sensor);
            // Square curves need no marker: the half-pipe corner shows them.
            label.gameObject.SetActive(showLabel && (def.Kind == ElementKind.Sensor || def.Kind == ElementKind.Button));

            clickTarget.BindElement(lane, index);
            clickCollider.enabled = def.Kind != ElementKind.Curve;

            switch (def.Kind)
            {
                case ElementKind.Honey: Tint(puddle, honeyColor, 0f); break;
                case ElementKind.Oil: Tint(puddle, oilColor, 0f); break;
                case ElementKind.Water: Tint(puddle, waterColor, 0.2f); break;
                case ElementKind.Flame: Tint(flameNozzle, GetLogicColor(def.Color), 0.3f); break;
                case ElementKind.Door: Tint(doorRenderer, GetLogicColor(def.Color), 0.4f); break;
                case ElementKind.Button: Tint(buttonRenderer, GetLogicColor(def.Color), 0.6f); break;
                case ElementKind.Sensor: Tint(sensorRenderer, new Color(0.9f, 0.9f, 0.95f), 0.5f); break;
                case ElementKind.Curve: break;
                default: throw new ArgumentOutOfRangeException(nameof(def), def.Kind, null);
            }

            if (def.Kind == ElementKind.Sensor)
            {
                label.text = "S" + def.Id;
            }
            else if (def.Kind == ElementKind.Button)
            {
                label.text = "B" + def.Id;
            }
        }

        public void Pulse()
        {
            _pulse = 1f;
        }

        public void Show(MarbleSimulation sim)
        {
            bool removed = _def.IsPuddle && sim.IsElementRemoved(Lane, Index);
            if (_def.IsPuddle)
            {
                puddle.gameObject.SetActive(!removed);
                clickCollider.enabled = !removed;
            }

            if (_def.Kind == ElementKind.Door)
            {
                bool closed = sim.IsLogicActive(_def);
                _doorOpenBlend = Mathf.MoveTowards(_doorOpenBlend, closed ? 0f : 1f, Time.deltaTime * 6f);
                doorPanel.localPosition = new Vector3(0f, 0.35f - _doorOpenBlend * 0.65f, 0f);
            }

            if (_def.Kind == ElementKind.Flame)
            {
                bool lit = sim.IsLogicActive(_def);
                flameJet.gameObject.SetActive(lit);
                flameLight.enabled = lit;
                if (lit)
                {
                    float flicker = 0.85f + Mathf.PerlinNoise(Time.time * 10f, Index) * 0.3f;
                    flameJet.localScale = new Vector3(0.25f, 0.45f * flicker, 0.25f);
                    flameLight.intensity = 3f * flicker;
                }
            }

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 3f);
            float scale = 1f + _pulse * 0.4f;
            buttonRenderer.transform.localScale = new Vector3(0.26f * scale, 0.03f, 0.26f * scale);
            sensorRenderer.transform.localScale = new Vector3(0.95f * scale, 0.06f, 0.06f);
        }

        private Color GetLogicColor(LogicColor color) => logicColors[(int)color];

        private void Tint(Renderer target, Color color, float emission)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * emission);
            target.SetPropertyBlock(_block);
        }
    }
}
