using System.Collections.Generic;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>One half-pipe channel. Owns its procedural mesh; points are in this transform's local space.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HalfPipeView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float radius = 0.32f;
        [SerializeField] private float thickness = 0.06f;
        [SerializeField, Range(4, 32)] private int arcSegments = 14;

        private Mesh _mesh;
        private MaterialPropertyBlock _block;

        public float Radius => radius;

        public void Build(IReadOnlyList<Vector3> localPoints, Color tint, bool capStart)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "HalfPipe" };
                _mesh.MarkDynamic();
                meshFilter.sharedMesh = _mesh;
            }
            _block ??= new MaterialPropertyBlock();

            HalfPipeMesh.Build(_mesh, localPoints, radius, thickness, arcSegments, capStart);

            meshRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, tint);
            meshRenderer.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
