// ── GnomesWalkoverTrigger ─────────────────────────────────────────────────

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// Trigger zone that fires callbacks when specific actors enter, stay,
    /// or exit. Supports an optional dwell time with a readable progress value.
    ///
    /// Setup:
    ///   - Add alongside a Collider set to Is Trigger
    ///   - Set AllowedTags or AllowedActors to filter who can trigger it
    ///   - Set DwellTime > 0 for hold-to-activate behaviour
    ///   - Subscribe to OnEnter, OnExit, OnComplete, OnProgressChanged
    ///   - Or wire up the UnityEvent equivalents in the inspector
    ///
    /// Progress (0–1) is readable at any time via the Progress property.
    /// Use it to drive UI progress rings, door charge-up bars etc.
    ///
    /// If multiple actors are in the zone simultaneously, the first to enter
    /// owns the dwell timer. Configure MultiActorMode for different behaviour.
    /// </summary>
    public class GnomesWalkoverTrigger : MonoBehaviour
    {
        // ── Settings ──────────────────────────────────────────────────────────
 
        [Header("Filter")]
        [Tooltip("Tags that can trigger this zone. Empty = any tag.")]
        public List<string> AllowedTags = new() { "Player" };
 
        [Tooltip("Specific actors that can trigger this zone. " +
                 "If set, takes priority over AllowedTags.")]
        public List<Actor> AllowedActors = new();
 
        [Header("Dwell")]
        [Tooltip("Seconds the actor must remain in the zone before OnComplete fires. " +
                 "0 = fire immediately on enter.")]
        public float DwellTime = 0f;
 
        [Tooltip("If true, progress resets when the actor exits before completing.")]
        public bool ResetOnExit = true;
 
        [Tooltip("If true, OnComplete can fire again after the actor exits and re-enters.")]
        public bool Repeatable = true;
 
        //[Header("Multi-actor")]
        public enum MultiActorBehaviour
        {
            FirstOnly,   // only the first actor to enter can trigger
            Any,         // any qualifying actor can trigger independently
        }
        public MultiActorBehaviour MultiActorMode = MultiActorBehaviour.FirstOnly;
 
        // ── Events ────────────────────────────────────────────────────────────
 
        /// <summary>Fired when a qualifying actor enters the zone.</summary>
        public event Action<Actor> OnEnter;
        /// <summary>Fired when a qualifying actor exits the zone.</summary>
        public event Action<Actor> OnExit;
        /// <summary>Fired when dwell time completes (or immediately if DwellTime == 0).</summary>
        public event Action<Actor> OnComplete;
        /// <summary>Fired every frame while an actor is dwelling. Value is 0–1.</summary>
        public event Action<Actor, float> OnProgressChanged;
 
        public UnityEvent<Actor>        OnEnterEvent;
        public UnityEvent<Actor>        OnExitEvent;
        public UnityEvent<Actor>        OnCompleteEvent;
 
        // ── Runtime ───────────────────────────────────────────────────────────
 
        /// <summary>Current dwell progress (0–1). 1 = complete.</summary>
        public float Progress { get; private set; }
 
        /// <summary>The actor currently dwelling in this zone, if any.</summary>
        public Actor DwellingActor { get; private set; }
 
        private readonly HashSet<Actor> _actorsInZone = new();
        private Coroutine               _dwellRoutine;
        private bool                    _completed;
 
        // ── Trigger callbacks ─────────────────────────────────────────────────
 
        private void OnTriggerEnter(Collider other)
        {
            var actor = other.GetComponentInParent<Actor>();
            if (!IsQualified(actor)) return;
            if (_actorsInZone.Contains(actor)) return;
 
            _actorsInZone.Add(actor);
 
            if (MultiActorMode == MultiActorBehaviour.FirstOnly &&
                DwellingActor != null && DwellingActor != actor)
                return;
 
            OnEnter?.Invoke(actor);
            OnEnterEvent?.Invoke(actor);
 
            if (_completed && !Repeatable) return;
 
            if (DwellTime <= 0f)
            {
                CompleteFor(actor);
            }
            else
            {
                if (_dwellRoutine != null) StopCoroutine(_dwellRoutine);
                _dwellRoutine = StartCoroutine(DwellRoutine(actor));
            }
        }
 
        private void OnTriggerExit(Collider other)
        {
            var actor = other.GetComponentInParent<Actor>();
            if (actor == null || !_actorsInZone.Contains(actor)) return;
 
            _actorsInZone.Remove(actor);
 
            OnExit?.Invoke(actor);
            OnExitEvent?.Invoke(actor);
 
            if (DwellingActor == actor)
            {
                if (_dwellRoutine != null)
                {
                    StopCoroutine(_dwellRoutine);
                    _dwellRoutine = null;
                }
 
                DwellingActor = null;
 
                if (ResetOnExit)
                {
                    Progress = 0f;
                    OnProgressChanged?.Invoke(actor, Progress);
                }
            }
        }
 
        // ── Dwell coroutine ───────────────────────────────────────────────────
 
        private IEnumerator DwellRoutine(Actor actor)
        {
            DwellingActor = actor;
            _completed    = false;
 
            float elapsed = Progress * DwellTime;   // resume from current progress
 
            while (elapsed < DwellTime)
            {
                elapsed  += Time.deltaTime;
                Progress  = Mathf.Clamp01(elapsed / DwellTime);
                OnProgressChanged?.Invoke(actor, Progress);
                yield return null;
            }
 
            Progress = 1f;
            CompleteFor(actor);
            _dwellRoutine = null;
        }
 
        private void CompleteFor(Actor actor)
        {
            _completed    = true;
            DwellingActor = null;
            Progress      = 1f;
 
            OnComplete?.Invoke(actor);
            OnCompleteEvent?.Invoke(actor);
 
            if (Repeatable)
            {
                Progress   = 0f;
                _completed = false;
            }
        }
 
        // ── Filter helpers ────────────────────────────────────────────────────
 
        private bool IsQualified(Actor actor)
        {
            if (actor == null) return false;
 
            if (AllowedActors.Count > 0)
                return AllowedActors.Contains(actor);
 
            if (AllowedTags.Count > 0)
                return AllowedTags.Contains(actor.tag);
 
            return true;
        }
 
        // ── Editor gizmo ──────────────────────────────────────────────────────
 
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.15f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.6f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
#endif
    }
}