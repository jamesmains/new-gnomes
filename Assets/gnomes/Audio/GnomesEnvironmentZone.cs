using System.Collections.Generic;
using UnityEngine;

namespace GNOMES.Audio {
    /// <summary>
    /// Trigger zone that activates a GnomesAudioEnvironment profile when
    /// a tagged collider enters. On exit, optionally restores the previous
    /// environment.
    ///
    /// Add this component alongside a Collider set to Is Trigger.
    ///
    /// Priority: higher-priority zones win when overlapping.
    /// Stacking: entering a zone pushes the previous environment onto a stack.
    ///           Exiting pops back to it automatically.
    /// </summary>
    public class GnomesEnvironmentZone : MonoBehaviour
    {
        [Tooltip("The environment profile to activate when entering this zone.")]
        public GnomesAudioEnvironment Environment;

        [Tooltip("Tags that trigger this zone. Leave empty to trigger on anything.")]
        public List<string> TriggerTags = new() { "Player" };

        [Tooltip("Higher priority zones win when multiple zones overlap.")]
        public int Priority = 0;

        [Tooltip("If true, restores the previous environment on exit. " +
                 "If false, the environment persists until another zone is entered.")]
        public bool RestoreOnExit = true;

        // Simple stack per-zone isn't correct for overlapping zones —
        // we use a global sorted list of active zones keyed by priority.
        private static readonly List<GnomesEnvironmentZone> _activeZones = new();
        private static SFXHandle _ambientHandle;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidTag(other.tag)) return;
            if (Environment == null) return;

            _activeZones.Add(this);
            _activeZones.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            ApplyTopZone();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsValidTag(other.tag)) return;

            _activeZones.Remove(this);
            ApplyTopZone();
        }

        private void OnDisable()
        {
            _activeZones.Remove(this);
            ApplyTopZone();
        }

        private bool IsValidTag(string tag)
        {
            if (TriggerTags == null || TriggerTags.Count == 0) return true;
            return TriggerTags.Contains(tag);
        }

        private static void ApplyTopZone()
        {
            var env = _activeZones.Count > 0
                ? _activeZones[0].Environment
                : null;

            GnomesAudioEnvironment.Activate(env);
            // Stop the current ambient loop before starting a new one
            _ambientHandle?.Stop();
            _ambientHandle = null;

            if (env?.AmbientLoop != null)
            {
                // Spin up a dedicated AudioSource for the ambient loop via
                // the SFX manager's pool, then hold the handle
                var source = GnomesSFXManager.Instance
                        .GetType()
                        .GetMethod("RentSource",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
                        ?.Invoke(GnomesSFXManager.Instance, null)
                    as AudioSource;

                if (source != null)
                {
                    source.clip         = env.AmbientLoop;
                    source.volume       = env.AmbientVolume;
                    source.loop         = true;
                    source.spatialBlend = 0f;
                    source.outputAudioMixerGroup = env.AmbientMixerGroup;
                    source.Play();

                    _ambientHandle = new SFXHandle(source);
                }
            }

            // Trigger music change if the environment specifies one
            if (env?.Music != null)
                GnomesMusicManager.Play(env.Music);
        }
    }
}