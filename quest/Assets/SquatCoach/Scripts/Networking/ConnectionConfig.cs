using UnityEngine;

namespace SquatCoach.Networking
{
    /// <summary>
    /// Runtime configuration for the Pi connection. The host/port default to
    /// the values baked in below so the Quest app can launch without needing
    /// controllers to type an IP. PlayerPrefs still override the defaults if
    /// a user has previously entered a different IP.
    /// </summary>
    public static class ConnectionConfig
    {
        // Hard-coded defaults used when PlayerPrefs are empty (controllers
        // optional). Update here if the Pi's LAN IP changes.
        public const string DefaultPiHost = "172.25.117.54";
        public const int    DefaultPiPort = 8765;

        private const string PiHostKey = "squatcoach.pi.host";
        private const string PiPortKey = "squatcoach.pi.port";

        public static string PiHost
        {
            get
            {
                var v = PlayerPrefs.GetString(PiHostKey, "");
                return string.IsNullOrWhiteSpace(v) ? DefaultPiHost : v;
            }
            set { PlayerPrefs.SetString(PiHostKey, value ?? ""); PlayerPrefs.Save(); }
        }

        public static int PiPort
        {
            get => PlayerPrefs.GetInt(PiPortKey, DefaultPiPort);
            set { PlayerPrefs.SetInt(PiPortKey, value); PlayerPrefs.Save(); }
        }

        public static string WebSocketUrl =>
            string.IsNullOrWhiteSpace(PiHost) ? null : $"ws://{PiHost}:{PiPort}";

        // Always true now that we have a baked-in default host.
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(PiHost);
    }
}
