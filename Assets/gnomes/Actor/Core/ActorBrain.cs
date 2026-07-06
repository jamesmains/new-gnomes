using System;
using System.Collections.Generic;
using System.Linq;
using GNOMES.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core {
    [Serializable]
    [CreateAssetMenu(fileName = "New Empty Brain", menuName = "Gnomes/Brains/Empty Brain")]
    public class ActorBrain : ScriptableObject {
        // ── Inspector — persistent asset-level config ─────────────────────────

        [Header("Modules")] [SerializeReference, Gnomeable]
        public List<IBrainModule> Modules = new();

        // ── Runtime state ─────────────────────────────────────────────────────

        [HideInInspector] public Actor Parent;
        [HideInInspector] public bool IsInstance;

        // ── Module access ─────────────────────────────────────────────────────

        public T GetModule<T>() where T : class, IBrainModule =>
            Modules.OfType<T>().FirstOrDefault();

        public bool HasModule<T>() where T : class =>
            Modules.Any(m => m is T);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public virtual void Bind(Actor actor) {
            Parent = actor;

            // On bind, clear only intent fields so the incoming brain starts
            // with a clean input slate. State fields (Velocity, IsGrounded etc.)
            // are preserved — the new brain sees correct physics state immediately.
            foreach (var module in Modules) {
                try {
                    module.ResetIntents();
                }
                catch (Exception e) {
                    Debug.LogError(
                        $"[Gnomes] {module.GetType().Name}.ResetIntents() threw: {e.Message}");
                }
            }
        }

        public virtual void Unbind() {
            // Full teardown — clears everything including state and subscribers.
            foreach (var module in Modules) {
                try {
                    module.Reset();
                }
                catch (Exception e) {
                    Debug.LogError(
                        $"[Gnomes] {module.GetType().Name}.Reset() threw: {e.Message}");
                }
            }

            Parent = null;
        }

        public virtual void Update() {
        }

        public virtual void FixedUpdate() {
        }

        public virtual void LateUpdate() {
        }
    }
}