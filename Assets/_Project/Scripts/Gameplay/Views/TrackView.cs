using System.Collections.Generic;
using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Half-pipe channel for every lane plus its elements. Built from pools on each level load.</summary>
    public sealed class TrackView : MonoBehaviour
    {
        [SerializeField] private HalfPipeView pipePrefab;
        [SerializeField] private ElementView elementPrefab;
        [SerializeField] private Transform pipeContainer;
        [SerializeField] private Transform elementContainer;
        [SerializeField] private float laneTint = 0.35f;
        [SerializeField] private Color trackColor = new Color(0.86f, 0.84f, 0.8f);

        private ComponentPool<HalfPipeView> _pipes;
        private ComponentPool<ElementView> _elements;
        private readonly List<Vector3> _points = new List<Vector3>();
        private Bounds _bounds;

        public Bounds LevelBounds => _bounds;

        public void Build(LevelDefinition level)
        {
            _pipes ??= new ComponentPool<HalfPipeView>(pipePrefab, pipeContainer, 8);
            _elements ??= new ComponentPool<ElementView>(elementPrefab, elementContainer, 32);
            _pipes.ReleaseAll();
            _elements.ReleaseAll();

            _bounds = new Bounds(level.SeesawStart, Vector3.one);
            _bounds.Encapsulate(level.SeesawEnd);

            LevelSimDef sim = level.SimDef;
            for (int lane = 0; lane < level.MarbleCount; lane++)
            {
                TrackPath path = level.Path(lane);
                Color tint = LaneTint(level, lane);

                _points.Clear();
                for (int p = 0; p < path.PointCount; p++)
                {
                    Vector3 point = path.Point(p) - pipeContainer.position;
                    _points.Add(point);
                    _bounds.Encapsulate(path.Point(p));
                }
                _bounds.Encapsulate(level.SeesawLaneEnd(lane));
                HalfPipeView pipe = _pipes.Get();
                pipe.name = $"Pipe_{level.Lane(lane).MarbleName}";
                pipe.transform.SetPositionAndRotation(pipeContainer.position, Quaternion.identity);
                pipe.Build(_points, tint, true);

                ElementDef[] defs = sim.Tracks[lane].Elements;
                for (int e = 0; e < defs.Length; e++)
                {
                    ElementView view = _elements.Get();
                    view.name = $"{defs[e].Kind}_{lane}_{e}";
                    view.Configure(lane, e, defs[e], path.Evaluate(defs[e].Distance), path.Direction(defs[e].Distance));
                }
            }
        }

        public Color LaneTint(LevelDefinition level, int lane)
        {
            return Color.Lerp(trackColor, level.Lane(lane).MarbleColor, laneTint);
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
    }
}
