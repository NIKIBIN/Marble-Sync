using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Greybox geometry of every lane plus its elements. Built from pools on each level load.</summary>
    public sealed class TrackView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer trackPiecePrefab;
        [SerializeField] private ElementView elementPrefab;
        [SerializeField] private Transform pieceContainer;
        [SerializeField] private Transform elementContainer;
        [SerializeField] private float trackWidth = 0.6f;
        [SerializeField] private float trackThickness = 0.12f;
        [SerializeField] private float laneTint = 0.25f;
        [SerializeField] private Color trackColor = new Color(0.78f, 0.74f, 0.68f);

        private ComponentPool<Renderer> _pieces;
        private ComponentPool<ElementView> _elements;
        private MaterialPropertyBlock _block;
        private Bounds _bounds;

        public Bounds LevelBounds => _bounds;

        public void Build(LevelDefinition level)
        {
            _pieces ??= new ComponentPool<Renderer>(trackPiecePrefab, pieceContainer, 64);
            _elements ??= new ComponentPool<ElementView>(elementPrefab, elementContainer, 32);
            _block ??= new MaterialPropertyBlock();
            _pieces.ReleaseAll();
            _elements.ReleaseAll();

            _bounds = new Bounds(level.SeesawStart, Vector3.one);
            _bounds.Encapsulate(level.SeesawEnd);

            LevelSimDef sim = level.SimDef;
            for (int lane = 0; lane < level.MarbleCount; lane++)
            {
                TrackPath path = level.Path(lane);
                Color tint = Color.Lerp(trackColor, level.Lane(lane).MarbleColor, laneTint);

                for (int p = 0; p < path.PointCount - 1; p++)
                {
                    Vector3 a = path.Point(p);
                    Vector3 b = path.Point(p + 1);
                    AddPiece(a, b, tint);
                    _bounds.Encapsulate(a);
                    _bounds.Encapsulate(b);
                }

                ElementDef[] defs = sim.Tracks[lane].Elements;
                for (int e = 0; e < defs.Length; e++)
                {
                    ElementView view = _elements.Get();
                    view.name = $"{defs[e].Kind}_{lane}_{e}";
                    view.Configure(lane, e, defs[e], path.Evaluate(defs[e].Distance), path.Direction(defs[e].Distance));
                }
            }
        }

        public ElementView FindElement(int lane, int index)
        {
            var active = _elements.Active;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Lane == lane && active[i].Index == index)
                {
                    return active[i];
                }
            }
            throw new System.ArgumentException($"No element view for lane {lane} element {index}.");
        }

        public void Show(MarbleSimulation sim)
        {
            var active = _elements.Active;
            for (int i = 0; i < active.Count; i++)
            {
                active[i].Show(sim);
            }
        }

        private void AddPiece(Vector3 a, Vector3 b, Color tint)
        {
            Renderer piece = _pieces.Get();
            Vector3 dir = b - a;
            float length = dir.magnitude;
            Transform t = piece.transform;
            t.position = (a + b) * 0.5f - Vector3.up * (trackThickness * 0.5f);
            t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            t.localScale = new Vector3(trackWidth, trackThickness, length + trackWidth * 0.5f);
            piece.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, tint);
            piece.SetPropertyBlock(_block);
        }
    }
}
