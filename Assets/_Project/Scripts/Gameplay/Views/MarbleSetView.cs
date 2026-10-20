using MarbleSync.Simulation;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>All marbles of one simulation (live, replay or ghost), with interpolation between sim steps.</summary>
    public sealed class MarbleSetView : MonoBehaviour
    {
        [SerializeField] private MarbleView marblePrefab;
        [SerializeField] private Transform container;
        [SerializeField] private SeesawView seesaw;
        [SerializeField] private SfxPlayer sfx;
        [SerializeField] private bool ghost;

        private ComponentPool<MarbleView> _pool;
        private LevelDefinition _level;
        private Vector3[] _previous = new Vector3[0];
        private Vector3[] _current = new Vector3[0];
        private float[] _previousSeesawT = new float[0];
        private float[] _currentSeesawT = new float[0];
        private bool[] _onSeesaw = new bool[0];
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
            _previousSeesawT = new float[count];
            _currentSeesawT = new float[count];
            _onSeesaw = new bool[count];
            for (int i = 0; i < count; i++)
            {
                MarbleView view = _pool.Get();
                view.name = (ghost ? "Ghost_" : "Marble_") + level.Lane(i).MarbleName;
                view.Configure(i, level.Lane(i).MarbleColor, ghost, sfx.RollClip);
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
                Sample(sim, i);
                _previous[i] = _current[i];
                _previousSeesawT[i] = _currentSeesawT[i];
            }
            _snapNext = true;
        }

        /// <summary>Call after every simulation step.</summary>
        public void Capture(MarbleSimulation sim)
        {
            for (int i = 0; i < _current.Length; i++)
            {
                bool wasOnSeesaw = _onSeesaw[i];
                _previous[i] = _current[i];
                _previousSeesawT[i] = _currentSeesawT[i];
                Sample(sim, i);
                if (_onSeesaw[i] && !wasOnSeesaw)
                {
                    _previousSeesawT[i] = 0f;
                }
            }
        }

        public void Render(MarbleSimulation sim, float alpha)
        {
            for (int i = 0; i < _current.Length; i++)
            {
                MarbleView view = _pool.Active[i];
                // Seesaw positions are resolved every frame so marbles ride the deck while it tilts.
                Vector3 point = _onSeesaw[i]
                    ? seesaw.LanePoint(i, Mathf.Lerp(_previousSeesawT[i], _currentSeesawT[i], alpha))
                    : Vector3.Lerp(_previous[i], _current[i], alpha);
                view.Place(point, _snapNext);
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
            var bounds = new Bounds(_pool.Active[0].transform.position, Vector3.one);
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

        private void Sample(MarbleSimulation sim, int i)
        {
            MarbleState m = sim.GetMarble(i);
            _onSeesaw[i] = m.Status == MarbleStatus.OnSeesaw || m.Status == MarbleStatus.Arrived;
            if (_onSeesaw[i])
            {
                _currentSeesawT[i] = Mathf.Clamp01(m.SeesawPos / _level.Tuning.SeesawLength);
                _current[i] = _level.SeesawLaneStart(i);
            }
            else
            {
                _currentSeesawT[i] = 0f;
                _current[i] = _level.Path(i).Evaluate(m.Distance);
            }
        }
    }
}
