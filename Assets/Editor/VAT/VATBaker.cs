using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace RestaurantLoop.Editor.VAT
{
    // Bakes the live customer rig (Assets/Art/Models/Character/Customer.prefab) into position-only
    // Vertex Animation Textures. Two bakes: the 5 skinned body parts + the rigid Eyebrow merged into
    // one mesh/texture (each part's flat material color baked into vertex COLOR so one shared
    // material reproduces the look — see Candyshader's _VERTEXCOLOR_ON), and each of the 5 rigid
    // hair variants baked separately (same clip/frame layout, so one instance's playback drives both
    // its body and its active hair renderer). See VATCustomerVisual for runtime playback.
    public static class VATBaker
    {
        private const float SampleFps = 30f;
        private const string CustomerPrefabPath = "Assets/Art/Models/Character/Customer.prefab";
        private static readonly string[] BodyPartNames = { "body", "eyes", "pants", "shoes", "tshirt" };
        private static readonly string[] HairNames = { "men_hair_1", "men_hair_2", "men_hair_3", "women_hair_1", "women_hair_2" };
        private const string EyebrowName = "Eyebrow";

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

        [MenuItem("RestaurantLoop/VAT/Bake Customer Body + Eyebrow")]
        public static void BakeBody()
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (sourcePrefab == null) { Debug.LogError($"[VATBaker] {CustomerPrefabPath} not found."); return; }

            var idleClip = FindIdleClip(sourcePrefab);
            if (idleClip == null) return;

            var scratch = Object.Instantiate(sourcePrefab);
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var skinnedSources = new List<SkinnedSource>();
                foreach (var partName in BodyPartNames)
                {
                    var t = FindDeepChild(scratch.transform, partName);
                    var smr = t != null ? t.GetComponent<SkinnedMeshRenderer>() : null;
                    if (smr == null || smr.sharedMesh == null)
                    {
                        Debug.LogError($"[VATBaker] Missing skinned body part '{partName}'.");
                        continue;
                    }
                    skinnedSources.Add(new SkinnedSource { Renderer = smr, VertexColor = GetFlatColor(smr.sharedMaterial) });
                }

                var rigidSources = new List<RigidSource>();
                var eyebrowT = FindDeepChild(scratch.transform, EyebrowName);
                var eyebrowMf = eyebrowT != null ? eyebrowT.GetComponent<MeshFilter>() : null;
                if (eyebrowMf != null && eyebrowMf.sharedMesh != null)
                {
                    var eyebrowMr = eyebrowT.GetComponent<MeshRenderer>();
                    rigidSources.Add(new RigidSource
                    {
                        Source = eyebrowT,
                        Mesh = eyebrowMf.sharedMesh,
                        VertexColor = GetFlatColor(eyebrowMr != null ? eyebrowMr.sharedMaterial : null)
                    });
                }
                else
                {
                    Debug.LogWarning("[VATBaker] Eyebrow mesh not found, baking body without it.");
                }

                BakeCombined("Body", scratch.transform, skinnedSources, rigidSources, idleClip, "Assets/Art/VAT/Body");
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        [MenuItem("RestaurantLoop/VAT/Bake Customer Hair Variants")]
        public static void BakeAllHair()
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (sourcePrefab == null) { Debug.LogError($"[VATBaker] {CustomerPrefabPath} not found."); return; }

            var idleClip = FindIdleClip(sourcePrefab);
            if (idleClip == null) return;

            foreach (var hairName in HairNames) BakeHairVariant(sourcePrefab, idleClip, hairName);
        }

        private static void BakeHairVariant(GameObject sourcePrefab, AnimationClip idleClip, string hairName)
        {
            var scratch = Object.Instantiate(sourcePrefab);
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var hairT = FindDeepChild(scratch.transform, hairName);
                var mf = hairT != null ? hairT.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogError($"[VATBaker] Missing hair variant '{hairName}'.");
                    return;
                }

                var rigidSources = new List<RigidSource> { new RigidSource { Source = hairT, Mesh = mf.sharedMesh, VertexColor = Color.white } };
                BakeCombined(hairName, scratch.transform, new List<SkinnedSource>(), rigidSources, idleClip, $"Assets/Art/VAT/Hair/{hairName}");
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        private static VATClipSet BakeCombined(string bakeName, Transform root, List<SkinnedSource> skinnedSources,
            List<RigidSource> rigidSources, AnimationClip clip, string folder)
        {
            int totalVerts = 0;
            foreach (var s in skinnedSources) totalVerts += s.Renderer.sharedMesh.vertexCount;
            foreach (var r in rigidSources) totalVerts += r.Mesh.vertexCount;

            if (totalVerts == 0)
            {
                Debug.LogError($"[VATBaker] {bakeName}: no source vertices to bake.");
                return null;
            }

            EnsureFolder(folder);

            int frameCount = Mathf.Max(1, Mathf.RoundToInt(clip.length * SampleFps));
            var positions = new Color[totalVerts * frameCount];
            var vatId = new Vector2[totalVerts];
            var vertColor = new Color[totalVerts];
            var combinedUv = new Vector2[totalVerts];
            var combinedNormals = new Vector3[totalVerts];
            var combinedVerts = new Vector3[totalVerts];
            var combinedTris = new List<int>();

            // Static topology (uv0/triangles/vertex color) doesn't change per frame — build once.
            int cursor = 0;
            foreach (var s in skinnedSources)
            {
                var mesh = s.Renderer.sharedMesh;
                int count = mesh.vertexCount;
                var uv0 = mesh.uv;
                var tris = mesh.triangles;
                for (int i = 0; i < count; i++)
                {
                    vatId[cursor + i] = new Vector2((cursor + i + 0.5f) / totalVerts, 0f);
                    combinedUv[cursor + i] = (uv0 != null && i < uv0.Length) ? uv0[i] : Vector2.zero;
                    vertColor[cursor + i] = s.VertexColor;
                }
                for (int i = 0; i < tris.Length; i++) combinedTris.Add(tris[i] + cursor);
                cursor += count;
            }
            foreach (var r in rigidSources)
            {
                var mesh = r.Mesh;
                int count = mesh.vertexCount;
                var uv0 = mesh.uv;
                var tris = mesh.triangles;
                for (int i = 0; i < count; i++)
                {
                    vatId[cursor + i] = new Vector2((cursor + i + 0.5f) / totalVerts, 0f);
                    combinedUv[cursor + i] = (uv0 != null && i < uv0.Length) ? uv0[i] : Vector2.zero;
                    vertColor[cursor + i] = r.VertexColor;
                }
                for (int i = 0; i < tris.Length; i++) combinedTris.Add(tris[i] + cursor);
                cursor += count;
            }

            var bakedMesh = new Mesh();
            for (int f = 0; f < frameCount; f++)
            {
                float t = frameCount == 1 ? 0f : clip.length * f / (frameCount - 1);
                clip.SampleAnimation(root.gameObject, t);

                Matrix4x4 rootInverse = root.worldToLocalMatrix;
                int rowBase = f * totalVerts;
                int writeCursor = 0;

                foreach (var s in skinnedSources)
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

                foreach (var r in rigidSources)
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
            Object.DestroyImmediate(bakedMesh);

            string texPath = $"{folder}/{bakeName}_VATPositions.asset";
            var tex = LoadOrCreate<Texture2D>(texPath, () => new Texture2D(totalVerts, frameCount, TextureFormat.RGBAHalf, false, true));
            if (tex.width != totalVerts || tex.height != frameCount)
                tex.Reinitialize(totalVerts, frameCount, TextureFormat.RGBAHalf, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(positions);
            tex.Apply(false, false);
            PersistAsset(tex, texPath);

            string meshPath = $"{folder}/{bakeName}_VATMesh.asset";
            var vatMesh = LoadOrCreate<Mesh>(meshPath, () => new Mesh());
            vatMesh.Clear();
            if (totalVerts > 65535) vatMesh.indexFormat = IndexFormat.UInt32;
            vatMesh.vertices = combinedVerts;
            vatMesh.normals = combinedNormals;
            vatMesh.uv = combinedUv;
            vatMesh.colors = vertColor;
            vatMesh.uv2 = vatId;
            vatMesh.triangles = combinedTris.ToArray();
            vatMesh.RecalculateBounds();
            vatMesh.name = bakeName + "_VATMesh";
            PersistAsset(vatMesh, meshPath);

            string clipSetPath = $"{folder}/{bakeName}_VATClipSet.asset";
            var clipSet = LoadOrCreate<VATClipSet>(clipSetPath, ScriptableObject.CreateInstance<VATClipSet>);
            clipSet.PositionTexture = tex;
            clipSet.VertexCount = totalVerts;
            clipSet.TotalFrames = frameCount;
            clipSet.SampleFps = SampleFps;
            clipSet.Idle = new VATClipEntry { ClipName = clip.name, StartFrame = 0, FrameCount = frameCount, Loop = true };
            PersistAsset(clipSet, clipSetPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[VATBaker] Baked {bakeName}: {totalVerts} verts x {frameCount} frames -> {clipSetPath}");
            return clipSet;
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
