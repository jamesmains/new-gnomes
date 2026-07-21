// ═════════════════════════════════════════════════════════════════════════════
//  GnomesSaveSystem.cs — Parent House Framework · Gnomes
//  Contains: ISaveable, GnomesIdentity, GnomesSaveSystem
//
//  Design:
//  - Modules opt in to persistence by implementing ISaveable
//  - GnomesIdentity gives every actor a stable GUID
//  - GnomesSaveSystem.Export() returns a JSON string
//  - GnomesSaveSystem.Import(string) restores state from that string
//  - The framework owns the data structure, not the storage medium —
//    write the string wherever makes sense for your game
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;

namespace GNOMES.Actor.Serialization
{
    // ── ISaveable ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opt-in interface for modules that want their state persisted.
    /// Implement on any GnomesModule subclass.
    ///
    /// Save() should return only the fields that need to persist between
    /// sessions — transient physics state (Velocity, IsGrounded etc.)
    /// should be excluded.
    ///
    /// Load() receives exactly what Save() returned and should restore
    /// the module to that state. Called after the actor is fully spawned
    /// and its brain is bound.
    ///
    /// Example:
    ///   public Dictionary&lt;string, object&gt; Save() =>
    ///       new() { ["health"] = Health, ["maxHealth"] = MaxHealth };
    ///
    ///   public void Load(Dictionary&lt;string, object&gt; data)
    ///   {
    ///       Health    = Convert.ToSingle(data["health"]);
    ///       MaxHealth = Convert.ToSingle(data["maxHealth"]);
    ///   }
    /// </summary>
    public interface ISaveable
    {
        Dictionary<string, object> Save();
        void Load(Dictionary<string, object> data);
    }
}