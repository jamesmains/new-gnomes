using System;

namespace GNOMES.Modules {
    /// <summary>
    /// Convenience base class for modules. Provides default no-op
    /// implementations of Reset and ResetIntents so concrete modules only
    /// need to override what they actually have.
    /// </summary>
    [Serializable]
    public abstract class GnomesModule : IBrainModule {
        public virtual void Reset() {
        }

        public virtual void ResetIntents() {
        }
    }
}