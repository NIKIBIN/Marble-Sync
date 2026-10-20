using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>All marbles of one simulation (live, replay or ghost), with interpolation between sim steps.</summary>
    public sealed class MarbleSetView : MonoBehaviour
    {
        [SerializeField] private MarbleView marblePrefab;
        [SerializeField] private Transform container;
        [SerializeField] private bool ghost;

        private ComponentPool<MarbleView> _pool;
        private LevelDefinition _level;
        private Vector3[] _previous = new Vector3[0];
        private Vector3[] _current = new Vector3[0];
        private bool _snapNext;

        public int Count => _pool == null ? 0 : _pool.Active.Count;

        public MarbleView Get(int index) => _pool.Active[index];

        public void Build(LevelDefinition level)
        {
            _pool ??= new ComponentPool<MarbleView>(marblePrefab, container, 8);
            _pool.ReleaseAll();
            _level = level;

            int count = level.MarbleCount;
            _previous = new Vector3[count];
            _current = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                MarbleView view = _pool.Get();
                view.name = (ghost ? "Ghost_" : "Marble_") + level.Lane(i).MarbleName;
                view.Configure(i, level.Lane(i).MarbleColor, ghost);
                Vector3 start = level.Path(i).Evaluate(0f);
                _previous[i] = start;
                _current[i] = start;
                view.Place(start, true);
            }
            _snapNext = true;
        }

        public void SetVisible(bool visible)
        {
            container.gameObject.SetActive(visible);
        }

        /// <summary>Snap to the simulation state (used after resets).</summary>
        public void Sync(MarbleSimulation sim)
        {
            for (int i = 0; i < _current.Length; i++)
            {
                _current[i] = SurfacePoint(sim, i);
                _previous[i] = _current[i];
            }
            _snapNext = true;
        }

        /// <summary>Call after every simulation step.</summary>
        public void Capture(MarbleSimulation sim)
        {
            for (int i = 0; i < _current.Length; i++)
            {
                _previous[i] = _current[i];
                _current[i] = SurfacePoint(sim, i);
            }
        }

        public void Render(MarbleSimulation sim, float alpha)
        {
            for (int i = 0; i < _current.Length; i++)
            {
                MarbleView view = _pool.Active[i];
                view.Place(Vector3.Lerp(_previous[i], _current[i], alpha), _snapNext);
                view.ShowState(sim.GetMarble(i));
            }
            _snapNext = false;
        }

        public void SetSelected(int index)
        {
            for (int i = 0; i < _pool.Active.Count; i++)
            {
                _pool.Active[i].SetSelected(i == index);
            }
        }

        public Bounds MarbleBounds(MarbleSimulation sim)
        {
            var bounds = new Bounds(_current[0], Vector3.one);
            for (int i = 0; i < _current.Length; i++)
            {
                if (sim.GetMarble(i).Status == MarbleStatus.Melted)
                {
                    continue;
                }
                bounds.Encapsulate(_pool.Active[i].transform.position);
            }
            return bounds;
        }

        private Vector3 SurfacePoint(MarbleSimulation sim, int i)
        {
            MarbleState m = sim.GetMarble(i);
            if (m.Status == MarbleStatus.OnSeesaw || m.Status == MarbleStatus.Arrived)
            {
                float t = Mathf.Clamp01(m.SeesawPos / _level.Tuning.SeesawLength);
                return Vector3.Lerp(_level.SeesawLaneStart(i), _level.SeesawLaneEnd(i), t);
            }
            return _level.Path(i).Evaluate(m.Distance);
        }
    }
}
