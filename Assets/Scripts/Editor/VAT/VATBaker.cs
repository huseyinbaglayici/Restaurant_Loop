using System.Collections.Generic;
using RestaurantLoop.Editor.LevelEditor;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace RestaurantLoop.Editor.VAT
{
    // Bakes Customer.prefab's idle clip into a position-only Vertex Animation Texture.
    // The prefab uses two shaders, so its parts are baked into two meshes/materials (Body, Outfit)
    // that share one position texture and clip layout. Each part's flat color goes into vertex color.
    // Runtime playback: VATCustomerVisual.
    public static class VATBaker
    {
        private const float SampleFps = 30f;
        private const string CustomerPrefabPath = "Assets/Art/Models/Character/Customer.prefab";
        private const string OutputFolder = "Assets/Art/VAT/Body";
        private const string EyebrowName = "Eyebrow";

        private const string BodyShaderName = "RestaurantLoop/SoftMatte";
        // Soft_Matte_2.shader declares the same shader name as RestaurantSoftMatte.shader,
        // so it is loaded by path instead of Shader.Find.
        private const string OutfitShaderPath = "Assets/Prefabs/JapanScenePrefabs/Soft_Matte_2.shader";

        private class SkinnedSource
        {
            public SkinnedMeshRenderer Renderer;
            public Color VertexColor;
        }

        private class RigidSource
        {
            public Transform Source;
            public Mesh Mesh;
            public Color VertexColor;
        }

        private class PartGroup
        {
            public string Name;
            public Shader Shader;
            // Its full property set is copied onto the baked material so the tuned look survives.
            public Material TemplateMaterial;
            public List<SkinnedSource> Skinned = new List<SkinnedSource>();
            public List<RigidSource> Rigid = new List<RigidSource>();

            public int VertexCount
            {
                get
                {
                    int n = 0;
                    foreach (var s in Skinned) n += s.Renderer.sharedMesh.vertexCount;
                    foreach (var r in Rigid) n += r.Mesh.vertexCount;
                    return n;
                }
            }
        }

        // All groups share one vertex index space: group by group, skinned then rigid within each.
        private class BakeBuffers
        {
            public readonly int TotalVerts;
            public readonly Color[] Positions;
            public readonly Vector2[] VatId;
            public readonly Color[] Colors;
            public readonly Vector2[] Uv;
            public readonly Vector3[] RestVerts;
            public readonly Vector3[] RestNormals;
            public readonly (int start, int count)[] GroupRanges;
            public readonly List<int>[] GroupTris;

            public BakeBuffers(int totalVerts, int frameCount, int groupCount)
            {
                TotalVerts = totalVerts;
                Positions = new Color[totalVerts * frameCount];
                VatId = new Vector2[totalVerts];
                Colors = new Color[totalVerts];
                Uv = new Vector2[totalVerts];
                RestVerts = new Vector3[totalVerts];
                RestNormals = new Vector3[totalVerts];
                GroupRanges = new (int start, int count)[groupCount];
                GroupTris = new List<int>[groupCount];
            }
        }

        [MenuItem("RestaurantLoop/VAT/Bake Customer Body")]
        public static void BakeBody()
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (sourcePrefab == null) { Debug.LogError($"[VATBaker] {CustomerPrefabPath} not found."); return; }

            var idleClip = FindIdleClip(sourcePrefab);
            if (idleClip == null) return;

            var bodyShader = Shader.Find(BodyShaderName);
            var outfitShader = AssetDatabase.LoadAssetAtPath<Shader>(OutfitShaderPath);
            if (bodyShader == null) { Debug.LogError($"[VATBaker] Shader '{BodyShaderName}' not found."); return; }
            if (outfitShader == null) { Debug.LogError($"[VATBaker] Shader at '{OutfitShaderPath}' not found."); return; }

            var scratch = Object.Instantiate(sourcePrefab);
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var bodyGroup = new PartGroup { Name = "Body", Shader = bodyShader };
                AddSkinned(scratch.transform, bodyGroup, "body");
                AddSkinned(scratch.transform, bodyGroup, "eyes");
                bodyGroup.TemplateMaterial = FirstSkinnedMaterial(bodyGroup);

                var outfitGroup = new PartGroup { Name = "Outfit", Shader = outfitShader };
                AddSkinned(scratch.transform, outfitGroup, "pants");
                AddSkinned(scratch.transform, outfitGroup, "shoes");
                AddSkinned(scratch.transform, outfitGroup, "tshirt");
                AddEyebrow(scratch.transform, outfitGroup);
                // pants shares its tuning with most outfit parts, so it is the template
                outfitGroup.TemplateMaterial = FirstSkinnedMaterial(outfitGroup);

                BakeGroups(new[] { bodyGroup, outfitGroup }, scratch.transform, idleClip, OutputFolder);
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        private static Material FirstSkinnedMaterial(PartGroup group) =>
            group.Skinned.Count > 0 ? group.Skinned[0].Renderer.sharedMaterial : null;

        private static void AddSkinned(Transform root, PartGroup group, string partName)
        {
            var t = FindDeepChild(root, partName);
            var smr = t != null ? t.GetComponent<SkinnedMeshRenderer>() : null;
            if (smr == null || smr.sharedMesh == null)
            {
                Debug.LogError($"[VATBaker] Missing skinned body part '{partName}'.");
                return;
            }
            group.Skinned.Add(new SkinnedSource { Renderer = smr, VertexColor = GetFlatColor(smr.sharedMaterial) });
        }

        private static void AddEyebrow(Transform root, PartGroup group)
        {
            var eyebrowT = FindDeepChild(root, EyebrowName);
            var eyebrowMf = eyebrowT != null ? eyebrowT.GetComponent<MeshFilter>() : null;
            if (eyebrowMf == null || eyebrowMf.sharedMesh == null)
            {
                Debug.LogWarning("[VATBaker] Eyebrow mesh not found, baking without it.");
                return;
            }
            var eyebrowMr = eyebrowT.GetComponent<MeshRenderer>();
            group.Rigid.Add(new RigidSource
            {
                Source = eyebrowT,
                Mesh = eyebrowMf.sharedMesh,
                VertexColor = GetFlatColor(eyebrowMr != null ? eyebrowMr.sharedMaterial : null)
            });
        }

        private static void BakeGroups(PartGroup[] groups, Transform root, AnimationClip clip, string folder)
        {
            int totalVerts = 0;
            foreach (var g in groups) totalVerts += g.VertexCount;

            if (totalVerts == 0)
            {
                Debug.LogError("[VATBaker] No source vertices to bake.");
                return;
            }

            AssetFolderUtil.EnsureFolder(folder);

            int frameCount = Mathf.Max(1, Mathf.RoundToInt(clip.length * SampleFps));
            var buffers = new BakeBuffers(totalVerts, frameCount, groups.Length);
            LayOutVertices(groups, buffers);
            SampleFrames(groups, root, clip, frameCount, buffers);

            var tex = WritePositionTexture(folder, buffers.Positions, totalVerts, frameCount);
            WriteClipSet(folder, tex, clip, totalVerts, frameCount);
            for (int g = 0; g < groups.Length; g++)
            {
                WriteGroupMesh(folder, groups[g], buffers, g);
                WriteGroupMaterial(folder, groups[g], tex, frameCount);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[VATBaker] Baked {totalVerts} verts x {frameCount} frames across {groups.Length} groups -> {folder}");
        }

        // Static per-vertex data (VAT column id, uv, color) and each group's triangle list.
        private static void LayOutVertices(PartGroup[] groups, BakeBuffers buffers)
        {
            int cursor = 0;
            for (int g = 0; g < groups.Length; g++)
            {
                int groupStart = cursor;
                buffers.GroupTris[g] = new List<int>();

                foreach (var s in groups[g].Skinned)
                    LayOutMesh(buffers, s.Renderer.sharedMesh, s.VertexColor, groupStart, buffers.GroupTris[g], ref cursor);
                foreach (var r in groups[g].Rigid)
                    LayOutMesh(buffers, r.Mesh, r.VertexColor, groupStart, buffers.GroupTris[g], ref cursor);

                buffers.GroupRanges[g] = (groupStart, cursor - groupStart);
            }
        }

        private static void LayOutMesh(BakeBuffers buffers, Mesh mesh, Color color, int groupStart, List<int> groupTris, ref int cursor)
        {
            int count = mesh.vertexCount;
            var uv0 = mesh.uv;
            var tris = mesh.triangles;
            for (int i = 0; i < count; i++)
            {
                buffers.VatId[cursor + i] = new Vector2((cursor + i + 0.5f) / buffers.TotalVerts, 0f);
                buffers.Uv[cursor + i] = (uv0 != null && i < uv0.Length) ? uv0[i] : Vector2.zero;
                buffers.Colors[cursor + i] = color;
            }
            // indices are local to the group's own mesh, not the shared vertex space
            for (int i = 0; i < tris.Length; i++) groupTris.Add(tris[i] + (cursor - groupStart));
            cursor += count;
        }

        // One texture row per frame, root-local positions. Frame 0 also becomes the rest pose mesh.
        private static void SampleFrames(PartGroup[] groups, Transform root, AnimationClip clip, int frameCount, BakeBuffers buffers)
        {
            var bakedMesh = new Mesh();
            for (int f = 0; f < frameCount; f++)
            {
                float t = frameCount == 1 ? 0f : clip.length * f / (frameCount - 1);
                clip.SampleAnimation(root.gameObject, t);

                Matrix4x4 rootInverse = root.worldToLocalMatrix;
                int writeCursor = 0;

                foreach (var group in groups)
                {
                    foreach (var s in group.Skinned)
                    {
                        s.Renderer.BakeMesh(bakedMesh, false);
                        Matrix4x4 toRoot = rootInverse * s.Renderer.transform.localToWorldMatrix;
                        WriteFrameVertices(buffers, bakedMesh.vertices, bakedMesh.normals, toRoot, f, ref writeCursor);
                    }

                    foreach (var r in group.Rigid)
                    {
                        Matrix4x4 toRoot = rootInverse * r.Source.localToWorldMatrix;
                        WriteFrameVertices(buffers, r.Mesh.vertices, r.Mesh.normals, toRoot, f, ref writeCursor);
                    }
                }
            }
            Object.DestroyImmediate(bakedMesh);
        }

        private static void WriteFrameVertices(BakeBuffers buffers, Vector3[] verts, Vector3[] normals, Matrix4x4 toRoot,
            int frame, ref int writeCursor)
        {
            int rowBase = frame * buffers.TotalVerts;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(verts[i]);
                buffers.Positions[rowBase + writeCursor + i] = new Color(p.x, p.y, p.z, 1f);
                if (frame == 0)
                {
                    buffers.RestVerts[writeCursor + i] = p;
                    buffers.RestNormals[writeCursor + i] = (normals != null && i < normals.Length)
                        ? toRoot.MultiplyVector(normals[i]).normalized
                        : Vector3.up;
                }
            }
            writeCursor += verts.Length;
        }

        private static Texture2D WritePositionTexture(string folder, Color[] positions, int totalVerts, int frameCount)
        {
            string texPath = $"{folder}/Body_VATPositions.asset";
            var tex = LoadOrCreate<Texture2D>(texPath, () => new Texture2D(totalVerts, frameCount, TextureFormat.RGBAHalf, false, true));
            if (tex.width != totalVerts || tex.height != frameCount)
                tex.Reinitialize(totalVerts, frameCount, TextureFormat.RGBAHalf, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(positions);
            tex.Apply(false, false);
            PersistAsset(tex, texPath);
            return tex;
        }

        private static void WriteClipSet(string folder, Texture2D tex, AnimationClip clip, int totalVerts, int frameCount)
        {
            string clipSetPath = $"{folder}/Body_VATClipSet.asset";
            var clipSet = LoadOrCreate<VATClipSet>(clipSetPath, ScriptableObject.CreateInstance<VATClipSet>);
            clipSet.PositionTexture = tex;
            clipSet.VertexCount = totalVerts;
            clipSet.TotalFrames = frameCount;
            clipSet.SampleFps = SampleFps;
            clipSet.Idle = new VATClipEntry { ClipName = clip.name, StartFrame = 0, FrameCount = frameCount, Loop = true };
            PersistAsset(clipSet, clipSetPath);
        }

        private static void WriteGroupMesh(string folder, PartGroup group, BakeBuffers buffers, int groupIndex)
        {
            var (start, count) = buffers.GroupRanges[groupIndex];

            string meshPath = $"{folder}/{group.Name}_VATMesh.asset";
            var vatMesh = LoadOrCreate<Mesh>(meshPath, () => new Mesh());
            vatMesh.Clear();
            if (count > 65535) vatMesh.indexFormat = IndexFormat.UInt32;
            vatMesh.vertices = Slice(buffers.RestVerts, start, count);
            vatMesh.normals = Slice(buffers.RestNormals, start, count);
            vatMesh.uv = Slice(buffers.Uv, start, count);
            vatMesh.colors = Slice(buffers.Colors, start, count);
            vatMesh.uv2 = Slice(buffers.VatId, start, count);
            vatMesh.triangles = buffers.GroupTris[groupIndex].ToArray();
            vatMesh.RecalculateBounds();
            vatMesh.name = group.Name + "_VATMesh";
            PersistAsset(vatMesh, meshPath);
        }

        private static void WriteGroupMaterial(string folder, PartGroup group, Texture2D tex, int frameCount)
        {
            string matPath = $"{folder}/{group.Name}_VATMaterial.mat";
            var mat = LoadOrCreate<Material>(matPath, () => new Material(group.Shader));
            if (group.TemplateMaterial != null)
                EditorUtility.CopySerialized(group.TemplateMaterial, mat);
            mat.shader = group.Shader;
            mat.name = group.Name + "_VATMaterial";
            // vertex color already carries each part's tint; a non-white base color would tint twice
            mat.SetColor("_BaseColor", Color.white);
            // crowd uses blob-shadow decals, realtime shadow receiving is wasted work on mobile
            mat.SetFloat("_ReceiveShadowStrength", 0f);
            mat.EnableKeyword("_VAT_ON");
            mat.EnableKeyword("_VERTEXCOLOR_ON");
            mat.SetFloat("_VATToggle", 1f);
            mat.SetFloat("_VertexColorToggle", 1f);
            mat.SetTexture("_VATPositions", tex);
            mat.SetFloat("_VATTexHeight", frameCount);
            mat.enableInstancing = true;
            PersistAsset(mat, matPath);
        }

        private static T[] Slice<T>(T[] source, int start, int count)
        {
            var result = new T[count];
            System.Array.Copy(source, start, result, 0, count);
            return result;
        }

        private static AnimationClip FindIdleClip(GameObject sourcePrefab)
        {
            var anim = sourcePrefab.GetComponentInChildren<Animator>(true);
            var controller = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            if (controller == null || controller.layers.Length == 0)
            {
                Debug.LogError("[VATBaker] Could not resolve the idle Animator Controller's states.");
                return null;
            }

            foreach (var s in controller.layers[0].stateMachine.states)
                if (s.state.motion is AnimationClip clip) return clip;

            Debug.LogError("[VATBaker] No AnimationClip found on the idle controller's states.");
            return null;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Color GetFlatColor(Material mat)
        {
            if (mat == null) return Color.white;
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color")) return mat.color;
            return Color.white;
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> factory) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            return existing != null ? existing : factory();
        }

        private static void PersistAsset(Object asset, string path)
        {
            if (AssetDatabase.Contains(asset)) EditorUtility.SetDirty(asset);
            else AssetDatabase.CreateAsset(asset, path);
        }
    }
}
