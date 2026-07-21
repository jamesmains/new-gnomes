using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GNOMES.Audio {
    /// <summary>
    /// Pooled SFX playback manager. Handles both fire-and-forget and
    /// looping sounds via SFXHandle.
    ///
    /// Usage:
    ///   // Fire-and-forget 2D
    ///   GnomesSFXManager.Play(myLibrary, "UI_Click");
    ///
    ///   // Fire-and-forget 3D at position
    ///   GnomesSFXManager.PlayAt(myLibrary, "Footstep_Stone", transform.position);
    ///
    ///   // Direct clip 3D
    ///   GnomesSFXManager.PlayAt(clip, transform.position, volume: 0.8f);
    ///
    ///   // Looping with handle
    ///   var handle = GnomesSFXManager.PlayLooping(myLibrary, "Engine_Loop", transform);
    ///   handle.SetVolume(0.5f);
    ///   handle.Stop();
    /// </summary>
    public class GnomesSFXManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static GnomesSFXManager _instance;

        public static GnomesSFXManager Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("[GnomesSFXManager]");
                _instance = go.AddComponent<GnomesSFXManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        // ── Pool ──────────────────────────────────────────────────────────────

        private const int InitialPoolSize = 16;

        private readonly Queue<AudioSource>      _pool        = new();
        private readonly List<AudioSource>       _active      = new();
        private readonly List<SFXHandle>         _handles     = new();
        private readonly List<AudioSource>       _handleSrcs  = new();

        [Range(0f, 1f)]
        private float _masterVolume = 1f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < InitialPoolSize; i++)
                _pool.Enqueue(CreatePooledSource());
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Reclaim finished one-shot sources back into the pool
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] == null || _active[i].isPlaying) continue;
                ReturnToPool(_active[i]);
                _active.RemoveAt(i);
            }

            // Reclaim stopped looping sources
            for (int i = _handleSrcs.Count - 1; i >= 0; i--)
            {
                if (_handleSrcs[i] == null || _handleSrcs[i].isPlaying) continue;
                _handles[i].Invalidate();
                ReturnToPool(_handleSrcs[i]);
                _handles   .RemoveAt(i);
                _handleSrcs.RemoveAt(i);
            }
        }

        // ── Public API — library-based ────────────────────────────────────────

        /// <summary>Plays a 2D fire-and-forget sound from a library key.</summary>
        public static void Play(SFXLibrary library, string key) =>
            Instance.PlayInternal(library, key, Vector3.zero, false, null);

        /// <summary>Plays a 3D fire-and-forget sound at a world position.</summary>
        public static void PlayAt(
            SFXLibrary library, string key, Vector3 position) =>
            Instance.PlayInternal(library, key, position, true, null);

        /// <summary>
        /// Plays a looped sound from a library key, optionally following a transform.
        /// Returns a handle to stop or adjust the sound.
        /// </summary>
        public static SFXHandle PlayLooping(
            SFXLibrary library, string key,
            Transform follow = null)
        {
            var entry = library?.Get(key);
            if (entry?.Clips == null || entry.Clips.Length == 0)
            {
                Debug.LogWarning($"[Gnomes] SFXLibrary key '{key}' not found.");
                return null;
            }

            var clip   = entry.Clips[Random.Range(0, entry.Clips.Length)];
            var source = Instance.RentSource();

            source.clip   = clip;
            source.volume = entry.Volume * Instance._masterVolume;
            source.loop   = true;
            source.outputAudioMixerGroup = entry.MixerGroup;
            source.spatialBlend = follow != null ? 1f : 0f;
            source.Play();

            if (follow != null)
                Instance.StartCoroutine(
                    Instance.FollowTransform(source, follow));

            var handle = new SFXHandle(source);
            Instance._handles   .Add(handle);
            Instance._handleSrcs.Add(source);
            return handle;
        }

        // ── Public API — direct clip ──────────────────────────────────────────

        /// <summary>Plays a clip directly (2D).</summary>
        public static void Play(
            AudioClip clip, float volume = 1f,
            AudioMixerGroup mixerGroup = null) =>
            Instance.PlayClipInternal(clip, Vector3.zero, false, volume, mixerGroup);

        /// <summary>Plays a clip at a world position (3D).</summary>
        public static void PlayAt(
            AudioClip clip, Vector3 position,
            float volume = 1f,
            AudioMixerGroup mixerGroup = null) =>
            Instance.PlayClipInternal(clip, position, true, volume, mixerGroup);

        /// <summary>Sets the master SFX volume (0–1).</summary>
        public static void SetVolume(float volume) =>
            Instance._masterVolume = Mathf.Clamp01(volume);

        // ── Internal ──────────────────────────────────────────────────────────

        private void PlayInternal(
            SFXLibrary library, string key,
            Vector3 position, bool spatial,
            AudioMixerGroup mixerGroupOverride)
        {
            var entry = library?.Get(key);
            if (entry?.Clips == null || entry.Clips.Length == 0)
            {
                Debug.LogWarning(
                    $"[Gnomes] SFXLibrary: key '{key}' not found or has no clips.");
                return;
            }

            var clip   = entry.Clips[Random.Range(0, entry.Clips.Length)];
            var group  = mixerGroupOverride ?? entry.MixerGroup;
            PlayClipInternal(
                clip, position, spatial,
                entry.Volume * _masterVolume, group);
        }

        private void PlayClipInternal(
            AudioClip clip, Vector3 position, bool spatial,
            float volume, AudioMixerGroup mixerGroup)
        {
            if (clip == null) return;

            var source = RentSource();
            source.clip         = clip;
            source.volume       = Mathf.Clamp01(volume * _masterVolume);
            source.loop         = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.outputAudioMixerGroup = mixerGroup;

            if (spatial)
                source.transform.position = position;

            source.Play();
            _active.Add(source);
        }

        private IEnumerator FollowTransform(AudioSource source, Transform target)
        {
            while (source != null && source.isPlaying && target != null)
            {
                source.transform.position = target.position;
                yield return null;
            }
        }

        /// <summary>
        /// Plays a clip as a looping ambient source and returns an SFXHandle.
        /// Used internally by GnomesEnvironmentZone.
        /// </summary>
        internal static SFXHandle PlayAmbient(
            AudioClip clip, float volume,
            AudioMixerGroup mixerGroup)
        {
            if (clip == null) return null;

            var source = Instance.RentSource();
            source.clip         = clip;
            source.volume       = Mathf.Clamp01(volume * Instance._masterVolume);
            source.loop         = true;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = mixerGroup;
            source.Play();

            var handle = new SFXHandle(source);
            Instance._handles   .Add(handle);
            Instance._handleSrcs.Add(source);
            return handle;
        }

        private AudioSource RentSource()
        {
            if (_pool.Count > 0) return _pool.Dequeue();

            // Pool exhausted — grow it
            Debug.Log("[Gnomes] SFX pool exhausted — growing.");
            return CreatePooledSource();
        }

        private void ReturnToPool(AudioSource source)
        {
            source.Stop();
            source.clip  = null;
            source.loop  = false;
            source.transform.SetParent(transform);
            source.transform.localPosition = Vector3.zero;
            _pool.Enqueue(source);
        }

        private AudioSource CreatePooledSource()
        {
            var go  = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            return src;
        }
    }
}