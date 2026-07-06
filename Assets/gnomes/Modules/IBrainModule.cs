namespace GNOMES.Modules {
    /// <summary>
    /// Marker interface for all brain modules.
    /// </summary>
    public interface IBrainModule {
        /// <summary>
        /// Clears ALL state — both intent fields and observable state fields.
        /// Called when the actor is fully cleaned up (destroyed, scene unloaded).
        /// </summary>
        void Reset();

        /// <summary>
        /// Clears only intent fields (IntentValue, IntentTrigger) and their
        /// subscribers. Called on every brain swap so stale input doesn't
        /// carry over to a new possession, while observable state (Velocity,
        /// IsGrounded etc.) is preserved for a clean handoff.
        /// </summary>
        void ResetIntents();
    }
}