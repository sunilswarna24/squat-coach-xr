using System.Collections.Generic;

namespace SquatCoach.Coaching
{
    /// <summary>
    /// Spoken copy for each issue key. Keep the Quest and the original
    /// Python prototype in rough sync — both draw from the same list of
    /// issues emitted by the analyzer.
    /// </summary>
    public static class IssueMessages
    {
        /// <summary>
        /// Single source of truth for which issue "wins" when more than one
        /// is active. Both the voice coach (AppController) and the HUD caption
        /// (HudPanel) consult this list so that the text on screen can never
        /// disagree with the clip being spoken.
        /// </summary>
        public static readonly string[] CuePriority =
        {
            "lean_forward", "heel_lift", "knees_forward",
            "depth_shallow", "rushed", "partial_rep",
        };

        /// <summary>
        /// Return the highest-priority key in <paramref name="issues"/>, or
        /// null if the list is empty. Unknown keys fall through to the first
        /// entry so analyzer additions don't silently disappear.
        /// </summary>
        public static string PickTopPriority(IList<string> issues)
        {
            if (issues == null || issues.Count == 0) return null;
            for (int i = 0; i < CuePriority.Length; i++)
            {
                if (issues.Contains(CuePriority[i])) return CuePriority[i];
            }
            return issues[0];
        }

        public static readonly IReadOnlyDictionary<string, string[]> All =
            new Dictionary<string, string[]>
            {
                ["depth_shallow"] = new[]
                {
                    "Go deeper, aim for parallel.",
                    "Sink a bit lower.",
                    "Hit your depth target on the next rep.",
                },
                ["lean_forward"] = new[]
                {
                    "Chest up, don't fold forward.",
                    "Keep your torso taller.",
                    "Stop leaning. Brace your core.",
                },
                ["knees_forward"] = new[]
                {
                    "Push your hips back.",
                    "Knees are drifting past your toes.",
                    "Sit back into the squat.",
                },
                ["heel_lift"] = new[]
                {
                    "Keep your heels planted.",
                    "Drive through your heels.",
                    "Heels down.",
                },
                ["rushed"] = new[]
                {
                    "Control the tempo.",
                    "Slow the descent down.",
                },
                ["partial_rep"] = new[]
                {
                    "Finish the rep all the way up.",
                    "Stand up fully before descending again.",
                },
                ["good_set"] = new[]
                {
                    "Great set.",
                    "Clean form, nice work.",
                },
                ["not_in_position"] = new[]
                {
                    "Step into the frame.",
                    "I can't see your whole side.",
                },
            };

        public static readonly IReadOnlyDictionary<string, string[]> InstructionSequences =
            new Dictionary<string, string[]>
            {
                ["welcome"] = new[]
                {
                    "Welcome to your form coach.",
                    "Place the camera to your side, at about hip height.",
                    "Stand roughly six feet from the camera so your whole body fits in the frame.",
                    "I'll count your reps and speak corrections in real time.",
                },
                ["how_to"] = new[]
                {
                    "Stand with your feet shoulder-width apart, toes slightly turned out.",
                    "Brace your core, and keep your chest up.",
                    "Push your hips back first, then bend your knees.",
                    "Lower until your hips are at least level with your knees.",
                    "Drive through your heels to stand, and squeeze your glutes at the top.",
                },
            };
    }
}
