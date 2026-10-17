using DG.Tweening;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // DOJump arcs along world Y, which on this steep, close camera pushes items through the near plane.
    // Arcing along the camera's up axis instead keeps the view depth constant: the hop reads as "up on screen".
    public static class ArcTween
    {
        public static Tween To(Transform target, Vector3 to, Vector3 arcAxis, float height, float duration)
        {
            // Start point is read when the tween actually starts, so it chains correctly inside sequences.
            Vector3 from = default;
            bool started = false;
            float t = 0f;
            return DOTween.To(() => t, value =>
            {
                if (!started)
                {
                    from = target.position;
                    started = true;
                }

                t = value;
                target.position = Vector3.LerpUnclamped(from, to, value) + arcAxis * (Mathf.Sin(Mathf.Clamp01(value) * Mathf.PI) * height);
            }, 1f, duration).SetTarget(target);
        }
    }
}
