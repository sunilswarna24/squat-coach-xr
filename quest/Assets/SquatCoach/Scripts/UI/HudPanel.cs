using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SquatCoach.Analysis;

namespace SquatCoach.UI
{
    /// <summary>
    /// Floating 2D HUD panel that mirrors what the Python prototype shows
    /// on its OpenCV window. Attach to a world-space Canvas and wire up
    /// the TMP/Image references in the Inspector.
    ///
    /// The panel is deliberately data-driven: the AppController calls
    /// `Render(...)` every frame with the latest snapshot.
    /// </summary>
    public class HudPanel : MonoBehaviour
    {
        [Header("Top bar")]
        public TMP_Text setAndRepsText;
        public TMP_Text phaseText;

        [Header("Metrics line")]
        public TMP_Text metricsText;

        [Header("Status + issues")]
        public TMP_Text statusText;
        public TMP_Text activeIssuesText;

        [Header("Badges")]
        public TMP_Text sensitivityBadge;
        public TMP_Text depthBadge;
        public TMP_Text sideBadge;
        public TMP_Text connectionBadge;
        public TMP_Text mutedBadge;

        [Header("Colors")]
        public Color okColor = new Color(0.20f, 0.80f, 0.20f);
        public Color warnColor = new Color(1.00f, 0.65f, 0.00f);
        public Color badColor = new Color(0.90f, 0.15f, 0.15f);
        public Color dimColor = new Color(0.75f, 0.75f, 0.75f);

        public void Render(
            int setIdx, int repsInSet, int totalReps,
            SquatAnalyzer.Phase phase,
            FrameMetrics metrics,
            List<string> activeIssues,
            string status,
            string sensitivity, string depthTarget, string side,
            string connectionLabel, bool muted)
        {
            if (setAndRepsText != null)
                setAndRepsText.text = $"Set {setIdx}   Reps {repsInSet}   (total {totalReps})";
            if (phaseText != null)
                phaseText.text = phase.ToString();

            if (metricsText != null)
            {
                if (metrics.VisibilityOk)
                {
                    metricsText.color = dimColor;
                    metricsText.text =
                        $"Knee {metrics.KneeAngleDeg,5:F1}°   " +
                        $"Lean {metrics.TorsoLeanDeg,5:F1}°   " +
                        $"Depth {metrics.DepthRatio:+0.00;-0.00}   " +
                        $"KoT {metrics.KneePastToeRatio:+0.00;-0.00}";
                }
                else
                {
                    metricsText.text = "";
                }
            }

            if (statusText != null)
            {
                statusText.text = status ?? "";
                statusText.color = string.IsNullOrEmpty(status) ? dimColor : warnColor;
            }

            if (activeIssuesText != null)
            {
                if (activeIssues != null && activeIssues.Count > 0)
                {
                    activeIssuesText.color = badColor;
                    activeIssuesText.text = string.Join("   •   ", activeIssues);
                }
                else
                {
                    activeIssuesText.text = "";
                }
            }

            if (sensitivityBadge != null) sensitivityBadge.text = $"Sens: {sensitivity}";
            if (depthBadge != null) depthBadge.text = $"Depth: {depthTarget}";
            if (sideBadge != null) sideBadge.text = $"Side: {side}";
            if (connectionBadge != null) connectionBadge.text = connectionLabel;
            if (mutedBadge != null) mutedBadge.gameObject.SetActive(muted);
        }
    }
}
