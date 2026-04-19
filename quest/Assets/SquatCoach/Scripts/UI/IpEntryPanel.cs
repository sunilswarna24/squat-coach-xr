using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SquatCoach.Networking;

namespace SquatCoach.UI
{
    /// <summary>
    /// First-run IP entry for the Pi. Shown when ConnectionConfig is empty,
    /// or when the user taps "Change Pi IP" from the HUD.
    ///
    /// Populate the refs in the Inspector:
    /// - hostInput: a TMP_InputField
    /// - portInput: a TMP_InputField (default 8765)
    /// - connectButton: a Button
    /// - statusText: optional TMP_Text for status/errors
    /// </summary>
    public class IpEntryPanel : MonoBehaviour
    {
        public TMP_InputField hostInput;
        public TMP_InputField portInput;
        public Button connectButton;
        public TMP_Text statusText;

        public event Action OnConnectRequested;

        private void OnEnable()
        {
            if (hostInput != null) hostInput.text = ConnectionConfig.PiHost;
            if (portInput != null) portInput.text = ConnectionConfig.PiPort.ToString();
            if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
        }

        private void OnDisable()
        {
            if (connectButton != null) connectButton.onClick.RemoveListener(OnConnectClicked);
        }

        private void OnConnectClicked()
        {
            string host = hostInput != null ? hostInput.text.Trim() : "";
            if (string.IsNullOrEmpty(host))
            {
                SetStatus("Enter the Pi's IP address.");
                return;
            }
            int port = 8765;
            if (portInput != null && !string.IsNullOrEmpty(portInput.text))
            {
                if (!int.TryParse(portInput.text, out port) || port <= 0 || port > 65535)
                {
                    SetStatus("Invalid port.");
                    return;
                }
            }
            ConnectionConfig.PiHost = host;
            ConnectionConfig.PiPort = port;
            SetStatus($"Connecting to ws://{host}:{port} ...");
            OnConnectRequested?.Invoke();
        }

        public void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg ?? "";
        }
    }
}
