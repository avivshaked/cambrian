using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's camera: fly anywhere, or follow one creature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not <c>FollowCamera</c>, which says so itself.</b> That component's own documentation
    /// calls it "a viewing aid for the sandbox scene, nothing more" and says the theatre "will
    /// not reuse this" — and it cannot: it orbits one target at a framing radius, which is right
    /// for looking at a single creature on a turntable and useless in a world 60 m deep and
    /// 6.4 km wide where the thing you want to look at is a kilometre away and moving.
    /// </para>
    /// <para>
    /// <b>Tiling is why free flight matters.</b> Creatures are placed 100 m apart on a lattice
    /// (§6.3), so a world of four thousand of them spans several kilometres and no fixed vantage
    /// shows two neighbours at once. Speed is therefore on the wheel, over three orders of
    /// magnitude: 0.5 m/s to look at a body, hundreds to cross the lattice.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public sealed class TheatreCamera : MonoBehaviour
    {
        [Tooltip("Metres per second at the current wheel setting.")]
        public float Speed = 12f;

        [Tooltip("Degrees per pixel of mouse movement while the right button is held.")]
        public float LookSensitivity = 0.15f;

        [Tooltip("Held while flying: multiplies speed.")]
        public float BoostMultiplier = 8f;

        /// <summary>The body being followed, or null to fly free.</summary>
        public Transform Following { get; private set; }

        private Vector3 _followOffset = new Vector3(0f, 1.2f, -3.5f);
        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        /// <summary>Follows a body, keeping the current viewing offset.</summary>
        public void Follow(Transform body, float radius)
        {
            Following = body;
            if (body == null) return;

            // Framed from the body's own size rather than a constant: part half-extents span
            // 0.1-0.4 m and part counts 1-16, so an offset that suits a large creature loses a
            // small one entirely — FollowCamera learnt this first.
            float distance = Mathf.Max(1.5f, radius * 3.5f);
            _followOffset = new Vector3(0f, radius * 0.8f, -distance);
        }

        public void StopFollowing() => Following = null;

        private void LateUpdate()
        {
            ReadLook();

            if (Following != null)
            {
                Vector3 target = Following.position;

                // The root of a built body never moves (physics moves its child), so follow the
                // articulation root if there is one — otherwise a "follow" would sit still while
                // the creature swam away.
                if (Following.childCount > 0) target = Following.GetChild(0).position;

                transform.position = target + Quaternion.Euler(0f, _yaw, 0f) * _followOffset;
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                return;
            }

            ReadFlight();
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void ReadLook()
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                Speed = Mathf.Clamp(Speed * Mathf.Exp(wheel * 4f), 0.5f, 800f);
            }

            if (!Input.GetMouseButton(1)) return;

            _yaw += Input.GetAxis("Mouse X") * LookSensitivity * 40f;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * LookSensitivity * 40f, -89f, 89f);
        }

        private void ReadFlight()
        {
            var move = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));

            if (move.sqrMagnitude <= 0f) return;

            float speed = Speed * (Input.GetKey(KeyCode.LeftShift) ? BoostMultiplier : 1f);

            transform.position +=
                (transform.right * move.x + Vector3.up * move.y + transform.forward * move.z) *
                (speed * Time.unscaledDeltaTime);
        }
    }
}
