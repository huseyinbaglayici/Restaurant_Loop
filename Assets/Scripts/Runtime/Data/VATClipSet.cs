using System;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Frame-range layout of a baked Vertex Animation Texture. Produced by Assets/Editor/VAT/VATBaker.cs,
    // consumed by VATCustomerVisual at runtime. Only Idle is populated today — happy_idle_beko.controller
    // has no reaction clips yet, so the other entries stay invalid (FrameCount 0) until real clips exist.
    [CreateAssetMenu(menuName = "RestaurantLoop/VAT Clip Set", fileName = "VATClipSet")]
    public class VATClipSet : ScriptableObject
    {
        public Texture2D PositionTexture;
        public int VertexCount;
        public int TotalFrames;
        public float SampleFps = 30f;

        public VATClipEntry Idle;
        public VATClipEntry[] HappyClips;
        public VATClipEntry FirstOrderServedClip;
        public VATClipEntry EatFailClip;

        public VATClipEntry PickRandomHappyClip()
        {
            if (HappyClips == null || HappyClips.Length == 0) return default;
            return HappyClips[UnityEngine.Random.Range(0, HappyClips.Length)];
        }
    }

    [Serializable]
    public struct VATClipEntry
    {
        public string ClipName;
        public int StartFrame;
        public int FrameCount;
        public bool Loop;

        public readonly bool IsValid => FrameCount > 0;
        public readonly float Duration(float sampleFps) => sampleFps > 0f ? FrameCount / sampleFps : 0f;
    }
}
