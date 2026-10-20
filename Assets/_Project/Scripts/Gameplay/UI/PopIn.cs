using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Elastic scale-in whenever the element appears (outcome banner, continue button).</summary>
    public sealed class PopIn : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float duration = 0.45f;
        [SerializeField] private float overshoot = 1.25f;

        private float _time;

        private void OnEnable()
        {
            _time = 0f;
            target.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (_time >= duration)
            {
                return;
            }
            _time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_time / duration);
            // Damped spring from 0 to 1 that overshoots once.
            float s = 1f - Mathf.Exp(-6f * t) * Mathf.Cos(t * Mathf.PI * 2.2f * overshoot);
            target.localScale = Vector3.one * s;
        }
    }
}
