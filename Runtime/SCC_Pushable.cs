using UnityEngine;

// ReSharper disable once CheckNamespace
namespace SuperiorCharacterController
{
    /// <summary>
    /// Opt-in component for <see cref="Rigidbody"/> that the <see cref="SCC_Controller"/> can push and be pushed by (kinda).
    /// Without this component the SCC treats rigidbodies as a static blocker.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SCC_Pushable : MonoBehaviour
    {
        [Tooltip("How much this object resists being pushed by the character. Can cause jittering.")]
        [SerializeField, Range(0f, 1f)] private float pushedResistance = 0f;

        [Tooltip("How much this object decelerates when it rams into an immobile SCC. Doesn't scale linearly.")]
        [SerializeField, Range(0f, 1f)] private float pushingResistance = 0.15f;

        public float PushedResistance => pushedResistance;
        public float PushingResistance => pushingResistance;
        public bool IsGrounded => groundedContactCount > 0;

        // Velocity scheduled by the SCC to apply at the contact point in the next FixedUpdate.
        // Exposed so the SCC can account for it when computing how much more to add/remove
        public Vector3 ScheduledVelocity { get; private set; }

        private Rigidbody rb;
        private Vector3 scheduledPoint;
        private int groundedContactCount;

        // Ignore SCC CurrentUpDirection by design
        // The SCC gravity isn't meant to always be shared with the world for my project,
        // so linking it here would probably cause issues in my use project
        private const float GroundNormalY = 0.5f;
        private const float NearZeroSqr = 0.0001f;


        private void Awake() => rb = GetComponent<Rigidbody>();


        /// <summary>
        /// Stack up velocity change to apply at the contact point in the next FixedUpdate.
        /// Used both when the character pushes the object and when the object rams the character and is forced to slow down.
        /// </summary>
        public void SchedulePush(Vector3 velocity, Vector3 point)
        {
            if (ScheduledVelocity.sqrMagnitude < NearZeroSqr) scheduledPoint = point;
            ScheduledVelocity += velocity;
        }


        private void FixedUpdate()
        {
            if (!(ScheduledVelocity.sqrMagnitude > NearZeroSqr)) return;

            rb.AddForceAtPosition(ScheduledVelocity, scheduledPoint, ForceMode.VelocityChange);
            ScheduledVelocity = Vector3.zero;
        }
    
    
        private void OnCollisionEnter(Collision c) => UpdateGroundedContacts(c, +1);
        private void OnCollisionExit(Collision c) => UpdateGroundedContacts(c, -1);
    
    
        private void UpdateGroundedContacts(Collision c, int delta)
        {
            int count = c.contactCount;
            for (int i = 0; i < count; i++)
            {
                if (!(c.GetContact(i).normal.y > GroundNormalY)) continue;
            
                groundedContactCount = Mathf.Max(0, groundedContactCount + delta);
                return;
            }
        }
    }
}
