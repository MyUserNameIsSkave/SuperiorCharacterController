using UnityEngine;

// ReSharper disable once CheckNamespace
namespace SuperiorCharacterController
{
    /// <summary>
    /// Stateless motion math for <see cref="SCC_Controller"/>. The pure, frame-agnostic functions that depend
    /// only on their arguments. Extracted from the controller to keep it focused on the per-frame collision/carry
    /// pipeline. Everything here is built on an explicit up axis, so it stays correct under arbitrary gravity.
    /// </summary>
    public static class SCC_Math
    {
        /// <summary>
        /// Scale the vertical part of an inherited inertia vector by a platform up/down intensity. Horizontal carry is untouched.
        /// </summary>
        public static Vector3 ScaleVerticalInertia(Vector3 inertia, Vector3 up, float upScale, float downScale)
        {
            float inertiaUpDot = Vector3.Dot(inertia, up);
            float scaled = inertiaUpDot  >= 0f ? inertiaUpDot * upScale : inertiaUpDot * downScale;
            return inertia + up * (scaled - inertiaUpDot);
        }

        /// <summary>
        /// Signed yaw a turning platform imparts around an axis, correct even when the body is tilted under arbitrary gravity
        /// .</summary>
        /// <param name="rotationDelta">The platform's rotation this frame in degrees.</param>
        /// <param name="axis">The axis to measure the twist around, usually the SCC up.</param>
        public static float YawAroundAxis(Quaternion rotationDelta, Vector3 axis)
        {
            Vector3 rotationVector = new Vector3(rotationDelta.x, rotationDelta.y, rotationDelta.z);
            Vector3 proj = Vector3.Project(rotationVector, axis);
            Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, rotationDelta.w);
        
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w < 1e-8f)
                return 0f; // degenerate (≈180° swing with no twist component)
        
            twist.Normalize();
            float angle = 2f * Mathf.Acos(Mathf.Clamp(twist.w, -1f, 1f)) * Mathf.Rad2Deg;
        
            if (Vector3.Dot(proj, axis) < 0f) 
                angle = -angle;
        
            return Mathf.DeltaAngle(0f, angle); // wrap to [-180, 180]
        }

        /// <summary>
        /// Along-up speed that keeps a horizontal velocity on a slope plane. Returns 0 for a vertical wall.
        /// </summary>
        /// <param name="horizontal">The velocity perpendicular to <paramref name="up"/>.</param>
        /// <param name="normal">The normal of the slope to stay consistent with.</param>
        /// <param name="up">The current up axis.</param>
        public static float SlopeConsistentUp(Vector3 horizontal, Vector3 normal, Vector3 up)
        {
            float normalUpDot = Vector3.Dot(normal, up);
            return Mathf.Abs(normalUpDot) > 0.0001f ? -Vector3.Dot(horizontal, normal) / normalUpDot : 0f;
        }


        /// <summary>
        /// Redirect a world velocity to follow a walkable slope, carrying the external vertical (jump/gravity) through verbatim. Correct for any gravity direction.
        /// </summary>
        /// <param name="incomingVelocity">The velocity to redirect.</param>
        /// <param name="slopeNormal">The slope to align onto.</param>
        /// <param name="up">The current up axis.</param>
        /// <param name="flatten">If true, preserve the flat input speed going up the slope (the climb consume speed) instead of rescaling to total speed.</param>
        /// <param name="notWhenGoingUp">Restricts <paramref name="flatten"/> to upward travel only.</param>
        public static Vector3 AlignVelocityWithSlope(Vector3 incomingVelocity, Vector3 slopeNormal, Vector3 up, bool flatten, bool notWhenGoingUp)
        {
            // Split into the up-plane horizontal and the along-up part
            Vector3 horizontalInput = Vector3.ProjectOnPlane(incomingVelocity, up);
            float upInput = Vector3.Dot(incomingVelocity, up);
            float normalUpDot = Vector3.Dot(slopeNormal, up);

            // No movement or normal with no up component (wall)
            if (horizontalInput.sqrMagnitude < 0.0001f || Mathf.Abs(normalUpDot) < 0.0001f)
                return incomingVelocity;
        
            float correctUp = SlopeConsistentUp(horizontalInput, slopeNormal, up);
            Vector3 aligned = horizontalInput + up * correctUp;

            // Preserve total speed equal to the flat input speed, unless flattening is requested for this case.
            if (!flatten || (notWhenGoingUp && correctUp > 0f))
                aligned = aligned.normalized * horizontalInput.magnitude;

            // Add the external vertical (jump/gravity) on top, along up.
            return aligned + up * upInput;
        }
    }
}
