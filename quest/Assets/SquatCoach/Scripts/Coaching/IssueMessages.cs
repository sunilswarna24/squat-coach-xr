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
                    "Welcome to the squat coach.",
                    "Place the camera to your side, at about hip height.",
                    "Stand roughly six feet from the camera so your whole body fits in the frame.",
                    "I'll count your reps and speak corrections in real time.",
                },
                ["how_to_squat"] = new[]
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
