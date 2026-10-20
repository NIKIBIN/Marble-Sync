using System;
using MarbleSync.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MarbleSync.Gameplay
{
    /// <summary>One row in the attempt history: "#7 — 0,41 s" plus watch / ghost / load-as-base.</summary>
    public sealed class HistoryEntryView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button watchButton;
        [SerializeField] private Button ghostButton;
        [SerializeField] private Text ghostLabel;
        [SerializeField] private Button baseButton;

        private AttemptRecord _record;
        private Action<AttemptRecord> _onWatch;
        private Action<AttemptRecord> _onGhost;
        private Action<AttemptRecord> _onBase;

        private void Awake()
        {
            watchButton.onClick.AddListener(() => _onWatch(_record));
            ghostButton.onClick.AddListener(() => _onGhost(_record));
            baseButton.onClick.AddListener(() => _onBase(_record));
        }

        public void Bind(AttemptRecord record, bool isGhost, Action<AttemptRecord> onWatch, Action<AttemptRecord> onGhost, Action<AttemptRecord> onBase)
        {
            _record = record;
            _onWatch = onWatch;
            _onGhost = onGhost;
            _onBase = onBase;

            AttemptOutcome o = record.Outcome;
            string mark = o.Success ? "✓" : "✗";
            label.text = $"{mark}  #{record.AttemptNumber}";
            label.color = o.Success ? new Color(0.55f, 1f, 0.6f) : Color.white;
            ghostLabel.text = isGhost ? "Fantasma ●" : "Fantasma";
        }
    }
}
