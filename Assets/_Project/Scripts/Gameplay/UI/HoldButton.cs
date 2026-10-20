using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarbleSync.Gameplay
{
    /// <summary>Must be held for a short time before it fires. Prevents accidental restarts.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image fill;
        [SerializeField] private float holdSeconds = 0.5f;

        private bool _pointerHeld;
        private bool _externalHeld;
        private float _progress;
        private bool _firedThisHold;

        public event Action Completed;

        public void SetExternalHold(bool held)
        {
            _externalHeld = held;
        }

        public void OnPointerDown(PointerEventData eventData) => _pointerHeld = true;

        public void OnPointerUp(PointerEventData eventData) => _pointerHeld = false;

        public void OnPointerExit(PointerEventData eventData) => _pointerHeld = false;

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
