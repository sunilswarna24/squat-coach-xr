using System;
using UnityEngine;

namespace SquatCoach.Coaching
{
    /// <summary>
    /// Thin wrapper over Android's android.speech.tts.TextToSpeech.
    /// Works on the Meta Quest 3 (Android). In the Unity editor it degrades
    /// to Debug.Log so the flow can be tested without a device.
    ///
    /// Call `Initialize()` once at startup, then `Speak(text)` to enqueue
    /// utterances. Android's TTS internally queues QUEUE_ADD calls so we
    /// don't need our own queue — we rely on the per-issue cooldown in
    /// VoiceCoach to avoid chattiness.
    /// </summary>
    public class AndroidTts : IDisposable
    {
        private AndroidJavaObject _tts;
        private bool _ready;

        public bool IsReady => _ready;

        public void Initialize(Action<bool> onReady = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Proxy for android.speech.tts.TextToSpeech.OnInitListener.
                var listener = new TtsInitListener(success =>
                {
                    _ready = success;
                    onReady?.Invoke(success);
                });

                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
            }
            catch (Exception e)
            {
                Debug.LogError("AndroidTts init failed: " + e.Message);
                _ready = false;
                onReady?.Invoke(false);
            }
#else
            _ready = true;
            onReady?.Invoke(true);
#endif
        }

        public void SetRateAndPitch(float rate = 1.0f, float pitch = 1.0f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call<int>("setSpeechRate", rate);
            _tts?.Call<int>("setPitch", pitch);
#endif
        }

        public void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_tts == null || !_ready) return;
            // QUEUE_ADD = 1 — appends this utterance to Android's TTS queue.
            try { _tts.Call<int>("speak", text, 1, null, null); }
            catch (Exception e) { Debug.LogWarning("AndroidTts speak failed: " + e.Message); }
#else
            Debug.Log("[TTS] " + text);
#endif
        }

        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _tts?.Call<int>("stop"); } catch { /* swallow */ }
#endif
        }

        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _tts?.Call("shutdown"); } catch { /* swallow */ }
            _tts?.Dispose();
            _tts = null;
#endif
            _ready = false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Java proxy for TextToSpeech.OnInitListener.onInit(int status).
        /// status == 0 (SUCCESS) means TTS is ready.
        /// </summary>
        private class TtsInitListener : AndroidJavaProxy
        {
            private readonly Action<bool> _cb;
            public TtsInitListener(Action<bool> cb)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _cb = cb;
            }
            public void onInit(int status) => _cb?.Invoke(status == 0);
        }
#endif
    }
}
