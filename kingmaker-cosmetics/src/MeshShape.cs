using UnityEngine;

namespace KingmakerCosmetics
{
    static class MeshShape
    {
        // A copy of the mesh with what hangs below `from` drawn up so its lowest point comes to `back` at the nape and
        // to `front` at the face, between them by the place front to back: the strands get shorter but keep their own
        // tips, which a cut would leave as a straight edge across the strand texture. Normals, uv, bone weights and
        // bind poses stay, so the copy skins on the same bones. `forward` is the sign of the z axis the face looks along.
        internal static Mesh Shorten(Mesh source, float from, float back, float front, float forward)
        {
            var mesh = Object.Instantiate(source);
            var vertices = mesh.vertices;
            float lowest = source.bounds.min.y;
            if (from <= lowest)
                return mesh;
            float nearest = float.MaxValue, farthest = float.MinValue;
            foreach (var v in vertices)
                if (v.y < from)
                {
                    nearest = Mathf.Min(nearest, v.z * forward);
                    farthest = Mathf.Max(farthest, v.z * forward);
                }
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y >= from)
                    continue;
                float t = farthest > nearest ? Mathf.InverseLerp(nearest, farthest, vertices[i].z * forward) : 0.5f;
                float to = Mathf.Lerp(back, front, t);
                float k = Mathf.Clamp01((from - to) / (from - lowest));
                vertices[i].y = from - (from - vertices[i].y) * k;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
