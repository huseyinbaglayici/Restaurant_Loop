using System;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Frame ranges of a baked Vertex Animation Texture, written by the VAT baker and read by VATCustomerVisual.
    // Clips without frames (FrameCount 0) report IsValid false.
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
