using UnityEngine;

namespace SquatCoach.Networking
{
    /// <summary>
    /// Runtime configuration for the Pi connection. IP is persisted in
    /// PlayerPrefs after the first successful connect, and can be changed
    /// from the IP-entry panel.
    /// </summary>
    public static class ConnectionConfig
    {
        private const string PiHostKey = "squatcoach.pi.host";
        private const string PiPortKey = "squatcoach.pi.port";

        public static string PiHost
        {
            get => PlayerPrefs.GetString(PiHostKey, "");
            set { PlayerPrefs.SetString(PiHostKey, value ?? ""); PlayerPrefs.Save(); }
        }

        public static int PiPort
        {
            get => PlayerPrefs.GetInt(PiPortKey, 8765);
            set { PlayerPrefs.SetInt(PiPortKey, value); PlayerPrefs.Save(); }
        }

        public static string WebSocketUrl =>
            string.IsNullOrWhiteSpace(PiHost) ? null : $"ws://{PiHost}:{PiPort}";

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(PiHost);
    }
}
