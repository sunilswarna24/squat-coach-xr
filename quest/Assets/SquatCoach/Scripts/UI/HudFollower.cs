using UnityEngine;

namespace SquatCoach.UI
{
    /// <summary>
    /// Keeps a world-space UI canvas parked at a comfortable position
    /// relative to the user's head.
    ///
    /// Why it exists: a fixed world-space canvas at (0, 1.5, 2) is only
    /// visible if the user happens to be facing the scene's +Z direction
    /// when they launch the app. In practice the headset's forward is
    /// wherever they were physically facing at app start, so a fixed
    /// canvas frequently ends up behind them and the whole screen reads
    /// as black. This component solves that by re-anchoring the canvas
    /// in front of the user at startup and then holding its position so
    /// it doesn't swim with every head motion.
    /// </summary>
    [ExecuteAlways]
    public class HudFollower : MonoBehaviour
    {
        [Tooltip("Camera to follow. Defaults to Camera.main if left empty.")]
        public Camera target;

        [Tooltip("Distance in meters from the camera to place the canvas.")]
        public float distance = 1.6f;

        [Tooltip("Vertical offset (meters) relative to camera height. Negative = slightly below eye line.")]
        public float verticalOffset = -0.05f;

        [Tooltip("How quickly the canvas re-centers on the camera's forward when you turn your head. 0 = locked to initial pose, 1 = snap instantly. Low values feel steady.")]
        [Range(0f, 1f)] public float followLerp = 0.04f;

        [Tooltip("Turn angle (degrees) before the canvas snaps to the new forward. Prevents big re-centers from tiny head wobble.")]
        public float snapAngle = 60f;

        [Tooltip("On Awake, immediately place the canvas in front of the head instead of waiting for the first frame.")]
        public bool recenterOnEnable = true;

        private bool _primed;

        private void OnEnable()
        {
            _primed = false;
            if (recenterOnEnable) SnapToCamera();
        }

        private void LateUpdate()
        {
            SnapToCamera();
        }

        private void SnapToCamera()
        {
            if (target == null) target = Camera.main;
            if (target == null) return;

            Vector3 camPos = target.transform.position;
            Vector3 camFwd = target.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 1e-4f) camFwd = Vector3.forward;
            camFwd.Normalize();

            Vector3 desiredPos = camPos + camFwd * distance + Vector3.up * verticalOffset;
            Quaternion desiredRot = Quaternion.LookRotation(camFwd, Vector3.up);

            if (!_primed)
            {
                transform.position = desiredPos;
                transform.rotation = desiredRot;
                _primed = true;
                return;
            }

            // Large head turns snap; small ones just drift to keep the UI
            // planted and avoid nauseating motion.
            float angleDelta = Quaternion.Angle(transform.rotation, desiredRot);
            float lerp = angleDelta > snapAngle ? 1f : followLerp;

            transform.position = Vector3.Lerp(transform.position, desiredPos, lerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, lerp);
        }
    }
}
