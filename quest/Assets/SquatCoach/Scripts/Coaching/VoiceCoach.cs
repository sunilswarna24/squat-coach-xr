using System.Collections.Generic;
using UnityEngine;

namespace SquatCoach.Coaching
{
    /// <summary>
    /// Plays coach cues as pre-rendered audio clips. Clips live under
    /// <c>Resources/VoiceCues/&lt;key&gt;/NN.wav</c> and are generated
    /// offline by <c>quest/tools/generate_voice_cues.sh</c> — this means
    /// we don't depend on Android's text-to-speech engine, which the
    /// Meta Quest 3 does not ship with.
    ///
    /// Public API is unchanged from the old TTS-based coach:
    ///   - SpeakIssue(key)      — one cue line, respects cooldown
    ///   - SpeakSequence(name)  — play every line under that key back-to-back
    ///   - SpeakRaw(text)       — ignored at runtime (no synthesizer)
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceCoach : MonoBehaviour
    {
        [Tooltip("Default cooldown per issue, in seconds. Usually driven by the sensitivity preset.")]
        public float defaultCooldownS = 4.0f;

        [Tooltip("Playback pitch for all clips. 1.0 = normal, >1 faster + higher.")]
        [Range(0.5f, 2.0f)] public float speechRate = 1.0f;

        [Tooltip("Overall cue volume.")]
        [Range(0f, 1f)] public float volume = 1.0f;

        [Tooltip("Silence enforced between the end of a cue (single or last in sequence) and the next one. Prevents two clips feeling back-to-back.")]
        [Range(0f, 1f)] public float interClipGapS = 0.20f;

        public bool Muted { get; set; } = false;

        private AudioSource _source;
        private readonly Dictionary<string, AudioClip[]> _clipsByKey = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> _cursors = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _lastSpokenAt = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _overrides = new Dictionary<string, float>();

        // Tracks when a SpeakSequence's scheduled playback will finish, so
        // SpeakIssue can suppress overlapping cues even after the primary
        // AudioSource stops reporting isPlaying.
        private double _speakingUntilDspTime;

        public bool IsSpeaking =>
            (_source != null && _source.isPlaying) ||
            AudioSettings.dspTime < _speakingUntilDspTime;

        private void Awake()
        {
            // The scene that shipped before this change may not have an
            // AudioSource on the VoiceCoach GameObject (RequireComponent only
            // runs in the Editor). Add one defensively at runtime.
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;          // 2D: heard uniformly in VR
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            _source.bypassReverbZones = true;

            LoadClipsFor(IssueMessages.All.Keys);
            LoadClipsFor(IssueMessages.InstructionSequences.Keys);

            int total = 0;
            foreach (var arr in _clipsByKey.Values) total += arr?.Length ?? 0;
            Debug.Log($"[VoiceCoach] Loaded {total} cue clips across {_clipsByKey.Count} keys.");
        }

        private void LoadClipsFor(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                // Resources.LoadAll returns everything under the folder, sorted
                // alphabetically — we use zero-padded filenames (00.wav, 01.wav…)
                // so this matches the authored order.
                var clips = Resources.LoadAll<AudioClip>("VoiceCues/" + key);
                if (clips != null && clips.Length > 0)
                {
                    _clipsByKey[key] = clips;
                }
                else
                {
                    Debug.LogWarning($"[VoiceCoach] No clips for key '{key}'. Expected under Resources/VoiceCues/{key}/");
                }
            }
        }

        public void SetIssueCooldown(string key, float seconds) => _overrides[key] = seconds;

        /// <summary>
        /// Try to play a cue for an issue key. Respects per-issue cooldown and
        /// mute state. Returns true if a clip was started, false otherwise.
        /// </summary>
        public bool SpeakIssue(string key)
        {
            if (Muted || string.IsNullOrEmpty(key)) return false;
            if (!_clipsByKey.TryGetValue(key, out var clips) || clips == null || clips.Length == 0)
                return false;

            // Global lockout: never talk over an in-flight cue or sequence.
            // Callers that want to queue should retry next frame; the
            // per-issue cooldown below handles the repeat case.
            if (IsSpeaking) return false;

            float now = Time.realtimeSinceStartup;
            float cooldown = _overrides.TryGetValue(key, out var o) ? o : defaultCooldownS;
            if (_lastSpokenAt.TryGetValue(key, out var last) && (now - last) < cooldown)
                return false;

            _cursors.TryGetValue(key, out int cursor);
            var clip = clips[cursor % clips.Length];
            _cursors[key] = cursor + 1;
            _lastSpokenAt[key] = now;

            _source.pitch = Mathf.Clamp(speechRate, 0.5f, 2.0f);
            _source.volume = volume;
            _source.clip = clip;
            _source.Play();
            // Keep the global speaking lockout alive until the clip has
            // finished AND a mandatory breath has passed. This is what stops
            // the next cue from starting right on top of this one's tail.
            _speakingUntilDspTime = AudioSettings.dspTime + clip.length / _source.pitch + interClipGapS;
            return true;
        }

        /// <summary>
        /// Kept for API compatibility. Without a TTS engine on Quest we can't
        /// synthesize arbitrary text, so this just logs the line.
        /// </summary>
        public void SpeakRaw(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[VoiceCoach] SpeakRaw (no TTS on Quest): " + text);
        }

        /// <summary>
        /// Play a whole named sequence (e.g. "welcome", "how_to") back-to-back
        /// with a short breath between lines. Later SpeakIssue calls still
        /// overlap via PlayOneShot, so the instruction playback isn't interrupted.
        /// </summary>
        public void SpeakSequence(string name)
        {
            if (Muted || string.IsNullOrEmpty(name)) return;
            if (!_clipsByKey.TryGetValue(name, out var clips) || clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[VoiceCoach] SpeakSequence: no clips for '{name}'.");
                return;
            }

            float pitch = Mathf.Clamp(speechRate, 0.5f, 2.0f);
            _source.pitch = pitch;

            // Chain clips by scheduling them on the DSP timeline. This gives
            // sample-accurate playback with no main-thread/coroutine jitter.
            double now = AudioSettings.dspTime;
            double dsp = now + 0.05;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                var go = new GameObject("VoiceCue_" + clip.name);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.clip = clip;
                s.volume = volume;
                s.pitch = pitch;
                s.spatialBlend = 0f;
                s.bypassEffects = true;
                s.bypassListenerEffects = true;
                s.bypassReverbZones = true;
                s.playOnAwake = false;
                s.PlayScheduled(dsp);

                // Destroy's delay counts from *now*, not from the scheduled
                // start — so for later clips we have to account for the time
                // they're still waiting on the DSP queue. Adding a generous
                // buffer (plus the inter-clip gap) keeps the AudioSource
                // alive comfortably past its own playback.
                float destroyDelay = (float)((dsp - now) + (clip.length / pitch) + interClipGapS + 1.0);
                Destroy(go, destroyDelay);

                dsp += clip.length / pitch + interClipGapS;
            }

            // Lock out SpeakIssue until the whole sequence has finished.
            _speakingUntilDspTime = dsp;
        }
    }
}
