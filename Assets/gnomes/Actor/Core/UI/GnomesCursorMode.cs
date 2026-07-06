namespace gnomes.Actor.Core.UI {
    public enum GnomesCursorMode {
        /// <summary>
        /// Cursor is locked to center and hidden — normal gameplay state.
        /// Mouse delta feeds into camera look via input linkers.
        /// </summary>
        LockedHidden,

        /// <summary>
        /// Cursor is visible and free to move — normal UI interaction state.
        /// Mouse delta no longer feeds into camera look because the action
        /// map has been switched away from the gameplay map.
        /// </summary>
        UnlockedVisible,

        /// <summary>
        /// Cursor is visible but confined to the game window.
        /// Useful for world-space UI in windowed mode.
        /// </summary>
        ConfinedVisible
    }
}