// ── Attribute ─────────────────────────────────────────────────────────────────
//
// Usage:  [SerializeReference] [Gnomeable] public List<MyBase> items = new();
// Note: [SerializeReference] is still required by Unity at the serialization
// layer — [Gnomeable] cannot replace a core engine attribute. The
// GnomeableValidator (bottom of file) will warn you if you forget it.

using System;
using NUnit.Framework;

namespace GNOMES.Runtime {
    [AttributeUsage(AttributeTargets.Field)]
// ReSharper disable once ClassNeverInstantiated.Global
    public sealed class GnomeableAttribute : PropertyAttribute { }
}