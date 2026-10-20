using System.Collections.Generic;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    /// <summary>
    /// Extrudes a U-shaped (half-pipe) profile along a polyline. The polyline is the bottom line of the
    /// channel, which is where the marble rolls. Corners are mitred so the channel keeps its width.
    /// </summary>
    public static class HalfPipeMesh
    {
        public static void Build(Mesh mesh, IReadOnlyList<Vector3> points, float radius, float thickness, int arcSegments, bool capStart)
        {
            if (points.Count < 2)
            {
                throw new System.ArgumentException("A half-pipe needs at least two points.", nameof(points));
            }

            int rings = points.Count;
            int profile = arcSegments + 1;
            var vertices = new List<Vector3>(rings * profile * 2 + rings * 4);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(rings * arcSegments * 12 + rings * 12);

            var centers = new Vector3[rings];
            var rights = new Vector3[rings];
            var ups = new Vector3[rings];
            float[] along = new float[rings];

            for (int i = 0; i < rings; i++)
            {
                Vector3 inDir = i > 0 ? (points[i] - points[i - 1]).normalized : (points[1] - points[0]).normalized;
                Vector3 outDir = i < rings - 1 ? (points[i + 1] - points[i]).normalized : inDir;
                Vector3 tangent = (inDir + outDir).normalized;
                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent = outDir;
                }

                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 segmentRight = Vector3.Cross(Vector3.up, outDir).normalized;
                float miter = 1f / Mathf.Max(0.35f, Vector3.Dot(right, segmentRight));
                Vector3 up = Vector3.Cross(tangent, right).normalized;

                rights[i] = right * miter;
                ups[i] = up;
                centers[i] = points[i] + up * radius;
                along[i] = i == 0 ? 0f : along[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }

            // Inner surface (faces the marble) then outer surface.
            int innerStart = vertices.Count;
            AddSurface(vertices, normals, uvs, centers, rights, ups, along, radius, arcSegments, inward: true);
            int outerStart = vertices.Count;
            AddSurface(vertices, normals, uvs, centers, rights, ups, along, radius + thickness, arcSegments, inward: false);

            for (int i = 0; i < rings - 1; i++)
            {
                for (int s = 0; s < arcSegments; s++)
                {
                    int a = innerStart + i * profile + s;
                    int b = a + 1;
                    int c = a + profile;
                    int d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);

                    int oa = outerStart + i * profile + s;
                    int ob = oa + 1;
                    int oc = oa + profile;
                    int od = oc + 1;
                    triangles.Add(oa); triangles.Add(ob); triangles.Add(oc);
                    triangles.Add(ob); triangles.Add(od); triangles.Add(oc);
                }
            }

            // Flat rims along both top edges.
            AddRim(vertices, normals, uvs, triangles, centers, rights, ups, along, radius, thickness, side: -1f);
            AddRim(vertices, normals, uvs, triangles, centers, rights, ups, along, radius, thickness, side: 1f);

            if (capStart)
            {
                AddCap(vertices, normals, uvs, triangles, points, centers[0], rights[0], ups[0], radius, thickness, arcSegments);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private static void AddSurface(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            Vector3[] centers, Vector3[] rights, Vector3[] ups, float[] along, float r, int arcSegments, bool inward)
        {
            for (int i = 0; i < centers.Length; i++)
            {
                for (int s = 0; s <= arcSegments; s++)
                {
                    float angle = Mathf.PI + Mathf.PI * s / arcSegments; // left rim -> bottom -> right rim
                    Vector3 offset = rights[i] * Mathf.Cos(angle) * r + ups[i] * Mathf.Sin(angle) * r;
                    vertices.Add(centers[i] + offset);
                    Vector3 n = offset.normalized;
                    normals.Add(inward ? -n : n);
                    uvs.Add(new Vector2(s / (float)arcSegments, along[i]));
                }
            }
        }

        private static void AddRim(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3[] centers, Vector3[] rights, Vector3[] ups, float[] along, float r, float thickness, float side)
        {
            int start = vertices.Count;
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 right = rights[i] * side;
                vertices.Add(centers[i] + right * r);
                vertices.Add(centers[i] + right * (r + thickness));
                normals.Add(ups[i]);
                normals.Add(ups[i]);
                uvs.Add(new Vector2(0f, along[i]));
                uvs.Add(new Vector2(1f, along[i]));
            }

            for (int i = 0; i < centers.Length - 1; i++)
            {
                int a = start + i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                if (side < 0f)
                {
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
                else
                {
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
        }

        /// <summary>Closes the start of the channel so the marble sits against a wall before release.</summary>
        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            IReadOnlyList<Vector3> points, Vector3 center, Vector3 right, Vector3 up, float r, float thickness, int arcSegments)
        {
            Vector3 back = -(points[1] - points[0]).normalized;
            Vector3 capCenter = center + back * thickness;
            int centerIndex = vertices.Count;
            vertices.Add(capCenter);
            normals.Add(back);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= arcSegments; s++)
            {
                float angle = Mathf.PI + Mathf.PI * s / arcSegments;
                vertices.Add(capCenter + right * Mathf.Cos(angle) * (r + thickness) + up * Mathf.Sin(angle) * (r + thickness));
                normals.Add(back);
                uvs.Add(new Vector2(s / (float)arcSegments, 0f));
            }
            for (int s = 0; s < arcSegments; s++)
            {
                // Two-sided so it reads from inside and outside.
                triangles.Add(centerIndex); triangles.Add(centerIndex + 1 + s); triangles.Add(centerIndex + 2 + s);
                triangles.Add(centerIndex); triangles.Add(centerIndex + 2 + s); triangles.Add(centerIndex + 1 + s);
            }
        }
    }
}
