using UnityEngine;
using UnityEngine.Audio;

namespace GNOMES.Audio {
    /// <summary>
    /// A single soundtrack entry. Create via
    /// Assets → Gnomes → Audio → Music Track.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Music Track",
        menuName  = "Gnomes/Audio/Music Track")]
    public class MusicTrack : ScriptableObject
    {
        public AudioClip Clip;

        [Range(0f, 1f)]
        public float Volume = 1f;

        public bool Loop = true;

        [Tooltip("Seconds to crossfade when transitioning to this track. " +
                 "0 = hard cut.")]
        public float CrossfadeDuration = 1.5f;

        [Tooltip("Optional mixer group for this track.")]
        public AudioMixerGroup MixerGroup;
    }
}