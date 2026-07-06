using System;
using System.Collections.Generic;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// A named group of InteractionOptions. GnomesInteractable holds multiple
    /// sets and switches between them via ActivateSet(name).
    ///
    /// Convention:
    ///   "Default" — options when the object is in its normal state
    ///   "Held"    — options while the object is being carried (Drop etc.)
    ///   Any other name for game-specific states (Locked, Empty, Open etc.)
    /// </summary>
    [Serializable]
    public class InteractionOptionSet
    {
        public string                  Name    = "Default";
        public List<InteractionOption> Options = new();
    }
}