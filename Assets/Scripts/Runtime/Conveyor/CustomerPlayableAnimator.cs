using System;
using System.Collections;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Drives customer idle/reaction clips through a hand-rolled PlayableGraph instead of an
    // Animator Controller — avoids per-instance state-machine evaluation for a large pooled crowd.
    // Graph is built once in Awake and torn down in OnDestroy, independent of pool spawn/despawn.
    public class CustomerPlayableAnimator : MonoBehaviour
    {
        [Inject] private CustomerAnimationSet _clips;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _current;
        private AnimationClipPlayable _incoming;
        private Coroutine _crossfadeRoutine;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _graph = PlayableGraph.Create($"CustomerAnim_{name}");
            _mixer = AnimationMixerPlayable.Create(_graph, 2);

            var output = AnimationPlayableOutput.Create(_graph, "CustomerOutput", _animator);
            output.SetSourcePlayable(_mixer);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        public void PlayIdleWithRandomOffset()
        {
            if (!_graph.IsValid() || _clips.Idle == null) return;

            StopCrossfade();
            float speed = UnityEngine.Random.Range(0.95f, 1.05f);
            float startTime = UnityEngine.Random.Range(0f, _clips.Idle.length);
            SetCurrentClip(_clips.Idle, startTime, speed);
            _graph.Play();
        }

        // hasSecondOrderRemaining == true: first-of-two order served, plays and returns to Idle.
        // hasSecondOrderRemaining == false: final order served, plays a random happy clip then invokes onComplete.
        public void PlayServeReaction(bool hasSecondOrderRemaining, Action onComplete)
        {
            if (!_graph.IsValid())
            {
                onComplete?.Invoke();
                return;
            }

            AnimationClip clip = hasSecondOrderRemaining
                ? _clips.FirstOrderServedClip
                : PickRandomHappyClip();

            if (clip == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopCrossfade();
            _crossfadeRoutine = StartCoroutine(CrossfadeAndReactRoutine(clip, hasSecondOrderRemaining, onComplete));
        }

        private AnimationClip PickRandomHappyClip()
        {
            var happy = _clips.HappyClips;
            if (happy == null || happy.Length == 0) return null;
            return happy[UnityEngine.Random.Range(0, happy.Length)];
        }

        private IEnumerator CrossfadeAndReactRoutine(AnimationClip clip, bool returnToIdleAfter, Action onComplete)
        {
            float speed = Mathf.Max(0.01f, _clips.ServeReactionSpeedMultiplier);
            yield return CrossfadeToClip(clip, speed);

            float playDuration = clip.length / speed;
            yield return new WaitForSeconds(Mathf.Max(0f, playDuration - _clips.CrossfadeDuration));

            // clear before PlayIdleWithRandomOffset() — it calls StopCrossfade(), which would
            // otherwise call StopCoroutine() on this very routine from inside itself.
            _crossfadeRoutine = null;

            if (returnToIdleAfter) PlayIdleWithRandomOffset();

            onComplete?.Invoke();
        }

        private IEnumerator CrossfadeToClip(AnimationClip clip, float speed)
        {
            _incoming = AnimationClipPlayable.Create(_graph, clip);
            _incoming.SetSpeed(speed);
            _mixer.DisconnectInput(1);
            _mixer.ConnectInput(1, _incoming, 0);

            float duration = Mathf.Max(0.0001f, _clips.CrossfadeDuration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float w = Mathf.Clamp01(t / duration);
                _mixer.SetInputWeight(0, 1f - w);
                _mixer.SetInputWeight(1, w);
                yield return null;
            }

            SwapCurrentToIncoming();
        }

        private void SetCurrentClip(AnimationClip clip, float startTime, float speed)
        {
            if (_current.IsValid()) _mixer.DisconnectInput(0);

            _current = AnimationClipPlayable.Create(_graph, clip);
            _current.SetTime(startTime);
            _current.SetSpeed(speed);
            _mixer.ConnectInput(0, _current, 0);
            _mixer.SetInputWeight(0, 1f);
            _mixer.SetInputWeight(1, 0f);
        }

        private void SwapCurrentToIncoming()
        {
            if (_current.IsValid()) _current.Destroy();

            _current = _incoming;
            _incoming = default;

            _mixer.DisconnectInput(0);
            _mixer.DisconnectInput(1);
            _mixer.ConnectInput(0, _current, 0);
            _mixer.SetInputWeight(0, 1f);
            _mixer.SetInputWeight(1, 0f);
        }

        private void StopCrossfade()
        {
            if (_crossfadeRoutine == null) return;
            StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = null;

            if (_incoming.IsValid())
            {
                _mixer.DisconnectInput(1);
                _incoming.Destroy();
                _incoming = default;
            }
        }
    }
}
