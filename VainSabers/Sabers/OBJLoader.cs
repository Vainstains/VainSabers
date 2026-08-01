using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using VainSabers.Config;

namespace VainSabers.Sabers
{
    internal class ObjMeshData
    {
        public Vector3[] Positions = Array.Empty<Vector3>();
        public Vector3[] Normals = Array.Empty<Vector3>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        public int[] Triangles = Array.Empty<int>();
        public string CacheKey = "";
    }

    internal static class OBJLoader
    {
        private static readonly NumberFormatInfo Invariant = CultureInfo.InvariantCulture.NumberFormat;

        public static ObjMeshData Load(string? fileName, string? embeddedBase64)
        {
            if (!string.IsNullOrEmpty(embeddedBase64))
            {
                try
                {
                    var text = Encoding.UTF8.GetString(Convert.FromBase64String(embeddedBase64!));
                    return Parse(text, "embedded|" + embeddedBase64);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"Failed to decode embedded obj '{fileName}': {ex.Message}");
                    return new ObjMeshData();
                }
            }

            if (string.IsNullOrEmpty(fileName))
                return new ObjMeshData();

            var path = System.IO.Path.Combine(ConfigUtil.ConfigDir, fileName!);
            if (!System.IO.File.Exists(path))
                return new ObjMeshData();

            string text2;
            try
            {
                text2 = System.IO.File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to load obj '{fileName}': {ex.Message}");
                return new ObjMeshData();
            }

            return Parse(text2, fileName + "|" + System.IO.File.GetLastWriteTimeUtc(path).Ticks);
        }

        public static ObjMeshData Parse(string text, string cacheKey)
        {
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var faces = new List<List<(int pos, int uv, int nrm)>>();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0])
                {
                    case "v":
                        if (parts.Length >= 4
                            && TryFloat(parts[1], out var vx)
                            && TryFloat(parts[2], out var vy)
                            && TryFloat(parts[3], out var vz))
                        {
                            positions.Add(new Vector3(vx, vy, vz));
                        }
                        break;
                    case "vt":
                        if (parts.Length >= 3 && TryFloat(parts[1], out var tu) && TryFloat(parts[2], out var tv))
                            uvs.Add(new Vector2(tu, tv));
                        break;
                    case "vn":
                        if (parts.Length >= 4
                            && TryFloat(parts[1], out var nx)
                            && TryFloat(parts[2], out var ny)
                            && TryFloat(parts[3], out var nz))
                        {
                            normals.Add(new Vector3(nx, ny, nz).normalized);
                        }
                        break;
                    case "f":
                        var face = new List<(int, int, int)>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            int posIdx = 0, uvIdx = -1, nrmIdx = -1;
                            var segs = parts[i].Split('/');
                            if (segs.Length >= 1)
                                posIdx = ResolveIndex(segs[0], positions.Count);
                            if (segs.Length >= 2 && segs[1].Length > 0)
                                uvIdx = ResolveIndex(segs[1], uvs.Count);
                            if (segs.Length >= 3 && segs[2].Length > 0)
                                nrmIdx = ResolveIndex(segs[2], normals.Count);
                            if (posIdx < 0 || posIdx >= positions.Count)
                                continue;
                            face.Add((posIdx, uvIdx, nrmIdx));
                        }
                        if (face.Count >= 3)
                            faces.Add(face);
                        break;
                }
            }

            var outPositions = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outNormals = new List<Vector3>();
            var outTriangles = new List<int>();
            var missingNormals = normals.Count == 0;

            foreach (var face in faces)
            {
                int baseIdx = outPositions.Count;
                for (int i = 0; i < face.Count; i++)
                {
                    var corner = face[i];
                    outPositions.Add(positions[corner.pos]);
                    if (corner.uv >= 0 && corner.uv < uvs.Count)
                        outUvs.Add(uvs[corner.uv]);
                    else
                        outUvs.Add(Vector2.zero);
                    if (!missingNormals && corner.nrm >= 0 && corner.nrm < normals.Count)
                        outNormals.Add(normals[corner.nrm]);
                    else
                        outNormals.Add(Vector3.zero);
                }
                for (int i = 1; i < face.Count - 1; i++)
                {
                    outTriangles.Add(baseIdx);
                    outTriangles.Add(baseIdx + i);
                    outTriangles.Add(baseIdx + i + 1);
                }
            }

            if (missingNormals)
                ComputeFaceNormals(outPositions, outTriangles, outNormals);

            return new ObjMeshData
            {
                Positions = outPositions.ToArray(),
                Normals = outNormals.ToArray(),
                Uvs = outUvs.ToArray(),
                Triangles = outTriangles.ToArray(),
                CacheKey = cacheKey
            };
        }

        private static void ComputeFaceNormals(List<Vector3> positions, List<int> triangles, List<Vector3> normals)
        {
            var accumulated = new Vector3[positions.Count];
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                accumulated[a] += n;
                accumulated[b] += n;
                accumulated[c] += n;
            }
            normals.Clear();
            for (int i = 0; i < positions.Count; i++)
            {
                var n = accumulated[i];
                normals.Add(n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.forward);
            }
        }

        private static int ResolveIndex(string token, int count)
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                return -1;
            if (idx < 0)
                idx = count + idx;
            else
                idx -= 1;
            return idx;
        }

        private static bool TryFloat(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, Invariant, out value);
        }
    }
}
