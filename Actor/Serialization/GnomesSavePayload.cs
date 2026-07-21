using System;
using System.Collections.Generic;

// ── Save payload data structures ──────────────────────────────────────────
// Plain serializable classes — Unity's JsonUtility handles the
// serialization, no third-party dependency required.
namespace GNOMES.Actor.Serialization {
    [Serializable]
    internal class GnomesSavePayload
    {
        public string          version = "1.0";
        public List<ActorSave> actors  = new();
    }
}