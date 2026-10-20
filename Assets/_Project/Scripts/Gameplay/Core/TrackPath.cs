using System;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>Polyline geometry for one lane. Maps a 1D simulation distance to a world position.</summary>
    public sealed class TrackPath
    {
        private readonly Vector3[] _points;
        private readonly float[] _cumulative;

        public TrackPath(Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                throw new ArgumentException("A track path needs at least two points.", nameof(points));
            }
            _points = points;
            _cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }

        public float Length => _cumulative[_cumulative.Length - 1];
        public int PointCount => _points.Length;
        public Vector3 Point(int index) => _points[index];
        public float DistanceAtPoint(int index) => _cumulative[index];

        public Vector3 Evaluate(float distance)
        {
            int segment = FindSegment(distance);
            float segLength = _cumulative[segment + 1] - _cumulative[segment];
            float t = segLength > 0f ? (distance - _cumulative[segment]) / segLength : 0f;
            return Vector3.Lerp(_points[segment], _points[segment + 1], Mathf.Clamp01(t));
        }

        public Vector3 Direction(float distance)
        {
            int segment = FindSegment(distance);
            return (_points[segment + 1] - _points[segment]).normalized;
        }

        private int FindSegment(float distance)
        {
            for (int i = 0; i < _cumulative.Length - 2; i++)
            {
                if (distance < _cumulative[i + 1])
                {
                    return i;
                }
            }
            return _cumulative.Length - 2;
        }
    }
}
