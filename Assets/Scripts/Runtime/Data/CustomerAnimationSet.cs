using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(menuName = "RestaurantLoop/Customer Animation Set", fileName = "CustomerAnimationSet")]
    public class CustomerAnimationSet : ScriptableObject
    {
        [Header("Looping")]
        public AnimationClip Idle;

        [Header("One-shots")]
        [Tooltip("Played when the final order is served; one is picked at random.")]
        public AnimationClip[] HappyClips;
        [Tooltip("Played when a customer with a second order has its first order served — returns to Idle after.")]
        public AnimationClip FirstOrderServedClip;
        [Tooltip("Reserved for a future 'could not be served' reaction.")]
        public AnimationClip EatFailClip;

        [Header("Crossfade")]
        public float CrossfadeDuration = 0.15f;

        [Header("Playback speed")]
        [Tooltip("Playback speed multiplier for the serve-reaction one-shots (Happy / First Order Served). " +
                 "Higher = reaction plays and finishes faster, so the customer despawns sooner after being served.")]
        public float ServeReactionSpeedMultiplier = 3f;
    }
}
