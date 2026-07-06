using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GNOMES.Audio {
    /// <summary>
    /// Defines an audio profile for a physical environment — which footstep
    /// clips to use, what ambient loop to play, reverb settings etc.
    ///
    /// Create via Assets → Gnomes → Audio → Audio Environment.
    ///
    /// Access the active environment from anywhere:
    ///   GnomesAudioEnvironment.Current
    ///
    /// Subscribe to changes:
    ///   GnomesAudioEnvironment.OnEnvironmentChanged += OnEnvChanged;
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Audio Environment",
        menuName  = "Gnomes/Audio/Audio Environment")]
    public class GnomesAudioEnvironment : ScriptableObject
    {
        // ── Active environment ────────────────────────────────────────────────

        /// <summary>The currently active environment profile.</summary>
        public static GnomesAudioEnvironment Current { get; private set; }

        /// <summary>Fired when the environment changes. Passes the new profile.</summary>
        public static event Action<GnomesAudioEnvironment> OnEnvironmentChanged;

        internal static void Activate(GnomesAudioEnvironment env)
        {
            if (Current == env) return;
            Current = env;
            OnEnvironmentChanged?.Invoke(env);
        }

        // ── Profile data ──────────────────────────────────────────────────────

        [Header("Ambient")]
        [Tooltip("Looping ambient sound for this environment. Null = silence.")]
        public AudioClip AmbientLoop;

        [Range(0f, 1f)]
        public float AmbientVolume = 0.3f;

        [Tooltip("Optional mixer group for the ambient loop.")]
        public AudioMixerGroup AmbientMixerGroup;

        [Header("Footsteps")]
        [Tooltip("SFX library entries to use for footsteps in this environment. " +
                 "Key examples: Footstep_Walk, Footstep_Run, Footstep_Land.")]
        public SFXLibrary FootstepLibrary;

        [Header("Reverb")]
        [Tooltip("Optional reverb preset for this environment. " +
                 "Assign to an AudioReverbZone or mixer snapshot.")]
        public AudioReverbPreset ReverbPreset = AudioReverbPreset.Off;

        [Header("Music")]
        [Tooltip("Optional music track to trigger when entering this environment. " +
                 "Null = no music change.")]
        public MusicTrack Music;
    }
}