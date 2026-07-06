using System.Collections.Generic;
using System.Linq;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core {
    public class Actor : MonoBehaviour {
        [Header("Settings")] [SerializeField] private ActorBrain DefaultBrain;

        [SerializeField, SerializeReference, Gnomeable] public CameraBehavior DefaultCameraBehavior;

        [SerializeReference, Gnomeable] public List<ActorBehavior> InitialBehaviors = new();

        [SerializeField] public Transform CameraRoot;

        public ActorBrain Brain;
        [SerializeReference, Gnomeable]
        public CameraBehavior ActiveCameraBehavior;
        private List<ActorBehavior> ActiveBehaviors = new();

        private void Start() {
            if (Brain == null && DefaultBrain != null)
                SwapBrain(DefaultBrain);

            //DefaultCameraBehavior ??= new DefaultCameraBehavior();
        }

        private void OnDestroy() {
            foreach (var behavior in ActiveBehaviors)
                behavior.Dispose();

            ActiveBehaviors.Clear();
            Brain?.Unbind();
        }

        public bool PossessedByPlayer() => Brain is PlayerBrain;

        public void SwapBrain(ActorBrain newBrain) {
            if (newBrain == null) {
                Debug.LogWarning($"[Gnomes] Actor.SwapBrain on '{name}': brain is null.");
                return;
            }

            if (newBrain.Parent != null && newBrain.Parent != this)
                newBrain.Parent.RevertToDefault();

            ResetBrain();
            AttachBrain(newBrain);

            foreach (var behavior in InitialBehaviors)
                AddBehavior(behavior);
        }

        public void RevertToDefault() {
            ResetBrain();
            if (DefaultBrain != null)
                AttachBrain(DefaultBrain);
        }

        public void AddBehavior(ActorBehavior behavior) {
            if (behavior == null) return;
            ActiveBehaviors.Add(behavior);

            // Keep the list sorted by BehaviourOrder so Update fires in the
            // right sequence without any per-frame sorting cost.
            ActiveBehaviors.Sort((a, b) =>
                GetBehaviourOrder(a).CompareTo(GetBehaviourOrder(b)));

            behavior.Bind(this);
        }

        private static int GetBehaviourOrder(ActorBehavior behavior) {
            var attr = behavior.GetType()
                .GetCustomAttributes(typeof(BehaviourOrderAttribute), true)
                .FirstOrDefault() as BehaviourOrderAttribute;
            return attr?.Order ?? 0;
        }

        public void RemoveBehavior(ActorBehavior behavior) {
            if (behavior == null) return;
            behavior.Unbind();
            ActiveBehaviors.Remove(behavior);
            behavior.Dispose();
        }

        public T GetBehavior<T>(out T component) where T : class =>
            component = ActiveBehaviors.Find(b => b is T) as T;

        public bool HasBehavior<T>() where T : class =>
            ActiveBehaviors.Exists(b => b is T);

        private void AttachBrain(ActorBrain newBrain) {
            if (newBrain == null) {
                Debug.LogError($"[Gnomes] Actor.AttachBrain on '{name}': brain is null.");
                return;
            }

            if (newBrain is PlayerBrain playerBrain) {
                Brain = playerBrain;
                ActiveCameraBehavior = DefaultCameraBehavior ?? playerBrain.DefaultCameraBehavior;
                ActiveCameraBehavior?.Bind(this);
                Brain.IsInstance = false;
            }
            else {
                Brain = Instantiate(newBrain);
                Brain.IsInstance = true;
            }

            Brain.Bind(this);
            Debug.Log($"[Gnomes] '{name}' attached brain '{newBrain.GetType().Name}'.");
        }

        private void ResetBrain() {
            if (Brain == null) return;

            for (int i = ActiveBehaviors.Count - 1; i >= 0; i--)
                RemoveBehavior(ActiveBehaviors[i]);

            ActiveBehaviors.Clear();
            Brain.Unbind();

            if (Brain.IsInstance)
                Destroy(Brain);

            Brain = null;
            ActiveCameraBehavior = null;
        }

        private void Update() {
            for (int i = 0; i < ActiveBehaviors.Count; i++)
                ActiveBehaviors[i].Update();
            Brain?.Update();
            ActiveCameraBehavior?.Update();
        }

        private void FixedUpdate() {
            for (int i = 0; i < ActiveBehaviors.Count; i++)
                ActiveBehaviors[i].FixedUpdate();
            Brain?.FixedUpdate();
            ActiveCameraBehavior?.FixedUpdate();
        }

        private void LateUpdate() {
            for (int i = 0; i < ActiveBehaviors.Count; i++)
                ActiveBehaviors[i].LateUpdate();
            Brain?.LateUpdate();
            ActiveCameraBehavior?.LateUpdate();
        }
    }
}