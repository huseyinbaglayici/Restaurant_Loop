using DG.Tweening;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Landing squash-and-stretch: flattens, overshoots tall, settles back on the current scale.
    public static class SquashTween
    {
        public static Sequence Play(Transform target, float amount = 0.2f, float duration = 0.26f)
        {
            var rest = target.localScale;
            var squash = new Vector3(rest.x * (1f + amount), rest.y * (1f - amount), rest.z * (1f + amount));
            var stretch = new Vector3(rest.x * (1f - amount * 0.4f), rest.y * (1f + amount * 0.5f), rest.z * (1f - amount * 0.4f));

            return DOTween.Sequence()
                .SetTarget(target)
                .Append(target.DOScale(squash, duration * 0.25f).SetEase(Ease.OutQuad))
                .Append(target.DOScale(stretch, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(target.DOScale(rest, duration * 0.45f).SetEase(Ease.OutBack))
                .OnKill(() => { if (target != null) target.localScale = rest; });
        }
    }
}
