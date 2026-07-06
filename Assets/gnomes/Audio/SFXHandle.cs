using UnityEngine;

namespace GNOMES.Audio {
    /// <summary>
    /// A runtime handle to a playing looped SFX. Returned by
    /// <see cref="GnomesSFXManager.PlayLooping"/>. Allows the caller to
    /// stop or adjust the sound without holding a direct AudioSource reference.
    /// </summary>
    public class SFXHandle
    {
        private AudioSource _source;
        private bool        _released;

        internal SFXHandle(AudioSource source) => _source = source;

        public bool IsPlaying =>
            !_released && _source != null && _source.isPlaying;

        public void SetVolume(float volume)
        {
            if (!_released && _source != null)
                _source.volume = Mathf.Clamp01(volume);
        }

        public void Stop()
        {
            if (_released || _source == null) return;
            _source.Stop();
            _released = true;
            // The pool reclaims the source automatically in GnomesSFXManager
        }

        /// <summary>Called by the manager when the source is reclaimed.</summary>
        internal void Invalidate()
        {
            _source   = null;
            _released = true;
        }
    }
}