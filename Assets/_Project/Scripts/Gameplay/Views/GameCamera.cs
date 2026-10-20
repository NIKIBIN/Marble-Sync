using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Keeps a box in frame from a fixed viewing angle. The tension director narrows the box.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class GameCamera : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private Vector3 viewEuler = new Vector3(42f, -18f, 0f);
        [SerializeField] private float margin = 1.15f;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float smoothTime = 0.3f;

        private Vector3 _targetCenter;
        private float _targetDistance = 10f;
        private Vector3 _center;
        private float _distance = 10f;
        private Vector3 _centerVelocity;
        private float _distanceVelocity;
        private float _punch;

        [Tooltip("Short, single camera kick on big moments (seesaw closing, success). Not a continuous shake.")]
        [SerializeField] private bool punchEnabled = true;

        public void Punch(float strength)
        {
            if (punchEnabled)
            {
                _punch = Mathf.Max(_punch, strength);
            }
        }

        public void Frame(Bounds bounds, bool instant)
        {
            _targetCenter = bounds.center;
            _targetDistance = Mathf.Max(minDistance, FitDistance(bounds) * margin);
            if (instant)
            {
                _center = _targetCenter;
                _distance = _targetDistance;
                _centerVelocity = Vector3.zero;
                _distanceVelocity = 0f;
                Apply();
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _center = Vector3.SmoothDamp(_center, _targetCenter, ref _centerVelocity, smoothTime, Mathf.Infinity, dt);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity, smoothTime, Mathf.Infinity, dt);
            Apply();
        }

        /// <summary>Smallest camera distance from the box centre that keeps all 8 corners on screen.</summary>
        private float FitDistance(Bounds bounds)
        {
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(viewEuler));
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            float distance = 0f;
            Vector3 e = bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = inverse * corner;
                distance = Mathf.Max(distance, p.z + Mathf.Abs(p.x) / tanH, p.z + Mathf.Abs(p.y) / tanV);
            }
            return distance;
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(viewEuler);
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 1.2f);
            Vector3 kick = rotation * new Vector3(0f, -_punch * Mathf.Sin(_punch * 60f), 0f) * (_distance * 0.02f);
            transform.SetPositionAndRotation(_center - rotation * Vector3.forward * _distance + kick, rotation);
        }
    }
}
