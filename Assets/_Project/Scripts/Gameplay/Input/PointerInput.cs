using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MarbleSync.Gameplay
{
    /// <summary>Routes world clicks (marbles, puddles, sensors) and keyboard shortcuts to the attempt controller.</summary>
    public sealed class PointerInput : MonoBehaviour
    {
        [SerializeField] private AttemptController controller;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float maxDistance = 500f;

        private static readonly Key[] ReleaseKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Update()
        {
            HandlePointer();
            HandleKeyboard();
        }

        private void HandlePointer()
        {
            Vector2 screen;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screen = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screen = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else
            {
                return;
            }

            if (eventSystem.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = worldCamera.ScreenPointToRay(screen);
            int count = Physics.RaycastNonAlloc(ray, _hits, maxDistance, clickMask, QueryTriggerInteraction.Collide);
            ClickTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!_hits[i].collider.TryGetComponent(out ClickTarget target))
                {
                    continue;
                }
                // Marbles win over elements underneath them.
                float score = _hits[i].distance - (target.Kind == ClickTargetKind.Marble ? 1000f : 0f);
                if (score < bestDistance)
                {
                    bestDistance = score;
                    best = target;
                }
            }

            if (best == null)
            {
                return;
            }

            if (best.Kind == ClickTargetKind.Marble)
            {
                controller.OnMarbleClicked(best.Lane);
            }
            else
            {
                controller.OnElementClicked(best.Lane, best.Element);
            }
        }

        private void HandleKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.spaceKey.wasPressedThisFrame)
            {
                controller.BeginAttempt();
            }

            // Number keys release marbles 1..5: precise manual timing without aiming the mouse.
            for (int i = 0; i < ReleaseKeys.Length && i < controller.Level.MarbleCount; i++)
            {
                if (kb[ReleaseKeys[i]].wasPressedThisFrame)
                {
                    controller.OnMarbleClicked(i);
                }
            }

            if (kb.upArrowKey.wasPressedThisFrame)
            {
                controller.AdjustSelectedTimer(1);
            }
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                controller.AdjustSelectedTimer(-1);
            }
            if (kb.tKey.wasPressedThisFrame)
            {
                controller.CycleSelectedTrigger();
            }
            if (kb.nKey.wasPressedThisFrame)
            {
                controller.NextLevel();
            }
            if (kb.pKey.wasPressedThisFrame)
            {
                controller.PreviousLevel();
            }
        }
    }
}
