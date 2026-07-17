using System;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core {
    
    /// <summary>
    /// This is just a bandaid solution, but also use as a reference for a long term solution
    /// </summary>
    public class BehaviorCommunicator: MonoBehaviour {
        [SerializeReference] private Actor TargetActor;
        [Gnomeable,SerializeReference]
        public ActorBehavior TargetType;

        private ActorBehavior _cachedBehaviorReference;

        private void Awake() {
            TargetActor ??= GetComponent<Actor>();
        }

        private bool ValidateType() {
            if (_cachedBehaviorReference != null) {
                return true;
            }
            ActorBehavior behavior = TargetActor.GetBehavior(TargetType.GetType());
            if (behavior == null) return false;
            _cachedBehaviorReference = behavior;
            return true;
        }

        public void SendData(int value) {
            if (TargetActor == null || !ValidateType()) return;
            _cachedBehaviorReference.SendData(value);
        }

        public void SendData(Actor actor) {
            if (TargetActor == null || !ValidateType()) return;
            _cachedBehaviorReference.SendData(actor);
        }

        public void SendData(float value) {
            if (TargetActor == null || !ValidateType()) return;
            _cachedBehaviorReference.SendData(value);
        }
    }
}