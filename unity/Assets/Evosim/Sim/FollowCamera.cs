using UnityEngine;

namespace Evosim.Sim
{
    /// <summary>
    /// Keeps the camera on a moving target, and lets a person drive it — orbit, zoom, frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A viewing aid for the sandbox scene, nothing more. The theatre (DESIGN.md §6.1,
    /// Milestone 7) is where presentation actually lives and will not reuse this.
    /// </para>
    /// <para>
    /// <b>It is driveable because a fixed camera turned out not to be a viewing aid at all.</b>
    /// The first version framed the creature from a constant offset and could not be moved, so
    /// the only way to get close enough to judge whether parts were overlapping was to leave
    /// the running game, switch to Unity's Scene view, and fly a developer camera around by
    /// keyboard. That is not a thing to ask of someone looking at their own simulation, and the
    /// questions it made unanswerable were the ones worth asking.
    /// </para>
    /// <para>
    /// Distance is derived from the creature's own size rather than fixed, because part
    /// half-extents span 0.1–0.4 m and creatures run from 2 to 16 parts. One offset that frames
    /// a sixteen-part body puts a two-part body in the far distance — and under §5A the early
    /// world is <i>entirely</i> small bodies.
    /// </para>
    /// </remarks>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;

        [Tooltip("Higher is snappier. Smoothing hides the fact that the target teleports on respawn.")]
        public float Smoothing = 3f;

        [Tooltip("Multiplies the framing distance derived from the creature's size.")]
        public float ZoomScale = 1f;

        [Tooltip("Degrees per screen-width of horizontal drag.")]
        public float OrbitSensitivity = 180f;

        /// <summary>Radius the target is framed at, before <see cref="ZoomScale"/>.</summary>
        /// <remarks>
        /// Set by whoever spawns the creature — it knows the body's extent and the camera does
        /// not. Falls back to something sane so the camera is never useless if nobody sets it.
        /// </remarks>
        public float FrameRadius = 2.5f;

        private float _yaw = 20f;
        private float _pitch = 18f;
        private Vector3 _focus;

        /// <summary>Points the camera at a new creature without smoothing in from the old one.</summary>
        public void SnapTo(Transform target, float frameRadius)
        {
            Target = target;
            FrameRadius = Mathf.Max(0.3f, frameRadius);

            if (target != null)
            {
                _focus = target.position;
                transform.position = _focus + Rotation() * new Vector3(0f, 0f, -Distance());
                transform.LookAt(_focus);
            }
        }

        private Quaternion Rotation() => Quaternion.Euler(_pitch, _yaw, 0f);

        private float Distance() => FrameRadius * 3f * Mathf.Max(0.15f, ZoomScale);

        private void LateUpdate()
        {
            ReadInput();

            if (Target == null) return;

            _focus = Vector3.Lerp(
                _focus, Target.position, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));

            transform.position = _focus + Rotation() * new Vector3(0f, 0f, -Distance());
            transform.LookAt(_focus);
        }

        private void ReadInput()
        {
            // Left-drag orbits and the wheel zooms, which is what every 3D viewer does and
            // therefore what someone will try first without being told.
            if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * OrbitSensitivity * 0.02f;
                _pitch = Mathf.Clamp(
                    _pitch - Input.GetAxis("Mouse Y") * OrbitSensitivity * 0.02f, -85f, 85f);
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                // Multiplicative, so one notch is the same proportional step whether the
                // creature is 0.3 m or 4 m across. A linear step would be imperceptible on a
                // large body and would pass straight through a small one.
                ZoomScale = Mathf.Clamp(ZoomScale * Mathf.Pow(0.88f, wheel), 0.15f, 12f);
            }
        }
    }
}
