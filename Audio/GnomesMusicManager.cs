// ═════════════════════════════════════════════════════════════════════════════
//  GnomesAudio.cs — Parent House Framework · Gnomes
//  Contains: MusicTrack, SFXLibrary, SFXHandle,
//            GnomesMusicManager, GnomesSFXManager,
//            GnomesAudioEnvironment, GnomesEnvironmentZone
//
//  Design:
//  - GnomesMusicManager     — crossfading soundtrack, ISaveable
//  - GnomesSFXManager       — pooled 2D/3D sfx with looping handle support
//  - SFXLibrary             — ScriptableObject mapping keys → AudioClip[]
//  - GnomesAudioEnvironment — ScriptableObject defining an sfx profile
//  - GnomesEnvironmentZone  — trigger that activates an environment profile
//
//  Both managers are DontDestroyOnLoad singletons. Neither depends on the
//  Actor/Brain system — they are standalone utilities.
// ═════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using GNOMES.Actor.Serialization;
using UnityEngine;

namespace GNOMES.Audio
{
    /// <summary>
    /// Handles crossfading soundtrack playback. Place once in your scene or
    /// let it auto-create itself on first access.
    ///
    /// Usage:
    ///   GnomesMusicManager.Play(myTrack);
    ///   GnomesMusicManager.Stop();
    ///   GnomesMusicManager.SetVolume(0.5f);   // master music volume
    ///
    /// Implements ISaveable so the active track survives save/load.
    /// </summary>
    public class GnomesMusicManager : MonoBehaviour, ISaveable
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static GnomesMusicManager _instance;

        public static GnomesMusicManager Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("[GnomesMusicManager]");
                _instance = go.AddComponent<GnomesMusicManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        // ── State ─────────────────────────────────────────────────────────────

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool        _aIsActive;   // which source is currently "primary"

        private MusicTrack _currentTrack;
        private Coroutine  _crossfadeRoutine;

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

            _sourceA = CreateSource("MusicA");
            _sourceB = CreateSource("MusicB");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions to <paramref name="track"/>. Uses the track's own
        /// CrossfadeDuration — set to 0 for a hard cut.
        /// Safe to call with the currently playing track (no-op).
        /// </summary>
        public static void Play(MusicTrack track)
        {
            if (track == null)
            {
                Debug.LogWarning("[Gnomes] GnomesMusicManager.Play called with null track.");
                return;
            }

            if (Instance._currentTrack == track) return;
            Instance.StartCrossfade(track);
        }

        /// <summary>Fades out and stops all music.</summary>
        public static void Stop(float fadeDuration = 1f) =>
            Instance.StartCoroutine(Instance.FadeOutRoutine(fadeDuration));

        /// <summary>Sets the master music volume (0–1). Affects both sources.</summary>
        public static void SetVolume(float volume)
        {
            Instance._masterVolume = Mathf.Clamp01(volume);
            Instance.ApplyMasterVolume();
        }

        public static MusicTrack CurrentTrack => Instance._currentTrack;

        // ── Internal crossfade ────────────────────────────────────────────────

        private void StartCrossfade(MusicTrack track)
        {
            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);
            
            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(track));
        }

        private IEnumerator CrossfadeRoutine(MusicTrack track)
        {
            
            // Outgoing = currently active source
            // Incoming = the other source
            var outgoing = _aIsActive ? _sourceA : _sourceB;
            var incoming = _aIsActive ? _sourceB : _sourceA;

            float targetVolume = track.Volume * _masterVolume;
            float duration     = track.CrossfadeDuration;

            // Set up incoming source
            incoming.clip         = track.Clip;
            incoming.loop         = track.Loop;
            incoming.volume       = 0f;
            incoming.outputAudioMixerGroup = track.MixerGroup;
            incoming.Play();

            _currentTrack = track;
            _aIsActive    = !_aIsActive;

            if (duration <= 0f)
            {
                // Hard cut
                incoming.volume = targetVolume;
                outgoing.Stop();
                outgoing.volume = 0f;
            }
            else
            {
                float elapsed = 0f;
                float startOutVolume = outgoing.volume;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t  = Mathf.Clamp01(elapsed / duration);

                    incoming.volume = Mathf.Lerp(0f, targetVolume, t);
                    outgoing.volume = Mathf.Lerp(startOutVolume, 0f, t);

                    yield return null;
                }

                outgoing.Stop();
                outgoing.volume = 0f;
                incoming.volume = targetVolume;
            }

            _crossfadeRoutine = null;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            var active = _aIsActive ? _sourceA : _sourceB;
            float start   = active.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed     += Time.unscaledDeltaTime;
                active.volume = Mathf.Lerp(start, 0f, elapsed / duration);
                yield return null;
            }

            active.Stop();
            active.volume = 0f;
            _currentTrack = null;
        }

        private void ApplyMasterVolume()
        {
            if (_currentTrack == null) return;
            var active = _aIsActive ? _sourceA : _sourceB;
            active.volume = _currentTrack.Volume * _masterVolume;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go  = new GameObject(sourceName);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // always 2D for music
            return src;
        }

        // ── ISaveable ─────────────────────────────────────────────────────────

        public Dictionary<string, object> Save() => new()
        {
            ["trackName"]    = _currentTrack?.name ?? "",
            ["masterVolume"] = _masterVolume
        };

        public void Load(Dictionary<string, object> data)
        {
            if (data.TryGetValue("masterVolume", out var vol))
                _masterVolume = Convert.ToSingle(vol);

            // Track restoration requires the caller to provide a lookup —
            // we store the name and let the game code re-trigger Play()
            // with the right MusicTrack asset after load.
            // GnomesMusicManager.LastSavedTrackName is exposed for that purpose.
            if (data.TryGetValue("trackName", out var name))
                LastSavedTrackName = name?.ToString() ?? "";
        }

        /// <summary>
        /// The name of the track that was playing when Save() was called.
        /// Use this after Import() to look up the right MusicTrack asset
        /// and call Play() with it.
        /// </summary>
        public static string LastSavedTrackName { get; private set; } = "";
    }
}