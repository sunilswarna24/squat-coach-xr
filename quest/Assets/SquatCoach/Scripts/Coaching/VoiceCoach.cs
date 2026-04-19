using System.Collections.Generic;
using UnityEngine;

namespace SquatCoach.Coaching
{
    /// <summary>
    /// Speaks issue cues with per-issue cooldowns and message cycling.
    /// Wraps AndroidTts; swap in a different ITts later without touching
    /// the analyzer integration.
    /// </summary>
    public class VoiceCoach : MonoBehaviour
    {
        [Tooltip("Default cooldown per issue, in seconds. Usually driven by the sensitivity preset.")]
        public float defaultCooldownS = 4.0f;

        [Tooltip("Android TTS speech rate. 1.0 = normal.")]
        public float speechRate = 1.0f;

        public bool Muted { get; set; } = false;

        private readonly AndroidTts _tts = new AndroidTts();
        private readonly Dictionary<string, int> _cursors = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _lastSpokenAt = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _overrides = new Dictionary<string, float>();

        private void Awake()
        {
            _tts.Initialize(success =>
            {
                _tts.SetRateAndPitch(speechRate, 1.0f);
            });
        }

        private void OnDestroy() => _tts.Dispose();

        public void SetIssueCooldown(string key, float seconds) => _overrides[key] = seconds;

        /// <summary>
        /// Try to speak a cue for an issue key. Respects per-issue cooldown and
        /// mute state. Returns true if spoken, false if suppressed.
        /// </summary>
        public bool SpeakIssue(string key)
        {
            if (Muted || string.IsNullOrEmpty(key)) return false;
            if (!IssueMessages.All.TryGetValue(key, out var lines) || lines.Length == 0)
                return false;

            float now = Time.realtimeSinceStartup;
            float cooldown = _overrides.TryGetValue(key, out var o) ? o : defaultCooldownS;
            if (_lastSpokenAt.TryGetValue(key, out var last) && (now - last) < cooldown)
                return false;

            _cursors.TryGetValue(key, out int cursor);
            string text = lines[cursor % lines.Length];
            _cursors[key] = cursor + 1;
            _lastSpokenAt[key] = now;

            _tts.Speak(text);
            return true;
        }

        /// <summary>Speak arbitrary text, bypassing cooldown. Use sparingly.</summary>
        public void SpeakRaw(string text)
        {
            if (Muted || string.IsNullOrEmpty(text)) return;
            _tts.Speak(text);
        }

        /// <summary>
        /// Speak an entire named sequence back-to-back (welcome, how_to_squat).
        /// Android's TTS queues these internally (QUEUE_ADD mode).
        /// </summary>
        public void SpeakSequence(string name)
        {
            if (Muted) return;
            if (!IssueMessages.InstructionSequences.TryGetValue(name, out var lines)) return;
            _tts.Stop();    // clear pending utterances so instructions land immediately
            foreach (var line in lines) _tts.Speak(line);
        }
    }
}
