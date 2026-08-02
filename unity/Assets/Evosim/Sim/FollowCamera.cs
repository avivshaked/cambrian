using UnityEngine;

namespace Evosim.Sim
{
    /// <summary>
    /// Keeps the camera on a moving target. A viewing aid for the sandbox scene, nothing more.
    /// </summary>
    /// <remarks>
    /// The theatre (DESIGN.md §6.1, Milestone 7) is where presentation actually lives and
    /// will not reuse this. Until then a creature with no fluid resistance leaves the frame
    /// in well under a second, and a scene you cannot see is not a visual payoff.
    /// </remarks>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;

        [Tooltip("Offset from the target, in the target's resting frame.")]
        public Vector3 Offset = new Vector3(0f, 2.5f, -7f);

        [Tooltip("Higher is snappier. Smoothing hides the fact that the target teleports on respawn.")]
        public float Smoothing = 3f;

        private void LateUpdate()
        {
            if (Target == null) return;

            Vector3 wanted = Target.position + Offset;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
            transform.LookAt(Target.position);
        }
    }
}
