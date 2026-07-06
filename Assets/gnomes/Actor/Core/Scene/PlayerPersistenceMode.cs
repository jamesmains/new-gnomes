namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Controls how the player actor is handled during a scene transition.
    /// </summary>
    public enum PlayerPersistenceMode
    {
        /// <summary>
        /// The player actor survives the transition via DontDestroyOnLoad
        /// and is physically moved to the spawn point in the new scene.
        /// Best for seamless open-world transitions.
        /// </summary>
        Persist,

        /// <summary>
        /// The player actor is destroyed on unload and recreated fresh in
        /// the new scene at the spawn point. PlayerBrain (ScriptableObject)
        /// is preserved. Best for discrete level transitions.
        /// </summary>
        Respawn,

        /// <summary>
        /// Gnomes does nothing with the player actor. Game code handles it
        /// entirely via the OnBeforeUnload / OnSceneReady hooks.
        /// Best for cutscenes, menus, or full custom control.
        /// </summary>
        Manual
    }
}