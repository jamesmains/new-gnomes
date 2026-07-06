using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace GNOMES.Audio {
    /// <summary>
    /// Maps string keys to arrays of AudioClips. On play, a random clip
    /// from the array is selected — easy variation without code changes.
    ///
    /// Create via Assets → Gnomes → Audio → SFX Library.
    ///
    /// Usage:
    ///   GnomesSFXManager.Play(myLibrary, "Footstep_Stone");
    /// </summary>
    [CreateAssetMenu(
        fileName = "New SFX Library",
        menuName  = "Gnomes/Audio/SFX Library")]
    public class SFXLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string      Key;
            public AudioClip[] Clips;

            [Range(0f, 1f)]
            public float Volume = 1f;

            [Tooltip("Optional mixer group override for this entry.")]
            public AudioMixerGroup MixerGroup;
        }

        public List<Entry> Entries = new();

        private Dictionary<string, Entry> _cache;

        public Entry Get(string key)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, Entry>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var e in Entries)
                    if (!string.IsNullOrEmpty(e.Key))
                        _cache[e.Key] = e;
            }

            _cache.TryGetValue(key, out var entry);
            return entry;
        }

        public AudioClip GetClip(string key)
        {
            var entry = Get(key);
            if (entry?.Clips == null || entry.Clips.Length == 0) return null;
            return entry.Clips[Random.Range(0, entry.Clips.Length)];
        }
    }
}