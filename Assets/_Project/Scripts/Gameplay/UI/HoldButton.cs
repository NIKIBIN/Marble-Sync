using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// Fires on press when no hold is required; otherwise must be held for a short time first.
    /// Prevents accidental restarts in the middle of an attempt.
    /// </summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image fill;
        [SerializeField] private float holdSeconds = 0.5f;

        private bool _pointerHeld;
        private bool _externalHeld;
        private float _progress;
        private bool _firedThisHold;

        public event Action Completed;

        public bool RequireHold { get; set; } = true;

        public void SetExternalHold(bool held)
        {
            if (held && !_externalHeld)
            {
                Press();
            }
            _externalHeld = held;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerHeld = true;
            Press();
        }

        public void OnPointerUp(PointerEventData eventData) => _pointerHeld = false;

        public void OnPointerExit(PointerEventData eventData) => _pointerHeld = false;

        private void Press()
        {
            if (!RequireHold)
            {
                _firedThisHold = true;
                Completed?.Invoke();
            }
        }

        private void OnDisable()
        {
            _pointerHeld = false;
            _externalHeld = false;
            _progress = 0f;
            _firedThisHold = false;
            fill.fillAmount = 0f;
        }

        private void Update()
        {
            bool held = _pointerHeld || _externalHeld;
            if (!held)
            {
                _firedThisHold = false;
                _progress = 0f;
            }
            else if (!_firedThisHold)
            {
                _progress += Time.unscaledDeltaTime / holdSeconds;
                if (_progress >= 1f)
                {
                    _progress = 0f;
                    _firedThisHold = true;
                    Completed?.Invoke();
                }
            }
            fill.fillAmount = _progress;
        }
    }
}
