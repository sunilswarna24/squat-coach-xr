#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using SquatCoach.App;
using SquatCoach.Coaching;
using SquatCoach.Networking;
using SquatCoach.UI;

namespace SquatCoach.EditorTools
{
    /// <summary>
    /// One-click scene generator. Builds the full Main scene — HUD window,
    /// left-column counters, right-column mannequin, IP entry panel, and App
    /// controller — and wires every Inspector reference via SerializedObject
    /// so the teammate has nothing to drag by hand.
    ///
    /// Use from the Unity menu:   SquatCoach -> Build Main Scene
    /// </summary>
    public static class BuildMainScene
    {
        private const string ScenePath = "Assets/SquatCoach/Scenes/Main.unity";

        [MenuItem("SquatCoach/Build Main Scene")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Main Scene",
                    "This will create (or overwrite) the Main scene at:\n" + ScenePath +
                    "\n\nContinue?",
                    "Build", "Cancel"))
            {
                return;
            }

            EnsureDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera + lighting + EventSystem ------------------------------
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 1f);
            cam.nearClipPlane = 0.05f;
            camGO.transform.position = new Vector3(0, 1.6f, 0);
            camGO.AddComponent<AudioListener>();

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // --- App (WebSocket + VoiceCoach + AppController) -----------------
            var appGO = new GameObject("App");

            var wsGO = new GameObject("WebSocketClient");
            wsGO.transform.SetParent(appGO.transform, false);
            var ws = wsGO.AddComponent<LandmarkWebSocketClient>();

            var voiceGO = new GameObject("VoiceCoach");
            voiceGO.transform.SetParent(appGO.transform, false);
            var voice = voiceGO.AddComponent<VoiceCoach>();

            var app = appGO.AddComponent<AppController>();

            // --- HUD canvas (world space, floating in front of the user) ------
            var hudCanvasGO = new GameObject("HUDCanvas");
            var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.sortingOrder = 0;
            hudCanvasGO.AddComponent<CanvasScaler>();
            hudCanvasGO.AddComponent<GraphicRaycaster>();
            var hudRT = hudCanvasGO.GetComponent<RectTransform>();
            hudRT.sizeDelta = new Vector2(1200, 700);
            hudRT.localScale = Vector3.one * 0.0015f;
            hudRT.position = new Vector3(0, 1.5f, 2.0f);
            hudRT.rotation = Quaternion.identity;

            // Root panel: single translucent window
            var hudPanelGO = CreateChildRect(hudCanvasGO.transform, "HudPanel",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                offsetMin: Vector2.zero, offsetMax: Vector2.zero);
            var hudBg = hudPanelGO.AddComponent<Image>();
            hudBg.color = new Color(0.05f, 0.06f, 0.08f, 0.35f);
            hudBg.raycastTarget = false;
            var hudPanel = hudPanelGO.AddComponent<HudPanel>();

            // Left column (counters + status + cue)
            var leftGO = CreateChildRect(hudPanelGO.transform, "LeftColumn",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.5f, 1f),
                offsetMin: new Vector2(40, 40), offsetMax: new Vector2(-20, -40));

            var repsLabel  = AddTmp(leftGO.transform, "RepsLabel",  "REPS",   48,  new Vector2(0, 620), FontStyles.Normal,   new Color(0.80f, 0.82f, 0.86f, 0.80f));
            var repsValue  = AddTmp(leftGO.transform, "RepsValue",  "00",    160,  new Vector2(0, 490), FontStyles.Bold,     Color.white);
            var setLabel   = AddTmp(leftGO.transform, "SetLabel",   "SET",    36,  new Vector2(0, 380), FontStyles.Normal,   new Color(0.80f, 0.82f, 0.86f, 0.80f));
            var setValue   = AddTmp(leftGO.transform, "SetValue",   "1",      90,  new Vector2(0, 300), FontStyles.Bold,     Color.white);
            var totalLabel = AddTmp(leftGO.transform, "TotalLabel", "TOTAL",  28,  new Vector2(0, 220), FontStyles.Normal,   new Color(0.80f, 0.82f, 0.86f, 0.80f));
            var totalValue = AddTmp(leftGO.transform, "TotalValue", "0",      48,  new Vector2(0, 170), FontStyles.Bold,     Color.white);
            var connection = AddTmp(leftGO.transform, "Connection", "Connecting...", 30, new Vector2(0, 100), FontStyles.Italic, new Color(1.00f, 0.80f, 0.20f));
            var status     = AddTmp(leftGO.transform, "Status",     "",       26,  new Vector2(0, 55),  FontStyles.Italic,   new Color(0.80f, 0.82f, 0.86f, 0.80f));
            var cue        = AddTmp(leftGO.transform, "Cue",        "",       60,  new Vector2(0, -30), FontStyles.Bold,     new Color(0.95f, 0.30f, 0.25f));

            var mutedGO = new GameObject("MutedBadge");
            mutedGO.transform.SetParent(leftGO.transform, false);
            var mutedRT = mutedGO.AddComponent<RectTransform>();
            mutedRT.anchorMin = mutedRT.anchorMax = new Vector2(0.5f, 0f);
            mutedRT.sizeDelta = new Vector2(40, 40);
            mutedRT.anchoredPosition = new Vector2(0, 10);
            var mutedBg = mutedGO.AddComponent<Image>();
            mutedBg.color = new Color(1f, 1f, 1f, 0.35f);
            mutedGO.SetActive(false);

            // Right column (mannequin)
            var rightGO = CreateChildRect(hudPanelGO.transform, "RightColumn",
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(20, 40), offsetMax: new Vector2(-40, -40));
            var mannequinGO = CreateChildRect(rightGO.transform, "Mannequin",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                offsetMin: Vector2.zero, offsetMax: Vector2.zero);
            var mannequinGraphic = mannequinGO.AddComponent<MannequinGraphic>();
            mannequinGraphic.color = Color.white;
            var mannequinRenderer = mannequinGO.AddComponent<MannequinRenderer>();

            // --- IP entry canvas (world space, in front of the HUD) ----------
            var ipCanvasGO = new GameObject("IPEntryCanvas");
            var ipCanvas = ipCanvasGO.AddComponent<Canvas>();
            ipCanvas.renderMode = RenderMode.WorldSpace;
            ipCanvas.sortingOrder = 1;
            ipCanvasGO.AddComponent<CanvasScaler>();
            ipCanvasGO.AddComponent<GraphicRaycaster>();
            var ipRT = ipCanvasGO.GetComponent<RectTransform>();
            ipRT.sizeDelta = new Vector2(800, 500);
            ipRT.localScale = Vector3.one * 0.0015f;
            ipRT.position = new Vector3(0, 1.5f, 1.5f);
            var ipBg = ipCanvasGO.AddComponent<Image>();
            ipBg.color = new Color(0.03f, 0.05f, 0.10f, 0.9f);

            var ipHeader = AddTmp(ipCanvasGO.transform, "Header",
                "Connect to your coach", 48, new Vector2(0, 180),
                FontStyles.Bold, Color.white);

            var hostInput = AddInput(ipCanvasGO.transform, "HostInput",
                placeholder: "192.168.1.42", position: new Vector2(0, 60), width: 560, height: 70);
            var portInput = AddInput(ipCanvasGO.transform, "PortInput",
                placeholder: "8765",         position: new Vector2(0, -30), width: 260, height: 70);

            var connectGO = new GameObject("ConnectButton");
            connectGO.transform.SetParent(ipCanvasGO.transform, false);
            var connectRT = connectGO.AddComponent<RectTransform>();
            connectRT.sizeDelta = new Vector2(280, 70);
            connectRT.anchoredPosition = new Vector2(0, -130);
            var connectBg = connectGO.AddComponent<Image>();
            connectBg.color = new Color(0.20f, 0.55f, 0.85f, 1f);
            var connectBtn = connectGO.AddComponent<Button>();
            var connectLabel = AddTmp(connectGO.transform, "Label", "Connect",
                34, Vector2.zero, FontStyles.Bold, Color.white);
            connectLabel.alignment = TextAlignmentOptions.Center;
            StretchToParent(connectLabel.rectTransform);

            var ipStatus = AddTmp(ipCanvasGO.transform, "Status",
                "", 26, new Vector2(0, -210), FontStyles.Italic,
                new Color(1.00f, 0.80f, 0.20f));

            var ipPanel = ipCanvasGO.AddComponent<IpEntryPanel>();

            // --- Wire every Inspector reference via SerializedObject ---------
            WireField(app, "wsClient", ws);
            WireField(app, "voiceCoach", voice);
            WireField(app, "hud", hudPanel);
            WireField(app, "ipEntryPanel", ipPanel);

            WireField(hudPanel, "backgroundImage", hudBg);
            WireField(hudPanel, "repsValueText",   repsValue);
            WireField(hudPanel, "setValueText",    setValue);
            WireField(hudPanel, "totalValueText",  totalValue);
            WireField(hudPanel, "connectionText",  connection);
            WireField(hudPanel, "statusText",      status);
            WireField(hudPanel, "cueText",         cue);
            WireField(hudPanel, "mutedBadge",      mutedGO);
            WireField(hudPanel, "mannequin",       mannequinRenderer);

            WireField(mannequinRenderer, "graphic", mannequinGraphic);

            WireField(ipPanel, "hostInput",     hostInput);
            WireField(ipPanel, "portInput",     portInput);
            WireField(ipPanel, "connectButton", connectBtn);
            WireField(ipPanel, "statusText",    ipStatus);

            // --- Save --------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            EditorUtility.DisplayDialog(
                "Build Main Scene",
                "Main scene created at " + ScenePath +
                "\nAdded to Build Settings.",
                "OK");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static GameObject CreateChildRect(
            Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private static TMP_Text AddTmp(
            Transform parent, string name, string text, float size,
            Vector2 position, FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, Mathf.Max(60, size * 1.4f));
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = position;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TMP_InputField AddInput(
            Transform parent, string name, string placeholder,
            Vector2 position, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = position;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.15f, 0.22f, 1f);
            var input = go.AddComponent<TMP_InputField>();

            // viewport
            var viewportGO = new GameObject("Text Area");
            viewportGO.transform.SetParent(go.transform, false);
            var vRT = viewportGO.AddComponent<RectTransform>();
            StretchToParent(vRT, new Vector4(12, 12, 12, 12));
            viewportGO.AddComponent<RectMask2D>();

            // placeholder
            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(viewportGO.transform, false);
            StretchToParent(phGO.AddComponent<RectTransform>());
            var ph = phGO.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 30;
            ph.color = new Color(0.6f, 0.62f, 0.7f, 1f);
            ph.fontStyle = FontStyles.Italic;

            // live text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(viewportGO.transform, false);
            StretchToParent(textGO.AddComponent<RectTransform>());
            var txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 30;
            txt.color = Color.white;

            input.textViewport = viewportGO.GetComponent<RectTransform>();
            input.textComponent = txt;
            input.placeholder = ph;
            return input;
        }

        private static void StretchToParent(RectTransform rt, Vector4 padding = default)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding.x, padding.y);
            rt.offsetMax = new Vector2(-padding.z, -padding.w);
        }

        private static void WireField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] {target.GetType().Name} has no serialized field '{fieldName}'");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
            {
                if (s.path == scenePath) return;
            }
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
            list.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
