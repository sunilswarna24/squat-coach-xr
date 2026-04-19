#!/usr/bin/env bash
# Pre-render every coach line to a mono 16-bit WAV using macOS's built-in
# `say` tool, then write them into the Unity `Resources/VoiceCues/<key>/NN.wav`
# layout that VoiceCoach loads at runtime.
#
# Why: the Meta Quest 3 ships without any Android text-to-speech engine, so
# `android.speech.tts.TextToSpeech` never becomes ready and audio cues never
# play. Bundling pre-synthesized clips removes that runtime dependency.
#
# Usage:  ./quest/tools/generate_voice_cues.sh            (uses defaults)
#         VOICE=Alex RATE=200 ./quest/tools/generate_voice_cues.sh
#
# Requirements: macOS (for `say` + `afconvert`).
set -euo pipefail

VOICE="${VOICE:-Samantha}"
RATE="${RATE:-185}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$SCRIPT_DIR/../Assets/SquatCoach/Resources/VoiceCues"
mkdir -p "$OUT"

gen() {
    local key="$1"; shift
    local dir="$OUT/$key"
    rm -rf "$dir"
    mkdir -p "$dir"
    local i=0
    for line in "$@"; do
        local aiff
        aiff="$(mktemp -t vc.XXXXXX).aiff"
        /usr/bin/say -v "$VOICE" -r "$RATE" -o "$aiff" "$line"
        /usr/bin/afconvert -f WAVE -d LEI16@22050 -c 1 "$aiff" "$dir/$(printf '%02d' $i).wav"
        rm -f "$aiff"
        printf '  [%-14s %2d] %s\n' "$key" "$i" "$line"
        i=$((i + 1))
    done
}

echo "Voice: $VOICE  Rate: $RATE  Out: $OUT"
echo

# --- issue cues ---------------------------------------------------------------
gen depth_shallow \
    "Go deeper, aim for parallel." \
    "Sink a bit lower." \
    "Hit your depth target on the next rep."

gen lean_forward \
    "Chest up, don't fold forward." \
    "Keep your torso taller." \
    "Stop leaning. Brace your core."

gen knees_forward \
    "Push your hips back." \
    "Knees are drifting past your toes." \
    "Sit back into the squat."

gen heel_lift \
    "Keep your heels planted." \
    "Drive through your heels." \
    "Heels down."

gen rushed \
    "Control the tempo." \
    "Slow the descent down."

gen partial_rep \
    "Finish the rep all the way up." \
    "Stand up fully before descending again."

gen good_set \
    "Great set." \
    "Clean form, nice work."

gen not_in_position \
    "Step into the frame." \
    "I can't see your whole side."

# --- instruction sequences ----------------------------------------------------
gen welcome \
    "Welcome to your form coach." \
    "Place the camera to your side, at about hip height." \
    "Stand roughly six feet from the camera so your whole body fits in the frame." \
    "I'll count your reps and speak corrections in real time."

gen how_to \
    "Stand with your feet shoulder-width apart, toes slightly turned out." \
    "Brace your core, and keep your chest up." \
    "Push your hips back first, then bend your knees." \
    "Lower until your hips are at least level with your knees." \
    "Drive through your heels to stand, and squeeze your glutes at the top."

echo
echo "Done. Wrote clips to $OUT"
