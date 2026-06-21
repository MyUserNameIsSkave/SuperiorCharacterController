using UnityEngine;

// ReSharper disable once CheckNamespace
namespace SuperiorCharacterController
{
    // Before the SCC_Controller, after what makes the character move (default order 0)
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class SCC_DynamicSurface : MonoBehaviour
    {
        [Header("     MONITORING")]
        [SerializeField] private bool trackPosition = true;
        [SerializeField] private bool trackRotation = true;
    
        public bool TrackPosition => trackPosition;
        public bool TrackRotation => trackRotation;

        // Needed for my personal project, not necessary for the default behavior
        [Header("Exception")]
        [Tooltip("Enable on cylinders that spin on their own axis. During wall-running, the camera yaw is derived from the angular arc the player actually traverses around " +
                 "the cylinder center, instead of the raw rotation delta. Fixes the wrong yaw direction when the cylinder spins against the player's movement direction.")]
        public bool isSelfSpinningCylinder = false;


        /// <summary>
        /// World translation of the pivot this frame (currentPos - prevPos).
        /// </summary>
        public Vector3 PositionDelta { get; private set; }
    
        /// <summary>
        /// World rotation applied this frame (currentRot * Inverse(prevRot)).
        /// </summary>
        public Quaternion RotationDelta { get; private set; } = Quaternion.identity;
    
        /// <summary>
        /// PositionDelta / dt — the pivot's linear velocity, for momentum hand-off on leave.
        /// </summary>
        public Vector3 Velocity { get; private set; }

        private Vector3 previousPosition;
        private Quaternion previousRotation = Quaternion.identity;
        private bool initialised;
    
    
    
        private void OnEnable()
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            PositionDelta = Vector3.zero;
            RotationDelta = Quaternion.identity;
            Velocity = Vector3.zero;
            initialised = true;
        }
    
    
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !initialised) return;
        
            if (trackPosition) UpdatePosition(dt);
            else ResetPosition();

            if (trackRotation) UpdateRotation();
            else ResetRotation();
        
        }


        private void UpdatePosition(float dt)
        {
            Vector3 current = transform.position;
            PositionDelta = current - previousPosition;
            Velocity = PositionDelta / dt;
            previousPosition = current;
        }
    
        private void ResetPosition()
        {
            PositionDelta = Vector3.zero;
            Velocity = Vector3.zero;
            previousPosition = transform.position;
        }
    
    
        private void UpdateRotation()
        {
            Quaternion current = transform.rotation;
            RotationDelta = current * Quaternion.Inverse(previousRotation);
            previousRotation = current;
        }
    
        private void ResetRotation()
        {
            RotationDelta = Quaternion.identity;
            previousRotation = transform.rotation;
        }
    
    
        /// <summary>
        /// World displacement this frame at a point, combining translation and the orbital shift from rotation.
        /// Fold into the controller's movement vector so it passes through collision resolution.
        /// </summary>
        /// <param name="worldPos">World-space point to sample.</param>
        public Vector3 GetPointDelta(Vector3 worldPos)
        {
            Vector3 pivotOffset = worldPos - previousPosition;
            Vector3 rotational = (RotationDelta * pivotOffset) - pivotOffset;
            return PositionDelta + rotational;
        }

    
        /// <summary>
        /// GetPointDelta as a per-second velocity, including the tangential speed from rotation.
        /// Used for momentum hand-off.
        /// </summary>
        /// <param name="worldPos">World-space point to sample.</param>
        public Vector3 GetPointVelocity(Vector3 worldPos)
        {
            float dt = Time.deltaTime;
            return dt > 0f ? GetPointDelta(worldPos) / dt : Vector3.zero;
        }

    
        /// <summary>Heading rotation a rider inherits from this surface this frame, to keep its facing aligned with the wall (geometric orbital arc if isSelfSpinningCylinder, otherwise the raw frame rotation).</summary>
        /// <param name="riderPos">World position of the rider.</param>
        /// <param name="intensity">Scales the geometric arc (external wall-run hook).</param>
        public Quaternion WallHeadingDelta(Vector3 riderPos, float intensity = 1f) => isSelfSpinningCylinder ? GeometricHeadingDelta(riderPos, intensity) : RotationDelta;

    
    
        // isSelfSpinningCylinder exception related logic
        // Previous rider sample for when using isSelfSpinningCylinder. Reset on a fresh bind via ResetGeometricSample.
        private Vector3 prevRiderPos;
        private Vector3 prevSampleCenter;
        private bool hasGeometricSample;
    
    
    
        /// <summary>
        /// Drop the geometric sample so the next ride starts clean. Call on a fresh bind.
        /// </summary>
        public void ResetGeometricSample() => hasGeometricSample = false;

    
        /// <summary>
        /// Geometric wall yaw for self-spinning cylinders, the signed arc the rider sweeps around the cylinder's spin axis this frame as a quaternion around that axis.
        /// </summary>
        /// <param name="riderPos">World position of the rider.</param>
        /// <param name="intensity">Scales the resulting arc.</param>
        private Quaternion GeometricHeadingDelta(Vector3 riderPos, float intensity)
        {
            Vector3 center = transform.position;
        
            // The spin axis is the orbit axis. A near-still spin has no defined axis → nothing to inherit this frame.
            RotationDelta.ToAngleAxis(out float spinAngle, out Vector3 axis);
            if (spinAngle <= 1e-3f || axis.sqrMagnitude < 1e-6f)
            {
                prevRiderPos = riderPos;
                prevSampleCenter = center;
                hasGeometricSample = true;
                return Quaternion.identity;
            }
            axis.Normalize();

            Vector3 relCurr = Vector3.ProjectOnPlane(riderPos - center, axis);
            if (relCurr.sqrMagnitude < 1e-6f) { hasGeometricSample = false; return Quaternion.identity; }

            float angle = 0f;
            if (hasGeometricSample)
            {
                Vector3 relPrev = Vector3.ProjectOnPlane(prevRiderPos - prevSampleCenter, axis);
                if (relPrev.sqrMagnitude >= 1e-6f) angle = Vector3.SignedAngle(relPrev, relCurr, axis);
                angle *= intensity; // external wall-run hook scales the arc
            }

            prevRiderPos = riderPos;
            prevSampleCenter = center;
            hasGeometricSample = true;
            return Quaternion.AngleAxis(angle, axis);
        }
    }
}
