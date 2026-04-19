using UnityEngine;
using UnityEngine.XR;

namespace SquatCoach.App
{
    /// <summary>
    /// Minimal stand-in for <c>TrackedPoseDriver</c>: copies the headset's
    /// pose onto this GameObject's transform every frame so anything parented
    /// underneath (like our head-locked HUD canvas) actually moves with the
    /// user.
    ///
    /// Why we need this: this project pulls in only the bare-bones
    /// <c>com.unity.modules.vr</c> / <c>com.unity.xr.oculus</c> packages (no
    /// XR Interaction Toolkit, no Input System Tracked Pose Driver). With
    /// just those, the OVR runtime drives the per-eye render matrices but
    /// leaves <c>Camera.transform</c> at whatever the scene serialized — so
    /// world-space UI parented to the camera is still glued to a fixed point
    /// in front of world +Z. This script closes that gap with one
    /// <see cref="InputTracking"/> read per frame.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class XrHeadPoseDriver : MonoBehaviour
    {
        [Tooltip("Tracking node to read. CenterEye is the conventional 'head' pose for HMDs.")]
        public XRNode node = XRNode.CenterEye;

        private void OnEnable()
        {
            Application.onBeforeRender += UpdatePose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePose;
        }

        private void Update()
        {
            // Update once per frame too so non-render systems (UI raycasts,
            // physics, follower scripts) see a fresh pose during their normal
            // update phase.
            UpdatePose();
        }

        private void UpdatePose()
        {
            var pos = InputTracking.GetLocalPosition(node);
            var rot = InputTracking.GetLocalRotation(node);

            // InputTracking returns identity when no device is connected
            // yet — leave the serialized eye-height pose alone in that case
            // so the editor / editor-batch builds don't snap the camera to
            // the floor.
            if (pos == Vector3.zero && rot == Quaternion.identity) return;

            transform.localPosition = pos;
            transform.localRotation = rot;
        }
    }
}
