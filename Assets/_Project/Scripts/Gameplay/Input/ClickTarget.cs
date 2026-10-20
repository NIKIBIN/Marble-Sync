using UnityEngine;

namespace MarbleSync.Gameplay
{
    public enum ClickTargetKind
    {
        Marble,
        Element
    }

    /// <summary>Tags a collider with what it represents so pointer input can route the click.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ClickTarget : MonoBehaviour
    {
        [SerializeField] private ClickTargetKind kind;

        public ClickTargetKind Kind => kind;
        public int Lane { get; private set; }
        public int Element { get; private set; }

        public void BindMarble(int lane)
        {
            kind = ClickTargetKind.Marble;
            Lane = lane;
            Element = -1;
        }

        public void BindElement(int lane, int element)
        {
            kind = ClickTargetKind.Element;
            Lane = lane;
            Element = element;
        }
    }
}
