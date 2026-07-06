using System;

namespace GNOMES.Runtime {
    /// <summary>
    /// Controls the order in which behaviours run their Update, FixedUpdate,
    /// and LateUpdate methods relative to other behaviours on the same actor.
    /// Lower values run first. Default is 0 if not specified.
    ///
    /// Usage:
    ///   [BehaviourOrder(-100)]   // runs before default-order behaviours
    ///   public class Movement3dBehavior : ActorBehavior { ... }
    ///
    ///   [BehaviourOrder(100)]    // runs after default-order behaviours
    ///   public class AnimationBehavior : ActorBehavior { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class BehaviourOrderAttribute : Attribute
    {
        public int Order { get; }
        public BehaviourOrderAttribute(int order) => Order = order;
    }
}