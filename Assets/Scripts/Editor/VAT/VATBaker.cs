using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace RestaurantLoop.Editor.VAT
{
    // Bakes the live customer rig (Assets/Art/Models/Character/Customer.prefab) into a position-only
    // Vertex Animation Texture. Customer.prefab actually uses TWO different shaders across its parts
    // (body/eyes on RestaurantLoop/SoftMatte v1, pants/shoes/tshirt/Eyebrow on Soft_Matte_2 v2) — one
    // merged material can't reproduce both looks, so this bakes into TWO meshes/materials sharing one
    // VAT position texture + one clip layout (still just 2 draw calls total, down from the original
    // 7 unbatched skinned draws). Each part's flat material color is baked into vertex COLOR so each
    // shared material reproduces all its parts' distinct tones. Single fixed character — no hair, no
    // per-instance variation. See VATCustomerVisual for runtime playback.
    public static class VATBaker
    {
        private const float SampleFps = 30f;
        private const string CustomerPrefabPath = "Assets/Art/Models/Character/Customer.prefab";
        private const string EyebrowName = "Eyebrow";

        private const string BodyShaderName = "RestaurantLoop/SoftMatte";
        // Soft_Matte_2.shader also declares itself as "RestaurantLoop/SoftMatte" (name collision with
        // RestaurantSoftMatte.shader) — resolve it by asset path instead of Shader.Find to avoid
        // picking the wrong one.
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
            // Source material whose full property set (hemisphere, directional, toon-step, AO,
            // gradient, etc — not just _BaseColor) is copied onto the baked material, so the merged
            // mesh reproduces Customer.prefab's actual tuned look, not shader defaults.
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
                // body is the dominant visible surface in this group — use its tuning as the template.
                bodyGroup.TemplateMaterial = bodyGroup.Skinned.Count > 0 ? bodyGroup.Skinned[0].Renderer.sharedMaterial : null;

                var outfitGroup = new PartGroup { Name = "Outfit", Shader = outfitShader };
                AddSkinned(scratch.transform, outfitGroup, "pants");
                AddSkinned(scratch.transform, outfitGroup, "shoes");
                AddSkinned(scratch.transform, outfitGroup, "tshirt");
                AddEyebrow(scratch.transform, outfitGroup);
                // pants matches 3 of the 4 outfit parts' _HemisphereWrap (0.92) — most representative template.
                outfitGroup.TemplateMaterial = outfitGroup.Skinned.Count > 0 ? outfitGroup.Skinned[0].Renderer.sharedMaterial : null;

                BakeGroups(new[] { bodyGroup, outfitGroup }, scratch.transform, idleClip, "Assets/Art/VAT/Body");
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

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

            EnsureFolder(folder);

            int frameCount = Mathf.Max(1, Mathf.RoundToInt(clip.length * SampleFps));
            var positions = new Color[totalVerts * frameCount];
            var vatId = new Vector2[totalVerts];
            var vertColor = new Color[totalVerts];
            var combinedUv = new Vector2[totalVerts];
            var combinedNormals = new Vector3[totalVerts];
            var combinedVerts = new Vector3[totalVerts];

            // Per-group vertex range + local triangle list, built while laying out the shared
            // global vertex space (order: group by group, skinned then rigid within each).
            var groupRanges = new (int start, int count)[groups.Length];
            var groupTris = new List<int>[groups.Length];

            int cursor = 0;
            for (int g = 0; g < groups.Length; g++)
            {
                int groupStart = cursor;
                groupTris[g] = new List<int>();

                void LayOut(Mesh mesh, Color color)
                {
                    int count = mesh.vertexCount;
                    var uv0 = mesh.uv;
                    var tris = mesh.triangles;
                    for (int i = 0; i < count; i++)
                    {
                        vatId[cursor + i] = new Vector2((cursor + i + 0.5f) / totalVerts, 0f);
                        combinedUv[cursor + i] = (uv0 != null && i < uv0.Length) ? uv0[i] : Vector2.zero;
                        vertColor[cursor + i] = color;
                    }
                    // Local to this group's own mesh (relative to groupStart), not the global cursor.
                    for (int i = 0; i < tris.Length; i++) groupTris[g].Add(tris[i] + (cursor - groupStart));
                    cursor += count;
                }

                foreach (var s in groups[g].Skinned) LayOut(s.Renderer.sharedMesh, s.VertexColor);
                foreach (var r in groups[g].Rigid) LayOut(r.Mesh, r.VertexColor);

                groupRanges[g] = (groupStart, cursor - groupStart);
            }

            var bakedMesh = new Mesh();
            for (int f = 0; f < frameCount; f++)
            {
                float t = frameCount == 1 ? 0f : clip.length * f / (frameCount - 1);
                clip.SampleAnimation(root.gameObject, t);

                Matrix4x4 rootInverse = root.worldToLocalMatrix;
                int rowBase = f * totalVerts;
                int writeCursor = 0;

                foreach (var group in groups)
                {
                    foreach (var s in group.Skinned)
                    {
                        s.Renderer.BakeMesh(bakedMesh, false);
                        var verts = bakedMesh.vertices;
                        var normals = bakedMesh.normals;
                        Matrix4x4 toRoot = rootInverse * s.Renderer.transform.localToWorldMatrix;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 p = toRoot.MultiplyPoint3x4(verts[i]);
                            positions[rowBase + writeCursor + i] = new Color(p.x, p.y, p.z, 1f);
                            if (f == 0)
                            {
                                combinedVerts[writeCursor + i] = p;
                                combinedNormals[writeCursor + i] = (normals != null && i < normals.Length)
                                    ? toRoot.MultiplyVector(normals[i]).normalized
                                    : Vector3.up;
                            }
                        }
                        writeCursor += verts.Length;
                    }

                    foreach (var r in group.Rigid)
                    {
                        var verts = r.Mesh.vertices;
                        var normals = r.Mesh.normals;
                        Matrix4x4 toRoot = rootInverse * r.Source.localToWorldMatrix;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 p = toRoot.MultiplyPoint3x4(verts[i]);
                            positions[rowBase + writeCursor + i] = new Color(p.x, p.y, p.z, 1f);
                            if (f == 0)
                            {
                                combinedVerts[writeCursor + i] = p;
                                combinedNormals[writeCursor + i] = (normals != null && i < normals.Length)
                                    ? toRoot.MultiplyVector(normals[i]).normalized
                                    : Vector3.up;
                            }
                        }
                        writeCursor += verts.Length;
                    }
                }
            }
            Object.DestroyImmediate(bakedMesh);

            string texPath = $"{folder}/Body_VATPositions.asset";
            var tex = LoadOrCreate<Texture2D>(texPath, () => new Texture2D(totalVerts, frameCount, TextureFormat.RGBAHalf, false, true));
            if (tex.width != totalVerts || tex.height != frameCount)
                tex.Reinitialize(totalVerts, frameCount, TextureFormat.RGBAHalf, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(positions);
            tex.Apply(false, false);
            PersistAsset(tex, texPath);

            string clipSetPath = $"{folder}/Body_VATClipSet.asset";
            var clipSet = LoadOrCreate<VATClipSet>(clipSetPath, ScriptableObject.CreateInstance<VATClipSet>);
            clipSet.PositionTexture = tex;
            clipSet.VertexCount = totalVerts;
            clipSet.TotalFrames = frameCount;
            clipSet.SampleFps = SampleFps;
            clipSet.Idle = new VATClipEntry { ClipName = clip.name, StartFrame = 0, FrameCount = frameCount, Loop = true };
            PersistAsset(clipSet, clipSetPath);

            for (int g = 0; g < groups.Length; g++)
            {
                var (start, count) = groupRanges[g];
                var groupVerts = new Vector3[count];
                var groupNormals = new Vector3[count];
                var groupUv = new Vector2[count];
                var groupColors = new Color[count];
                var groupVatId = new Vector2[count];
                System.Array.Copy(combinedVerts, start, groupVerts, 0, count);
                System.Array.Copy(combinedNormals, start, groupNormals, 0, count);
                System.Array.Copy(combinedUv, start, groupUv, 0, count);
                System.Array.Copy(vertColor, start, groupColors, 0, count);
                System.Array.Copy(vatId, start, groupVatId, 0, count);

                string meshPath = $"{folder}/{groups[g].Name}_VATMesh.asset";
                var vatMesh = LoadOrCreate<Mesh>(meshPath, () => new Mesh());
                vatMesh.Clear();
                if (count > 65535) vatMesh.indexFormat = IndexFormat.UInt32;
                vatMesh.vertices = groupVerts;
                vatMesh.normals = groupNormals;
                vatMesh.uv = groupUv;
                vatMesh.colors = groupColors;
                vatMesh.uv2 = groupVatId;
                vatMesh.triangles = groupTris[g].ToArray();
                vatMesh.RecalculateBounds();
                vatMesh.name = groups[g].Name + "_VATMesh";
                PersistAsset(vatMesh, meshPath);

                string matPath = $"{folder}/{groups[g].Name}_VATMaterial.mat";
                var mat = LoadOrCreate<Material>(matPath, () => new Material(groups[g].Shader));
                if (groups[g].TemplateMaterial != null)
                    EditorUtility.CopySerialized(groups[g].TemplateMaterial, mat);
                mat.shader = groups[g].Shader;
                mat.name = groups[g].Name + "_VATMaterial";
                // Vertex color carries each part's own tone (baked from its own template's
                // _BaseColor) — the merged material's own _BaseColor must be white so it doesn't
                // double-tint on top.
                mat.SetColor("_BaseColor", Color.white);
                // Realtime shadow receiving is pointless overhead for a mobile top-down crowd —
                // this project's own shader already prefers blob-shadow decals over it.
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

            AssetDatabase.SaveAssets();
            Debug.Log($"[VATBaker] Baked {totalVerts} verts x {frameCount} frames across {groups.Length} groups -> {folder}");
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
