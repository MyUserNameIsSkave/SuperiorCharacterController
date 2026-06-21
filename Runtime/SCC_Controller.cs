using DG.Tweening;
using SuperiorCharacterController.Attributes;
using UnityEngine;

namespace SuperiorCharacterController
{
    // ReSharper disable InconsistentNaming
    // Run AFTER everything else 
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CapsuleCollider))]
    public class SCC_Controller : MonoBehaviour
    {
        #region Inspector — Collision
        [Tab("Collision")]
        [Header("     BODY")]
        [SerializeField] private float height = 2f;
        [SerializeField] private float radius = 0.5f;
        [Space(10)]
        [SerializeField] private Vector3 center = new Vector3(0f, 1f, 0f);

        [Space(10)]
        [Header("     COLLISION")]
        [SerializeField] private LayerMask toIgnore;
        [SerializeField] private float skinWidth = 0.01f;
        [Space(5)]
        [SerializeField] private float maxSlopeAngle = 50f;
        [Tooltip("Angle range (from vertical) classified as a wall. Outside this range means floor or ceiling.")]
        [SerializeField, MinMaxRangeSlider(0, 180f)] Vector2 maxWallAngle = new Vector2(80, 100);

        [Space(10)]
        [Header("     PROBE DISTANCES")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        [Tooltip("Only used to update CC state, doesn't affect collisions.")]
        [SerializeField] private float wallCheckDistance = 0.05f;
        [Tooltip("Only used to update CC state, doesn't affect collisions.")]
        [SerializeField] private float ceilingCheckDistance = 0.05f;
        [Space(5)]
        [SerializeField, EnableIf("handleStepUp")]  private float maxStepHeight      = 0.4f;  [EndIf]
        [SerializeField, EnableIf("handleGroundSnap")] private float maxSnapDistance = 0.4f; [EndIf]


        #endregion
    
    
        #region Inspector — Gravity
        [Tab("Gravity")]
        [Header("     GRAVITY")]
        [SerializeField] private Vector3 upDirectionTarget = Vector3.up;
        [Tooltip("Pull gravity along the target up immediately, skipping the transition animation.")]
        [SerializeField] private bool gravityUsesTargetUp = false;
        [Space(5)]
        [Tooltip("Gravity strength along the controller's own up axis. Not auto-managed by the controller — drive it yourself at runtime (e.g. low-gravity zones, gravity guns).")]
        public float currentGravity = 45f;
        [Tooltip("0 disable the limit.")]
        [SerializeField] private float maxFallSpeed = 50f;

        [Space(15)]
        [Header("     TRANSITION")]
        [Tooltip("Using the camera transform is advised for smoother rotation.")]
        [SerializeField] private Transform upRotationPivot;
        [Space(5)]
        [SerializeField] private float upAlignDuration = 0.25f;
        [SerializeField] private Ease upAlignEase = Ease.InOutSine;


        #endregion
    
    
        #region Inspector — Motion
        [Tab("Motion")]
        [Header("     STEP & SNAP")]
        [SerializeField] private bool handleStepUp = true;
        [Tooltip("Restrict step-up to when the character is already grounded.")]
        [SerializeField, EnableIf("handleStepUp")] private bool onlyWhenGrounded = true; [EndIf]
        [Space(5)]
        [Tooltip("Only affect steps.")]
        [SerializeField] private bool handleGroundSnap = true;

        [Space(20)]
        [Header("     SLOPES")]
        [Tooltip("Keep the initial horizontal velocity instead of smashing it against the slope.")]
        [SerializeField] private bool flattenVelocityOnSlope = true;
        [SerializeField, EnableIf("flattenVelocityOnSlope")] private bool onlyWhenGoingDown = true; [EndIf]
        [Space(5)]
        [Tooltip("Apply the vertical velocity when leaving a slope to make the transition smoother.")]
        [SerializeField] private bool preserveSlopeInertia = true;
        [EnableIf("preserveSlopeInertia")]
        [SerializeField] private float slopeInertiaUpScale   = 1f;
        [SerializeField] private float slopeInertiaDownScale = 1f;
        [EndIf]
        [Space(5)]
        [Tooltip("Unground when the floor normal changes sharply from uphill to downhill.")]
        [SerializeField] private bool ungroundOnCrest = true;
        [Tooltip("Minimum angular change between frames (degrees) that triggers the ungrounding.")]
        [SerializeField, EnableIf("ungroundOnCrest")] private float crestAngle = 60f; [EndIf]
        [Space(5)]
        [Tooltip("Horizontal push speed (m/s) away from the ledge contact applied when the character steps off slowly. Helps the capsule clear the edge and fall cleanly when standing right on the edge. Prevent jittering.")]
        [SerializeField] private float ledgeNudgeStrength = 0.4f;
        [Tooltip("Max horizontal speed (m/s) at which the ledge assist triggers. Above this the character is already moving fast enough to fall cleanly on its own.")]
        [SerializeField] private float ledgeAssistMaxSpeed = 2f;

        [Space(20)]
        [Header("     CEILING")]
        [Tooltip("Zero the upward velocity on ceiling contact to prevent sticking.")]
        [SerializeField] private bool neutralizeJumpOnCeiling = true;

        [Space(20)]
        [Header("     RIGIDBODY PUSH")]
        [Tooltip("Push non-kinematic rigidbodies that have an SCC_Pushable component. Objects without the component are treated as static blockers.")]
        [SerializeField] private bool pushRigidbodies = true;
        [EnableIf("pushRigidbodies")]
        [Tooltip("Extra radius around the capsule used to reliably detect rigidbodies ramming into the character (IncomingResistance). Small values are imperceptible; too large slows objects before they touch.")]
        [SerializeField] private float pushDetectionMargin = 0.05f;
        [EndIf]


        #endregion
    
    
        #region Inspector — Dynamic Surfaces
        [Tab("Dynamic Surfaces")]
        [Header("     CORE")]
        [SerializeField] private bool handleDynamicSurfaces = true;
        [Space(5)]
        [Tooltip("Be physically carried by the surface's translation. The SCC Still get pushed around by DynamicSurface equipped moving elements.")]
        [SerializeField, EnableIf("handleDynamicSurfaces")] private bool followSurfacePosition = true; [EndIf]
        [Tooltip("Inherit the surface's yaw/heading. Disable to stand on a spinning platform without turning with it. The SCC Still get pushed around by DynamicSurface equipped moving elements.")]
        [SerializeField, EnableIf("handleDynamicSurfaces")] private bool followSurfaceRotation = true; [EndIf]

        [Space(20)]
        [Header("     PLATFORM")]
        [Tooltip("When carried into a collision, slide along it instead of stopping.")]
        [SerializeField] private bool slidePlatformMotionAlongCollisions = true;
        [Header("          Movement")]
        [Tooltip("Inherit the platform's velocity on leave.")]
        [SerializeField] private bool transferPlatformMomentum = true;
        [SerializeField, EnableIf("transferPlatformMomentum")] private float platformInertiaUpScale   = 1f; [EndIf]
        [SerializeField, EnableIf("transferPlatformMomentum")] private float platformInertiaDownScale = 1f; [EndIf]
        [Space(5)]
        [Tooltip("Allow sharp vertical movement changes to unground and launch the CC.")]    
        [SerializeField] private bool handlePlatformLaunch = true;
        [Space(5)]
        [Tooltip("Vertical speed delta (m/s per frame) required to trigger a launch.")]
        [SerializeField, EnableIf("handlePlatformLaunch")] private float platformLaunchThreshold  = 10f; [EndIf]
        [SerializeField, EnableIf("handlePlatformLaunch")] private float platformLaunchForceScale = 1f; [EndIf]
        [Header("          Rotation")]
        [Tooltip("Inherit the platform's yaw spin on leave.")]
        [SerializeField] private bool transferPlatformRotation = true;
        [Tooltip("Exponential decay rate of the inherited spin.")]
        [SerializeField, EnableIf("transferPlatformRotation")] private float platformRotationDecay = 2f; [EndIf]


        [Space(20)]
        [Header("     WALL")]
        [Header("          Movement")]
        [SerializeField] private bool transferWallMomentum = true;
        [SerializeField, EnableIf("transferWallMomentum")] private float wallInertiaUpScale   = 1f; [EndIf]
        [SerializeField, EnableIf("transferWallMomentum")] private float wallInertiaDownScale = 1f; [EndIf]
        [Header("          Rotation")]
        [Tooltip("Inherit the wall's yaw spin on leave.")]
        [SerializeField] private bool transferWallRotation = true;
        [Tooltip("Exponential decay rate of the inherited spin.")]
        [SerializeField, EnableIf("transferWallRotation")] private float wallRotationDecay = 2f; [EndIf]

        [Space(20)]
        [Header("     SHARED")]
        [Tooltip("Clear inherited rotation inertia when landing.")]
        [SerializeField] private bool stopRotationInertiaOnGround = true;
        [Tooltip("Clear inherited rotation inertia when hitting a wall.")]
        [SerializeField] private bool stopRotationInertiaOnWall = true;
        [Space(5)]
        [Tooltip("Inherit velocity when pushed by a moving collider.")]
        [SerializeField] private bool transferPushMomentum = true;
        [SerializeField, EnableIf("transferPushMomentum")] private float pushMomentumScale = 1f;

        [EndIf]

        #endregion


        #region Debug visualization

        const bool falseBool = false;
        
        [Tab("Settings", "Settings")]
        [Header("     VELOCITY")]
        [EnableIf("falseBool")]
        public Vector3 AskedLocalVelocity;
        public Vector3 appliedVelocity;
    #if UNITY_EDITOR
        [Space(5)]
        public float currentSpeed;
        public float currentFlattenedSpeed;
    #endif

        [Space(10)]
        [Header("     CONTACT")]
        [ShowInInspector] private bool wasGrounded;
        public bool isGrounded;
        public float groundAngle;
        [Space(5)]
        [ShowInInspector] private bool hasSteppedUp;
        [Space(5)]
        public bool isOnWall;
        public bool shouldStickToWall;
        public bool isSticking;
        [Space(5)]
        public bool isUnderCeiling;
        
        [EndIf]
        #endregion
    
    
        [HideInInspector] public Vector3 wallNormal;
        /// <summary>
        /// Nearest wall contact point from the last probe. Not averaged, and not cleared on loss, so an external probe can keep aiming at the wall through a momentary contact interruption.
        /// </summary>
        [HideInInspector] public Vector3 lastWallCollisionPoint;
        [HideInInspector] public Vector3 ceilingNormal;
    

        #region Private State
        // Actual up axis the whole controller reasons with; lerps toward upDirectionTarget each frame.
        private Vector3 currentUpDirection = Vector3.up;

        private Vector3 groundNormal = Vector3.up;
        private Vector3 lastValidGroundNormal  = Vector3.up;
        private Vector3 previousGroundNormal   = Vector3.up;
    
        private bool crestLaunched;
        private Vector3 lastEdgeContactDir; 

        // Floor DynamicSurface under the feet (null = airborne / none). Set in GroundCheck, ridden next frame in ApplySurfaceCarry.
        private SCC_DynamicSurface currentSurface;
        private Vector3 surfaceMomentum; 

        // Floor yaw (deg) imparted last grounded frame, kept so the leave frame can seed the inherited spin
        // even though currentSurface is already null by then.
        private float lastPlatformYawDelta; 
        private float inheritedFloorYawVelocity; // decay rate: platformRotationDecay
        private float inheritedWallYawVelocity;  // decay rate: wallRotationDecay

        // Momentum hand-off only fires after standing on the SAME surface for MomentumStabilityTime
        // so a one-frame edge clip can't fling us with the platform's velocity.
        private const float MomentumStabilityTime = 0.05f;
        private SCC_DynamicSurface momentumStableSurface;
        private float groundedSurfaceTime;

        // Tracks getting pushed by a surface we're not standing on (ResolvePenetrations), e.g. a wall shoving into us.
        private SCC_DynamicSurface currentPushSurface; // this frame's pusher (null = not pushed)
        private Vector3 currentPushMomentum;
        private Vector3 lastPushMomentum;          // committed on release
        private bool wasPushed;                     // edge-detects the release
        private SCC_DynamicSurface pushStableSurface;   // surface the push-stability timer counts against
        private float pushedTime;

        // Carry applied this frame (zero when not riding) and the one-shot deduction CollideAndSlide consumes on
        // the leave frame — the commit injects the platform velocity while the carry already moved us by its delta,
        // so without the deduction the leave frame travels at ~2× platform speed.
        private Vector3 carryDeltaThisFrame;
        private Vector3 momentumCarryComp;
        private float momentumCommittedY;

        // Fraction (0..1) of the platform's rotation-orbit that survived the carry's slide-against-static. The
        // inherited yaw is scaled by it: pinned at a wall so the orbit can't complete → the heading follows the
        // spin only as far as we actually moved. 1 when unobstructed or there's no orbit.
        private float carryRotationFraction = 1f;

        // Platform-launch tracking: a sharp frame-to-frame drop in the SAME surface's vertical velocity triggers
        // the unground-and-fling; launchedThisFrame tells HandleSurfaceMomentum to use the launch force scale.
        private float previousSurfaceVUp;
        private SCC_DynamicSurface launchTrackSurface;
        private bool launchedThisFrame;

        // Grace after a ground correction (step-up / snap) where the momentum hand-off is muted: the correction
        // re-grounds us, but a ground-check blink around it would otherwise read as a real leave and fling us.
        private const float LeaveSuppressGrace = 0.06f;
        private float leaveSuppressTimer;

        // Cancellable momentum committed THIS frame (launch excluded): if a step-up/snap re-grounds us the same
        // frame we didn't really leave, so it's subtracted back. Catches what the grace window (armed later) can't.
        private Vector3 committedMomentumThisFrame;

        // Yaw (deg) the base rotated this frame while we stand on it, for the look system to follow a turning
        // platform (the positional orbit is already applied by the carry). Zero when not riding.
        public float platformYawDelta;

        // Persistent body heading, driven by the look system through ApplyLookYaw. A full quaternion rotated
        // INCREMENTALLY around currentUpDirection, so the facing keeps following the up axis once it leaves world
        // up (gravity walking, wall-running). Seeded lazily from the startup rotation.
        private Quaternion headingRotation = Quaternion.identity;
        private bool hasHeading;

        // Latched while a reorientation's position-orbit is suppressed because it would embed the capsule (see
        // UpdateUpAlignment); held until the reorientation settles so the body pivots about the feet throughout.
        private bool orbitBlocked;

        // Up-change tween state (timed + eased): reorientTarget is what we animate toward; a target change
        // re-captures reorientStartUp and restarts reorientTimer.
        private Vector3 reorientStartUp = Vector3.up;
        private Vector3 reorientTarget = Vector3.up;
        private float reorientTimer;

        // Our own capsule — the reference Physics.ComputePenetration needs. Found/created in Awake, kept in sync
        // with height/radius/center. When auto-created it's a trigger: it never physically interacts and our sweeps
        // ignore it natively.
        private CapsuleCollider ownCollider;
        private readonly Collider[] penetrationOverlaps = new Collider[8];

        // Reusable NonAlloc cast buffers, kept as fields so the per-frame probes/sweeps don't allocate each call.
        private readonly RaycastHit[] groundHitBuffer = new RaycastHit[8];
        private readonly RaycastHit[] slideHitBuffer = new RaycastHit[8];
        private readonly Vector3[] collisionNormals = new Vector3[5]; // CollideAndSlide bounce normals (size = maxBounces)

        // How far (skin widths) the CollideAndSlide cast origin is pulled back along the movement so a one-frame
        // embed from a moving collider is still caught. Higher = catches deeper embeds, at more risk of starting
        // inside geometry behind.
        private const float BackstepSkinFactor = 5f;

        // Reference fps for IncomingResistance's per-frame decay, so the slowdown feels the same at any frame rate.
        private const float PushDecayReferenceFps = 60f;

        private const int WallProbeCount = 8; // horizontal sweeps around upDirection, detection only
        private readonly RaycastHit[] wallHitBuffer = new RaycastHit[8];

        // Persistent wall ride (anchor-based, mirrors the floor carry): once bound we follow TransformPoint(anchor)
        // every frame, immune to the probe missing when the wall recedes. isWallCarrying gates the de-pen skip and
        // the release hand-off; wallMomentum is the ride velocity committed on release.
        private SCC_DynamicSurface ridingWall;
        private Vector3 wallAnchorLocal;
        private bool isWallCarrying;
        private Vector3 wallMomentum;
        private bool wallStickBlocked; // latched when the ride clips us inside a collider; cleared on input release
        // External hook: the Wall Run gameplay layer writes a scale for the geometric wall yaw here each frame; it is
        // passed to SCC_DynamicSurface.WallHeadingDelta and reset to 1 after. REMOVE when extracting the CC as a package.
        [HideInInspector] public float wallRunningYawMultiplier = 1f;
        // Heading the wall-ride imparts this frame, about the cylinder's real spin axis (not up, which drifts when
        // gravity tilts). Set in HandleSurfaceMomentum, applied + reset in ApplyLookYaw.
        private Quaternion ridingWallHeadingDelta = Quaternion.identity;
        private readonly float wallStickBreakPenetration = 0.1f; // max depth tolerated before the safety drops the stick

        private readonly RaycastHit[] ceilingHitBuffer = new RaycastHit[8]; // one upward sweep, detection only

        // Where we stand ON the surface, in its local space. Following TransformPoint(anchor) rather than
        // integrating per-frame deltas is drift-free and self-correcting against script order.
        private Vector3 surfaceAnchorLocal;
        private bool hasSurfaceAnchor;

        private Vector3 previousPosition;


        #endregion

        #region Properties
        // Godot CharacterBody3D–style query API. Prefer these in external scripts over the raw public fields
        // (those stay public only for the Inspector/debug readout). Wall/ceiling state comes from dedicated probes
        // (CheckWallContact / CheckCeilingContact), not CollideAndSlide, and updates every frame regardless of movement.

        public bool IsOnGround => isGrounded;
        public bool IsOnGroundOnly => isGrounded && !isOnWall && !isUnderCeiling;

        /// <summary>
        /// Touching a wall (8 horizontal probes), even when not moving toward it.
        /// </summary>
        public bool IsOnWall => isOnWall;
        public bool IsOnWallOnly => isOnWall && !isGrounded && !isUnderCeiling;

        /// <summary>
        /// Ceiling overhead (upward probe), even when not moving up.
        /// </summary>
        public bool IsOnCeiling => isUnderCeiling;
        public bool IsOnCeilingOnly => isUnderCeiling && !isGrounded && !isOnWall;

        /// <summary>
        /// On ground too steep to walk (treated as a wall by CollideAndSlide).
        /// </summary>
        public bool IsOnSteepSlope => isGrounded && groundAngle > maxSlopeAngle;

        /// <summary>
        /// Sticking to and carried by a dynamic wall.
        /// </summary>
        public bool IsWallRiding => isWallCarrying;

        /// <summary>
        /// Standing on a dynamic floor (dynamic walls are tracked separately, via RidingWall).
        /// </summary>
        public bool IsOnDynamicSurface => currentSurface != null;

        /// <summary>
        /// World ground normal, zero when airborne.
        /// </summary>
        public Vector3 GroundNormal => isGrounded ? groundNormal : Vector3.zero;
        public float GroundAngle => groundAngle;
        /// <summary>
        /// Averaged world normal of the probed wall(s), zero when none.
        /// </summary>
        public Vector3 WallNormal => wallNormal;
        /// <summary>
        /// Layers the controller's queries skip. Reuse for external probes so they ignore the same colliders (incl. self).
        /// </summary>
        public LayerMask IgnoreMask  => toIgnore;
        public float Radius => radius;
        /// <summary>
        /// Wall band (deg between a surface normal and up) treated as a wall. Reuse so external probes reject floors/ceilings the same way.
        /// </summary>
        public Vector2 MaxWallAngle => maxWallAngle;
        public Vector3 CeilingNormal => ceilingNormal;

        /// <summary>
        /// Actual world velocity this frame (position delta / deltaTime).
        /// </summary>
        public Vector3 RealVelocity => appliedVelocity;

        /// <summary>
        /// DynamicSurface ridden as a floor, or null.
        /// </summary>
        public SCC_DynamicSurface RiddenSurface => currentSurface;
        /// <summary>
        /// DynamicSurface ridden as a wall, or null.
        /// </summary>
        public SCC_DynamicSurface RidingWall => ridingWall;

        /// <summary>
        /// AskedLocalVelocity in world space. Prefer over manual Local/World conversions when setting velocity from world quantities (wall normals, camera directions).
        /// </summary>
        public Vector3 AskedWorldVelocity
        {
            get => BodyToWorld(AskedLocalVelocity);
            set => AskedLocalVelocity = WorldToBody(value);
        }
        #endregion


        #region Lifecycle
        // Called in edit mode on every Inspector change and after recompilation — keeps the collider
        // locked and in sync with the serialized dimensions without needing to enter Play mode.
        private void OnValidate() => SyncCollider();

        /// <summary>
        /// Keep the CapsuleCollider in sync with the serialized dimensions and lock it from the Inspector. Called from both OnValidate (edit mode) and Awake (runtime).
        /// </summary>
        private void SyncCollider()
        {
            if (ownCollider == null) ownCollider = GetComponent<CapsuleCollider>();
            if (ownCollider == null) return;
            ownCollider.isTrigger  = true; // purely a geometric reference for ComputePenetration, never physical
            ownCollider.hideFlags  = HideFlags.NotEditable;
            ownCollider.center     = center;
            ownCollider.height     = height;
            ownCollider.radius     = radius;
            ownCollider.direction  = 1; // Y axis
        }

        private void Awake()
        {
            SyncCollider();

            // ORIENT AT SPAWN INSTANTLY (runtime changes lerp via UpdateUpAlignment).
            if (upDirectionTarget.sqrMagnitude < 1e-6f) upDirectionTarget = Vector3.up;
            upDirectionTarget.Normalize();
            currentUpDirection = upDirectionTarget;
            reorientTarget = currentUpDirection;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, currentUpDirection);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(transform.right, currentUpDirection);
            transform.rotation = Quaternion.LookRotation(forward.normalized, currentUpDirection);
            headingRotation = transform.rotation;
            hasHeading = true;
        }

        private void Update()
        {
            committedMomentumThisFrame = Vector3.zero;

            // Must precede SyncTransforms and every cast below (which read upDirection).
            UpdateUpAlignment();

            // SYNC — movers moved the platform transforms this frame, but the physics QUERY world only
            // sees them at the next FixedUpdate (autoSyncTransforms off). Without this, every cast below runs against
            // the stale collider pose → ungrounding mid-descent / seat jitter on rising platforms.
            Physics.SyncTransforms();

            // CARRY BEFORE GroundCheck — a rising platform would otherwise penetrate the capsule first, and sweeps
            // ignore initial overlaps, so GroundCheck would go blind and the platform rises through us. Carrying
            // first keeps the feet glued so GroundCheck always sees the surface.
            ApplySurfaceCarry();

            SCC_DynamicSurface wallCandidate = CheckWallContact(); // pure read; isOnWall reflects the post-carry pose
            TryWallStick(wallCandidate);                       // bind/ride a dynamic wall while stickToWall is held (before de-pen, like the carry)
            // Grace: the probe can miss a frame on a fast-spinning cylinder; a live anchor (isSticking) still means contact.
            if (isSticking) isOnWall = true;
            CheckCeilingContact();

            ResolvePenetrations();
            HandlePushMomentum();

            GroundCheck();

            // LEDGE FALL ASSIST — only when nearly stationary at a ledge edge (a moving character clears it on its
            // own). Nudge the capsule away from the contact point so it falls cleanly instead of jittering on the edge.
            float flatSpeed = Vector3.ProjectOnPlane(BodyToWorld(AskedLocalVelocity), currentUpDirection).magnitude;
            if (wasGrounded && !isGrounded && AskedLocalVelocity.y <= 0.1f && flatSpeed <= ledgeAssistMaxSpeed)
            {
                if (lastEdgeContactDir.sqrMagnitude > 0.01f)
                    AskedWorldVelocity += -lastEdgeContactDir * ledgeNudgeStrength;
            }

            CheckCrestUnground();
            CheckPlatformLaunch();
            HandleSurfaceMomentum();
            ApplyGravity();

            CollideAndSlide();

            // Slow rigidbodies ramming INTO the character (RB → SCC). Runs after CollideAndSlide so the
            // character's own push (SCC → RB) is already scheduled and read back via ScheduledVelocity.
            DecelerateIncomingPushables();

            ReconcileLandingVelocity();

            // Re-anchor AFTER the final position so next frame's carry starts from where we actually rest.
            UpdateSurfaceAnchor();
            UpdateWallAnchor();

            DebugValues();
        }

        /// <summary>
        /// On the landing frame, convert AskedLocalVelocity from absolute to surface-relative, so it doesn't accelerate strangely against the platform. Runs after CollideAndSlide so the landing frame still travels at the absolute velocity (no stall); vertical is left to the gravity clamp.
        /// </summary>
        private void ReconcileLandingVelocity()
        {
            if (!followSurfacePosition || wasGrounded || !isGrounded || currentSurface == null) return;
            AskedLocalVelocity -= WorldToBody(Vector3.ProjectOnPlane(currentSurface.GetPointVelocity(transform.position), currentUpDirection));
        }
    
    #if UNITY_EDITOR
        private void DebugValues()
        {
            if (Time.deltaTime <= 0f) return;

            appliedVelocity = (transform.position - previousPosition) / Time.deltaTime;
            previousPosition = transform.position;
            currentSpeed = appliedVelocity.magnitude;
            currentFlattenedSpeed = Vector3.ProjectOnPlane(appliedVelocity, currentUpDirection).magnitude;
            groundAngle = isGrounded ? Vector3.Angle(currentUpDirection, groundNormal) : 0f;
        }
    #endif
    
    
        #endregion

        #region Ground
        /// <summary>
        /// Sphere-cast straight down to find walkable ground under the feet, set isGrounded / groundNormal / currentSurface.
        /// </summary>
        private void GroundCheck()
        {
            wasGrounded = isGrounded;

            // DESCENT TOLERANCE — extend every downward range by the surface's drop this frame, so a descending
            // platform the carry couldn't fully follow doesn't unground us mid-ride. Zero for static/rising ground.
            float surfaceDrop = CurrentSurfaceDrop();

            isGrounded = false;
            currentSurface = null;

            Vector3 sphereBase = transform.position + currentUpDirection * radius;
            int hitCount = Physics.SphereCastNonAlloc(sphereBase, radius, -currentUpDirection, groundHitBuffer,
                groundCheckDistance + surfaceDrop, ~toIgnore);

            for (int h = 0; h < hitCount; h++)
            {
                RaycastHit hit = groundHitBuffer[h];

                // Re-probe with a short straight-down ray, offset outward past the contact, to sample the flat FACE
                // (clean normal) not the skewed edge corner. Short so it can't reach a lower floor across a step edge.
                Vector3 toHit = Vector3.ProjectOnPlane(hit.point - transform.position, currentUpDirection);
                Vector3 probeOffset = toHit.sqrMagnitude > 1e-4f ? toHit.normalized * skinWidth : Vector3.zero;
                Vector3 probeOrigin = hit.point + probeOffset + currentUpDirection * skinWidth;
                float probeLength = groundCheckDistance + skinWidth * 2f + surfaceDrop;

                if (!Physics.Raycast(probeOrigin, -currentUpDirection, out var probeHit, probeLength, ~toIgnore))
                    continue;

                // EDGE GUARD — surface more than a radius to the side (horizontal) means we're hanging over the
                // edge and the ground is beside, not below us. Reject so "grounded" doesn't persist off a ledge.
                Vector3 horizontalToHit = Vector3.ProjectOnPlane(probeHit.point - transform.position, currentUpDirection);
                if (horizontalToHit.magnitude > radius)
                    continue;

                // Reject surfaces too far below for snap to reach (anti-levitation). No lower bound: float noise can
                // put a valid flat hit fractionally above the feet. Measured ALONG upDirection (arbitrary gravity).
                float groundDist = Vector3.Dot(transform.position - probeHit.point, currentUpDirection);
                if (groundDist > groundCheckDistance + skinWidth + surfaceDrop)
                    continue;

                float angle = Vector3.Angle(currentUpDirection, probeHit.normal);
                if (angle > maxSlopeAngle)
                    continue;

                isGrounded = true;
                groundNormal = probeHit.normal;
                lastValidGroundNormal = groundNormal;
                // Resolve the surface from the SPHERE CAST hit, not the offset probe ray: the ray can land on a riser /
                // adjacent collider (→ null surface → carry blinks off on stairs/platforms). The sphere cast is the real contact.
                currentSurface = ResolveDynamicSurface(hit.collider);
                lastEdgeContactDir = horizontalToHit.sqrMagnitude > 1e-4f ? horizontalToHit.normalized : Vector3.zero;
                break;
            }

            // CENTER-LINE FALLBACK
            if (!isGrounded)
            {
                Vector3 centerOrigin = transform.position + currentUpDirection * skinWidth;
                if (Physics.Raycast(centerOrigin, -currentUpDirection, out var centerHit,
                        groundCheckDistance + skinWidth * 2f + surfaceDrop, ~toIgnore)
                    && Vector3.Angle(currentUpDirection, centerHit.normal) <= maxSlopeAngle)
                {
                    isGrounded = true;
                    groundNormal = centerHit.normal;
                    lastValidGroundNormal = groundNormal;
                    currentSurface = ResolveDynamicSurface(centerHit.collider);
                }
            }

            if (!isGrounded)
            {
                groundNormal = currentUpDirection;
            }

            if (crestLaunched && isGrounded)
            {
                if (AskedLocalVelocity.y > 0f)
                {
                    isGrounded = false;
                    currentSurface = null;
                    groundNormal = currentUpDirection;
                }
                else
                {
                    crestLaunched = false;
                }
            }

        }


        /// <summary>
        /// The DynamicSurface to ride for a ground collider, or null. Checks the collider then walks up the hierarchy (collider on a child, DynamicSurface on the moving parent).
        /// </summary>
        /// <param name="groundCollider">Collider hit by the ground probe.</param>
        private SCC_DynamicSurface ResolveDynamicSurface(Collider groundCollider)
        {
            if (!handleDynamicSurfaces) return null;
            if (groundCollider == null) return null;
            return groundCollider.TryGetComponent(out SCC_DynamicSurface surface) ? surface : groundCollider.GetComponentInParent<SCC_DynamicSurface>();
        }


        /// <summary>
        /// How far the followed floor surface dropped along up this frame (0 if none, rising, or not position-followed). Downward probes extend their reach by this so a fast descender stays grounded.
        /// </summary>
        private float CurrentSurfaceDrop()
        {
            if (!followSurfacePosition || currentSurface == null) return 0f;
            return Mathf.Max(0f, -Vector3.Dot(currentSurface.PositionDelta, currentUpDirection));
        }


        #endregion

        #region Surface Carry
        /// <summary>
        /// Move with the dynamic surface we stand on. Anchor-based: move toward where our stored stand-point sits NOW on the platform's transform, rather than integrating per-frame deltas — drift-free, rotation orbit included, self-correcting.
        /// </summary>
        private void ApplySurfaceCarry()
        {
            carryDeltaThisFrame = Vector3.zero;
            carryRotationFraction = 1f;
            if (!followSurfacePosition || currentSurface == null || !hasSurfaceAnchor) return;

            Vector3 delta = SurfaceFollowDelta(currentSurface, surfaceAnchorLocal);
            if (delta.sqrMagnitude < 1e-10f) return;

            // Slide the carry against STATIC geometry so being carried sideways into a wall slides along it
            // instead of clamping. SlideAlongStatic skips ALL movers (incl. the base) so a DESCENDING platform's
            // surface under the feet can't clamp the downward follow and strand us behind it.
            Vector3 slid = SlideAlongStatic(delta, transform.position);
            transform.position += slid;
            carryDeltaThisFrame = slid;

            // YAW FRACTION — how much of the carry survived the slide (applied projected onto intended), so the
            // inherited yaw is scaled to match: pinned at a wall → the spin carries us round only as far as we moved.
            carryRotationFraction = Mathf.Clamp01(Vector3.Dot(slid, delta) / delta.sqrMagnitude);
        }


        /// <summary>
        /// This frame's follow displacement for a point anchored to a surface. Tracking both axes (the common case) reads the live transform via TransformPoint (drift-free, orbit included); otherwise the per-frame delta, which DynamicSurface already zeroes on any untracked axis.
        /// </summary>
        /// <param name="surface">Surface the anchor is bound to.</param>
        /// <param name="anchorLocal">Stand-point in the surface's local space.</param>
        private Vector3 SurfaceFollowDelta(SCC_DynamicSurface surface, Vector3 anchorLocal)
        {
            return surface.TrackPosition && surface.TrackRotation
                ? surface.transform.TransformPoint(anchorLocal) - transform.position
                : surface.GetPointDelta(transform.position);
        }


        #endregion

        #region Wall
        /// <summary>
        /// Informational wall probe (never moves the character): eight horizontal capsule sweeps around upDirection; a hit in the wall band flags isOnWall and feeds wallNormal. The dynamic side-push is handled separately in ResolvePenetrations.
        /// </summary>
        /// <returns>The nearest dynamic wall (the candidate for TryWallStick), or null.</returns>
        private SCC_DynamicSurface CheckWallContact()
        {
            isOnWall = false;
            wallNormal = Vector3.zero;
            SCC_DynamicSurface nearestDynamic = null;
            float nearestDynamicWall = float.MaxValue;
            // wallCollisionPoint is intentionally NOT reset here — we keep the last known contact point.
            float nearestWall = float.MaxValue;

            CapsuleFeetPoints(transform.position, out var point1, out var point2);
            float reach = skinWidth + wallCheckDistance;

            // Horizontal basis in the plane perpendicular to upDirection, seeded from our facing.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, currentUpDirection);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(Vector3.forward, currentUpDirection);
            forward.Normalize();

            Vector3 accumulated = Vector3.zero;
            int wallHits = 0;

            for (int d = 0; d < WallProbeCount; d++)
            {
                Vector3 dir = Quaternion.AngleAxis(360f / WallProbeCount * d, currentUpDirection) * forward;
                int hitCount = Physics.CapsuleCastNonAlloc(point1, point2, radius, dir, wallHitBuffer,
                    reach, ~toIgnore, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = wallHitBuffer[i];
                    if (IsSelf(hit.collider)) continue;
                    if (hit.distance <= 0f) continue; // started overlapping → unreliable normal, skip

                    float angle = Vector3.Angle(currentUpDirection, hit.normal);
                    // Half-degree tolerance so surfaces at exactly the band edge aren't inconsistently
                    // rejected by floating-point rounding on the boundary.
                    if (angle < maxWallAngle.x - 0.5f || angle > maxWallAngle.y + 0.5f) continue; // floor or ceiling

                    isOnWall = true;
                    accumulated += hit.normal;
                    wallHits++;

                    // Record the nearest contact point (no averaging) so an external probe can aim at the wall.
                    if (hit.distance < nearestWall)
                    {
                        nearestWall = hit.distance;
                        lastWallCollisionPoint = hit.point;
                    }

                    // Remember the closest dynamic wall so TryWallStick can acquire it for the ride.
                    if (hit.distance < nearestDynamicWall)
                    {
                        SCC_DynamicSurface ds = ResolveDynamicSurface(hit.collider);
                        if (ds != null)
                        {
                            nearestDynamicWall = hit.distance;
                            nearestDynamic = ds;
                        }
                    }
                }
            }

            if (wallHits > 0) wallNormal = accumulated.normalized;
            return nearestDynamic;
        }


        /// <summary>
        /// While stickToWall is held against a dynamic wall, ride its motion anchor-based (bind once in its local space, follow TransformPoint every frame) — robust to the short probe losing a fast-receding wall.
        /// </summary>
        /// <param name="wallCandidate">Nearest dynamic wall from CheckWallContact, or null.</param>
        /// <returns>True for a ridden dynamic wall or any valid wall contact (static included), so an external mechanic can react to the static case too.</returns>
        public bool TryWallStick(SCC_DynamicSurface wallCandidate)
        {
            // The safety latch clears the instant the input is released, so re-pressing can stick again.
            if (!shouldStickToWall) wallStickBlocked = false;

            // ACQUIRE / SWITCH — a fresh probe contact (re)binds us to that wall; after that we follow the anchor even
            // if the probe misses, releasing only when stickToWall drops. ridingWall != candidate avoids re-anchoring mid-ride.
            if (shouldStickToWall && !wallStickBlocked && wallCandidate != null && ridingWall != wallCandidate)
            {
                ridingWall = wallCandidate;
                wallAnchorLocal = ridingWall.transform.InverseTransformPoint(transform.position);
                ridingWall.ResetGeometricSample(); // fresh ride → start the angular-arc tracking clean
            }

            bool carrying = followSurfacePosition && shouldStickToWall && !wallStickBlocked && ridingWall != null;

            if (carrying)
            {
                // delta is PURE wall motion — our own along-wall input was baked into the anchor last frame,
                // so carry and input never double-count.
                Vector3 delta = SurfaceFollowDelta(ridingWall, wallAnchorLocal);
                Vector3 slid = delta.sqrMagnitude >= 1e-10f ? SlideAlongStatic(delta, transform.position) : Vector3.zero;
                transform.position += slid;

                // SAFETY (penetration) — the wall we ride isn't slid against or de-penetrated (ResolvePenetrations
                // skips it), so if it shoves us too deep into anything, break the stick and let the normal de-pen out.
                if (IsPenetratingDeeperThan(wallStickBreakPenetration))
                {
                    BreakWallStick();
                    return false;
                }

                // Velocity we're carried at, for the release hand-off (rotational tangential part included).
                wallMomentum = Time.deltaTime > 0f ? delta / Time.deltaTime : Vector3.zero;
            }
            else
            {
                // Released / never bound → inject the wall's last velocity once (the floor-leave hand-off). No carry
                // ran this frame, so nothing to deduct.
                if (followSurfacePosition && isWallCarrying && transferWallMomentum && wallMomentum.sqrMagnitude > 1e-6f)
                    CommitMomentum(SCC_Math.ScaleVerticalInertia(
                        wallMomentum, currentUpDirection, wallInertiaUpScale, wallInertiaDownScale));

                // Rotational momentum — seed the decaying yaw from the wall's last frame rotation.
                // ridingWall is still valid here; it's cleared below.
                if (followSurfaceRotation && isWallCarrying && transferWallRotation && ridingWall != null)
                {
                    float wallYaw = SurfaceYawAroundUp(ridingWall);
                    if (Mathf.Abs(wallYaw) > 0.001f)
                        inheritedWallYawVelocity = wallYaw;
                }

                wallMomentum = Vector3.zero;
                ridingWall = null;
            }

            isWallCarrying = carrying;
            isSticking = carrying || (shouldStickToWall && !wallStickBlocked && isOnWall);
            return isSticking;
        }


        /// <summary>
        /// Drop the wall ride and latch it off — an abort with no momentum hand-off, used by both safety valves. The latch clears in TryWallStick when stickToWall goes false, so re-pressing re-arms it.
        /// </summary>
        private void BreakWallStick()
        {
            wallStickBlocked = true;
            ridingWall = null;
            wallMomentum = Vector3.zero;
            isWallCarrying = false;
            isSticking = false;
        }


        /// <summary>
        /// Re-anchor our pose on the ridden wall after CollideAndSlide (input slide included), so next frame's TryWallStick follows pure wall motion. The wall twin of UpdateSurfaceAnchor.
        /// </summary>
        private void UpdateWallAnchor()
        {
            if (isWallCarrying && ridingWall != null)
                wallAnchorLocal = ridingWall.transform.InverseTransformPoint(transform.position);
        }


        /// <summary>
        /// True if the capsule penetrates any solid collider deeper than maxDepth — the stick-to-wall safety valve.
        /// </summary>
        /// <param name="maxDepth">Penetration depth tolerated before reporting true.</param>
        private bool IsPenetratingDeeperThan(float maxDepth)
        {
            CapsuleFeetPoints(transform.position, out var point1, out var point2);

            int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, penetrationOverlaps,
                ~toIgnore, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = penetrationOverlaps[i];
                if (IsSelf(other)) continue;

                if (TryDepenetrate(other, transform.position, transform.rotation, out _, out float dist) && dist > maxDepth)
                    return true;
            }

            return false;
        }


        #endregion

        #region Ceiling
        /// <summary>
        /// Informational ceiling probe (never moves the character): one upward sphere sweep; a hit past the wall band flags isUnderCeiling and stores ceilingNormal. Skips initial overlaps for a clean normal — an actively overlapping descending platform is handled in ResolvePenetrations instead.
        /// </summary>
        private void CheckCeilingContact()
        {
            isUnderCeiling = false;
            ceilingNormal = Vector3.zero;

            Vector3 topSphere = transform.position + currentUpDirection * (height - radius);
            float reach = skinWidth + ceilingCheckDistance;

            int hitCount = Physics.SphereCastNonAlloc(topSphere, radius, currentUpDirection, ceilingHitBuffer,
                reach, ~toIgnore, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = ceilingHitBuffer[i];
                if (IsSelf(hit.collider)) continue;
                if (hit.distance <= 0f) continue; // started overlapping → unreliable normal, skip

                float angle = Vector3.Angle(currentUpDirection, hit.normal);
                if (angle <= maxWallAngle.y) continue; // floor or wall, not overhead

                isUnderCeiling = true;
                ceilingNormal = hit.normal;
                break;
            }
        }


        /// <summary>
        /// Drop only the part of AskedLocalVelocity driving into the surface, keeping the tangential slide. No-op when already moving along/away. Shared by every "don't re-drive into this surface" site (static ceiling, dynamic-platform ceiling, side-wall clip).
        /// </summary>
        /// <param name="contactNormal">World outward normal of the surface.</param>
        private void DeflectVelocityAlongCeiling(Vector3 contactNormal)
        {
            Vector3 worldVelocity = BodyToWorld(AskedLocalVelocity);
            float into = Vector3.Dot(worldVelocity, contactNormal);
            if (into < 0f) AskedLocalVelocity = WorldToBody(worldVelocity - contactNormal * into);
        }


        #endregion

        #region Geometry & Depenetration
        // ---- CAPSULE GEOMETRY HELPERS ----

        /// <summary>
        /// Our own trigger collider or anything on our transform — the self-skip every overlap/sweep loop needs.
        /// </summary>
        /// <param name="c">Collider to test.</param>
        private bool IsSelf(Collider c) => c == ownCollider || c.transform == transform;

        /// <summary>
        /// MTV to push our capsule out of another collider. The single ComputePenetration call site.
        /// </summary>
        /// <param name="other">Collider to escape.</param>
        /// <param name="selfPos">Our capsule's world position for the test.</param>
        /// <param name="selfRot">Our capsule's world rotation for the test.</param>
        /// <param name="dir">Escape direction (MTV).</param>
        /// <param name="dist">Escape distance (MTV).</param>
        private bool TryDepenetrate(Collider other, Vector3 selfPos, Quaternion selfRot, out Vector3 dir, out float dist)
            => Physics.ComputePenetration(ownCollider, selfPos, selfRot,
                other, other.transform.position, other.transform.rotation, out dir, out dist);

        /// <summary>
        /// Sweep endpoints for an upright capsule at a feet position, along upDirection. Relies on the body's up axis being upDirection — the form used by every upDirection-aligned cast.
        /// </summary>
        /// <param name="feet">World position of the feet.</param>
        /// <param name="p1">Bottom sweep endpoint.</param>
        /// <param name="p2">Top sweep endpoint.</param>
        private void CapsuleFeetPoints(Vector3 feet, out Vector3 p1, out Vector3 p2)
        {
            p1 = feet + currentUpDirection * radius;
            p2 = feet + currentUpDirection * (height - radius);
        }

        /// <summary>
        /// Endpoints for a capsule at an arbitrary pose, around the rotated centre along the pose's own up. Correct when tilted and for a not-yet-committed candidate rotation; agrees with CapsuleFeetPoints once the pose is committed.
        /// </summary>
        /// <param name="pos">World position of the capsule centre.</param>
        /// <param name="rot">World rotation of the capsule.</param>
        /// <param name="p1">Bottom sweep endpoint.</param>
        /// <param name="p2">Top sweep endpoint.</param>
        private void CapsuleBodyPoints(Vector3 pos, Quaternion rot, out Vector3 p1, out Vector3 p2)
        {
            Vector3 up = rot * Vector3.up;
            Vector3 cap = pos + rot * center;
            p1 = cap - up * (height * 0.5f - radius);
            p2 = cap + up * (height * 0.5f - radius);
        }


        /// <summary>
        /// MTV de-penetration (needs primitive/convex colliders) — handles "something moved into the capsule", which sweeps can't see. Each overlap is routed by escape direction: sideways (wall band) keeps the lateral push so we're carried along, up seats with the up component only, overhead registers the ceiling and matches its descent. Dynamic surfaces only — static geometry is CollideAndSlide's, double-resolving it would jitter.
        /// </summary>
        private void ResolvePenetrations()
        {
            CapsuleFeetPoints(transform.position, out var point1, out var point2);

            int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, penetrationOverlaps,
                ~toIgnore, QueryTriggerInteraction.Ignore);

            float upAmount = 0f;                  // upward seat, summed from floor-ish contacts
            Vector3 lateralPush = Vector3.zero;   // sideways shove, summed from wall-ish contacts
            Vector3 wallContactNormal = Vector3.zero;
            int lateralContacts = 0;
            Vector3 ceilingContactNormal = Vector3.zero;
            int ceilingContacts = 0;
            float ceilingDescentVel = 0f; // most-negative vertical velocity among overhead platforms (m/s along up)

            // Strongest lateral pusher this frame (deepest penetration), for the push-momentum hand-off.
            currentPushSurface = null;
            float maxPushDist = 0f;

            for (int i = 0; i < count; i++)
            {
                Collider other = penetrationOverlaps[i];
                if (IsSelf(other)) continue;
                SCC_DynamicSurface ds = ResolveDynamicSurface(other);
                Rigidbody otherRb = ds == null ? other.attachedRigidbody : null;

                // Pure static geometry is owned by CollideAndSlide. Non-kinematic rigidbodies get a
                // positional safety de-penetration here (no force — CollideAndSlide owns the push).
                if (ds == null && (otherRb == null || otherRb.isKinematic)) continue;
                if (isWallCarrying && ds == ridingWall) continue; // the wall carry owns following this wall

                if (!TryDepenetrate(other, transform.position, transform.rotation, out Vector3 pushDir, out float pushDist))
                    continue;

                Vector3 mtv = pushDir * (pushDist + 0.001f);
                float escapeAngle = Vector3.Angle(currentUpDirection, pushDir);

                // Rigidbody safety: de-penetrate the character without touching the RB (force is CollideAndSlide's job).
                // SCCPushable bodies skip the lateral push entirely — pushing here would fight CollideAndSlide and jitter.
                if (ds == null)
                {
                    bool isPushable = pushRigidbodies && other.TryGetComponent<SCC_Pushable>(out _);
                    if (!isPushable && escapeAngle >= maxWallAngle.x && escapeAngle <= maxWallAngle.y)
                    {
                        float upComponent = Vector3.Dot(mtv, currentUpDirection);
                        lateralPush += mtv - currentUpDirection * upComponent;
                    }
                    else if (escapeAngle < maxWallAngle.x)
                    {
                        upAmount += Mathf.Max(0f, Vector3.Dot(mtv, currentUpDirection));
                    }
                    continue;
                }

                if (escapeAngle >= maxWallAngle.x && escapeAngle <= maxWallAngle.y)
                {
                    // Sideways escape → moving wall. Keep only the horizontal part so we slide along it, never get
                    // launched. pushDir is the wall's outward normal (toward us), as CheckWallContact's hit.normal.
                    float upComponent = Vector3.Dot(mtv, currentUpDirection);
                    lateralPush += mtv - currentUpDirection * upComponent;
                    wallContactNormal += pushDir;
                    lateralContacts++;
                    if (pushDist > maxPushDist) { maxPushDist = pushDist; currentPushSurface = ds; } // deepest = the pusher
                }
                else if (escapeAngle > maxWallAngle.y)
                {
                    // Escape DOWN → a platform overlapping us from ABOVE. No push (it would crush us into the floor);
                    // just register the ceiling and match its descent below.
                    ceilingContactNormal += pushDir;
                    ceilingContacts++;
                    float vUp = Vector3.Dot(ds.Velocity, currentUpDirection); // track the fastest descent to match it
                    if (vUp < ceilingDescentVel) ceilingDescentVel = vUp;
                }
                else
                {
                    // Up / leading-edge diagonal → seat on top with the up component only.
                    upAmount += Mathf.Max(0f, Vector3.Dot(mtv, currentUpDirection));
                }
            }

            // Flag the wall from the reliable ComputePenetration normal (CheckWallContact's sweep skipped this
            // overlap), without clobbering a cleaner normal it already found.
            if (lateralContacts > 0)
            {
                Vector3 avgWallNormal = wallContactNormal.normalized;
                isOnWall = true;
                if (wallNormal == Vector3.zero) wallNormal = avgWallNormal;

                // ANTI-TUNNEL — the push is positional only; bleed the velocity driving INTO the mover or CollideAndSlide
                // re-applies the full charge and out-runs the per-frame de-pen until it tunnels. Into-component only, keeps the slide.
                DeflectVelocityAlongCeiling(avgWallNormal);

                // Record the pusher's point-velocity so HandlePushMomentum can inject it once the push ENDS.
                if (currentPushSurface != null)
                    currentPushMomentum = currentPushSurface.GetPointVelocity(transform.position);
            }

            if (ceilingContacts > 0)
            {
                Vector3 avgCeilingNormal = ceilingContactNormal.normalized;
                isUnderCeiling = true;
                if (ceilingNormal == Vector3.zero) ceilingNormal = avgCeilingNormal;
                if (neutralizeJumpOnCeiling)
                    DeflectVelocityAlongCeiling(avgCeilingNormal); // slide along an inclined underside, lose the upward Y on a flat one

                // Match a descending platform's speed so it can't overtake and clip through us. Harmless when grounded
                // (ApplyGravity clamps a downward Y to 0) and airborne (CollideAndSlide still stops us at static floor).
                if (ceilingDescentVel < 0f && AskedLocalVelocity.y > ceilingDescentVel)
                    AskedLocalVelocity.y = ceilingDescentVel;
            }

            // SEAT UP — swept so it clamps under a ceiling instead of tunnelling.
            if (upAmount > 0.0001f)
            {
                float dist = upAmount;
                if (Physics.CapsuleCast(point1, point2, radius, currentUpDirection, out var hit, dist, ~toIgnore,
                        QueryTriggerInteraction.Ignore))
                    dist = Mathf.Max(0f, hit.distance - skinWidth);
                transform.position += currentUpDirection * dist;
            }

            // SIDE PUSH — slid against STATIC geometry so a mover can't shove us through the decor (deflect along the
            // wall; when pinned, overlap the mover rather than tunnel static). Positional only — input slides freely.
            if (lateralPush.sqrMagnitude > 1e-8f)
                transform.position += SlideAlongStatic(lateralPush, transform.position);
        }


        /// <summary>
        /// Dedicated detection for SCCPushable rigidbodies pressing INTO the character. Uses an inflated-radius
        /// overlap (radius + pushDetectionMargin) every frame — independent of the character's own movement,
        /// unlike CollideAndSlide's sweep — so a STATIONARY character still reliably slows incoming bodies.
        /// Bleeds each body's approach velocity by its IncomingResistance. The character's own push (already
        /// scheduled by CollideAndSlide via SchedulePush) is read back through ScheduledVelocity, so an
        /// actively-pushing character isn't fought and we never double-count the same impulse.
        /// </summary>
        private void DecelerateIncomingPushables()
        {
            if (!pushRigidbodies) return;

            CapsuleBodyPoints(transform.position, transform.rotation, out var p1, out var p2);
            int count = Physics.OverlapCapsuleNonAlloc(p1, p2, radius + pushDetectionMargin, penetrationOverlaps,
                ~toIgnore, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = penetrationOverlaps[i];
                if (IsSelf(other)) continue;

                Rigidbody rb = other.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (!other.TryGetComponent<SCC_Pushable>(out var pushable)) continue;
                if (pushable.PushingResistance <= 0f) continue;

                // Horizontal character → body direction, stable across all collider shapes.
                Vector3 toRb = Vector3.ProjectOnPlane(rb.worldCenterOfMass - transform.position, currentUpDirection);
                if (toRb.sqrMagnitude < 0.0001f) continue;
                toRb.Normalize();

                // Approach speed = velocity heading toward the character (opposite toRb). Reading the already-
                // scheduled push means the character's own shove is subtracted: it can still resist/push freely.
                float approachSpeed = Mathf.Max(0f, -Vector3.Dot(rb.linearVelocity + pushable.ScheduledVelocity, toRb));

                // Framerate-independent exponential decay: IncomingResistance is the fraction bled per frame at
                // the reference rate, so the steady-state slowdown feels identical regardless of actual fps.
                float frac = 1f - Mathf.Pow(1f - pushable.PushingResistance, Time.deltaTime * PushDecayReferenceFps);
                float speedToRemove = approachSpeed * frac;
                if (speedToRemove > 0.001f)
                    pushable.SchedulePush(toRb * speedToRemove, other.ClosestPoint(transform.position)); // at contact point → can spin
            }
        }


        /// <summary>
        /// Collide-and-slide a world displacement against STATIC geometry only, deflecting ALONG surfaces instead of
        /// clamping. Movers are skipped (a platform must never block its own carry; when pinned we overlap a mover
        /// rather than tunnel static — ResolvePenetrations then resolves it).
        /// </summary>
        /// <param name="displacement">World displacement to attempt.</param>
        /// <param name="fromPosition">Position to slide from.</param>
        /// <returns>The displacement actually applied.</returns>
        private Vector3 SlideAlongStatic(Vector3 displacement, Vector3 fromPosition)
        {
            const int maxBounces = 4;
            Vector3 applied = Vector3.zero;
            Vector3 remaining = displacement;
            Vector3 pos = fromPosition;

            for (int i = 0; i < maxBounces; i++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-6f) break;
                Vector3 dir = remaining / dist;

                // Endpoints around the ROTATED centre so the cast capsule stays correct when the body is tilted.
                CapsuleBodyPoints(pos, transform.rotation, out var point1, out var point2);

                // Nearest STATIC hit along the path (skip self, initial overlaps, and every mover).
                int hitCount = Physics.CapsuleCastNonAlloc(point1, point2, radius, dir, slideHitBuffer,
                    dist + skinWidth, ~toIgnore, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                Vector3 hitNormal = Vector3.zero;
                for (int k = 0; k < hitCount; k++)
                {
                    RaycastHit h = slideHitBuffer[k];
                    if (h.collider == ownCollider) continue;
                    if (h.distance <= 0f) continue;                          // initial overlap (e.g. the pusher)
                    if (ResolveDynamicSurface(h.collider) != null) continue; // static only
                    if (h.distance < nearest) { nearest = h.distance; hitNormal = h.normal; }
                }

                if (nearest == float.MaxValue)
                {
                    applied += remaining; // clear path — apply the rest and finish
                    break;
                }

                // Advance to just before the wall.
                float travel = Mathf.Max(0f, nearest - skinWidth);
                applied += dir * travel;
                pos     += dir * travel;

                if (slidePlatformMotionAlongCollisions)
                {
                    // CONSERVE (ON) — redirect the FULL remaining budget along the wall. Budget is (dist - travel),
                    // NOT (dist - nearest): shedding the skinWidth gap would drain skinWidth of travel every contact
                    // frame. A head-on hit projects to ~zero → stops, like the clamp.
                    float remainingDist = dist - travel;
                    Vector3 alongWall = Vector3.ProjectOnPlane(dir, hitNormal);
                    remaining = alongWall.sqrMagnitude > 1e-8f
                        ? alongWall.normalized * remainingDist
                        : Vector3.zero;
                }
                else
                {
                    remaining = Vector3.zero; // CLAMP (OFF, default) — stop at the wall, drop the rest this frame
                }
            }

            return applied;
        }


        #endregion

        #region Surface Momentum
        /// <summary>
        /// CREST UNGROUND — walking over a ridge, the ground normal rotates from uphill to downhill. When that
        /// signed rotation (measured around the travel-relative right axis) exceeds crestAngle, unground so we
        /// launch over the crest tangentially instead of gluing to the descending face. Overwrites
        /// lastValidGroundNormal with the UPHILL normal so CollideAndSlide's slope inertia gives a positive Y.
        /// </summary>
        private void CheckCrestUnground()
        {
            Vector3 prev = previousGroundNormal;
            previousGroundNormal = groundNormal;

            if (!ungroundOnCrest || !wasGrounded || !isGrounded) return;

            // Measure the normal's rotation around travel-relative right, not the body's fixed right: Cross(up, moveDir)
            // flips with the walk direction, so the sign stays consistent (a fixed axis only caught forward crests).
            Vector3 moveDir = Vector3.ProjectOnPlane(BodyToWorld(AskedLocalVelocity), currentUpDirection);
            if (moveDir.sqrMagnitude < 1e-6f) return; // no horizontal motion → no crest traversal
            Vector3 travelRight = Vector3.Cross(currentUpDirection, moveDir.normalized);

            // Signed angle distinguishes crest (uphill→downhill, positive) from valley (negative), through the up side.
            float signedAngle = Vector3.SignedAngle(prev, groundNormal, travelRight);
            if (signedAngle < crestAngle) return;

            isGrounded = false;
            crestLaunched = true;
            // GroundCheck committed the DOWNHILL normal, which preserveSlopeInertia would read as a negative Y (gluing
            // us to the descending face). Overwrite with the UPHILL normal so the inertia launches us over the crest.
            lastValidGroundNormal = prev;
        }

        /// <summary>
        /// PLATFORM-LAUNCH — when the floor platform's vertical velocity suddenly DROPS by more than
        /// platformLaunchThreshold in one frame (stops/reverses/yanked down), force an unground BEFORE
        /// HandleSurfaceMomentum so it commits last frame's (pre-drop) velocity and flings us. Clears currentSurface
        /// so we actually leave. Needs the same surface two frames running. Gated by handlePlatformLaunch.
        /// </summary>
        private void CheckPlatformLaunch()
        {
            launchedThisFrame = false;
            float currentVUp = currentSurface != null ? Vector3.Dot(currentSurface.Velocity, currentUpDirection) : 0f;

            if (followSurfacePosition && handlePlatformLaunch && isGrounded && wasGrounded
                && currentSurface != null && currentSurface == launchTrackSurface
                && (previousSurfaceVUp - currentVUp) > platformLaunchThreshold)
            {
                isGrounded = false;
                currentSurface = null;     // stop the carry / anchor from re-gluing us this frame
                launchedThisFrame = true;  // HandleSurfaceMomentum will use the dedicated launch force scale
            }

            previousSurfaceVUp = currentVUp;
            launchTrackSurface = currentSurface;
        }


        /// <summary>
        /// Platform momentum hand-off. GROUNDED: sample the base's point-velocity at our feet (rotational tangential
        /// included) and publish platformYawDelta. LEAVING (jump/walk-off): inject that velocity into AskedLocalVelocity
        /// ONCE — the FULL vector, up included (rising platform launches, descending one drops). From then on it's
        /// ordinary velocity. Safe because contact is stable (sync + carry + seat): a non-jump unground is a real leave.
        /// </summary>
        private void HandleSurfaceMomentum()
        {
            momentumCommittedY = 0f; // one-frame lifetime

            // Count down the post-correction grace (committedMomentumThisFrame is reset in Update, before the
            // wall/push hand-offs that also feed it — not here, or those earlier commits would be wiped).
            if (leaveSuppressTimer > 0f) leaveSuppressTimer -= Time.deltaTime;

            if (isGrounded)
            {
                // Landing: real platform rotation takes over immediately — clear both inherited yaws.
                if (!wasGrounded && stopRotationInertiaOnGround)
                    inheritedFloorYawVelocity = inheritedWallYawVelocity = 0f;

                // STABILITY — accumulate continuous grounded time on THIS surface; a one-frame edge clip would
                // otherwise arm surfaceMomentum and fling us.
                if (currentSurface != null && currentSurface == momentumStableSurface)
                    groundedSurfaceTime += Time.deltaTime;
                else { momentumStableSurface = currentSurface; groundedSurfaceTime = 0f; }

                surfaceMomentum = followSurfacePosition && currentSurface != null
                    ? currentSurface.GetPointVelocity(transform.position)
                    : Vector3.zero;
                // Scale the inherited yaw by the orbit fraction the carry achieved (pinned at a wall → follow the
                // spin only as far as we actually moved, not pivot in place).
                platformYawDelta = followSurfaceRotation && currentSurface != null ? SurfaceYawAroundUp(currentSurface) * carryRotationFraction : 0f;
                lastPlatformYawDelta = platformYawDelta; // captured for the leave-frame seed below
                return;
            }

            // Airborne: a ridden rotating wall still turns the heading like a floor platform — publish its yaw. The
            // surface owns the geometric-vs-raw choice (isSelfSpinningCylinder); we just feed it the rider position and
            // the gameplay intensity, then consume the multiplier. platformYawDelta stays 0 for walls (floor-only channel).
            if (isWallCarrying && ridingWall != null)
            {
                if (followSurfaceRotation)
                {
                    ridingWallHeadingDelta = ridingWall.WallHeadingDelta(transform.position, wallRunningYawMultiplier);
                    wallRunningYawMultiplier = 1f; // consumed — reset so a stale gameplay value can't linger
                }
                else
                {
                    ridingWallHeadingDelta = Quaternion.identity;
                }
            
                platformYawDelta = 0f;
            }
            else
            {
                platformYawDelta = 0f;
                ridingWallHeadingDelta = Quaternion.identity;
            }

            bool leaveGateOpen = launchedThisFrame || (leaveSuppressTimer <= 0f && groundedSurfaceTime >= MomentumStabilityTime);

            // LINEAR COMMIT GATE
            if (followSurfacePosition && wasGrounded && transferPlatformMomentum && surfaceMomentum.sqrMagnitude > 1e-6f && leaveGateOpen)
            {
                // LAUNCH uses its dedicated force scale; a normal leave uses the inertia Up/Down scaling.
                Vector3 momentum = launchedThisFrame ? 
                    surfaceMomentum * platformLaunchForceScale : 
                    SCC_Math.ScaleVerticalInertia(surfaceMomentum, currentUpDirection, platformInertiaUpScale, platformInertiaDownScale);
            
                // cancellable for a normal leave (a same-frame step/snap re-ground refunds it); launch is never undone.
                Vector3 localM = CommitMomentum(momentum, cancellable: !launchedThisFrame);
                // Stash the carry delta so CollideAndSlide deducts it once (else the leave frame travels at ~2× speed),
                // and the local up so its slope-inertia gate can subtract it (else the committed Y breaks its equality).
                momentumCarryComp = carryDeltaThisFrame;
                momentumCommittedY = localM.y;
                surfaceMomentum = Vector3.zero;
            }

            // ROTATIONAL COMMIT GATE (floor) — independent of linear so a pure-rotation platform (no translation)
            // still works. Uses the same stability gate so a one-frame edge clip imparts nothing.
            if (followSurfaceRotation && wasGrounded && transferPlatformRotation
                && Mathf.Abs(lastPlatformYawDelta) > 0.001f && leaveGateOpen)
            {
                inheritedFloorYawVelocity = lastPlatformYawDelta;
            }

            if (isOnWall && stopRotationInertiaOnWall)
            {
                inheritedFloorYawVelocity = inheritedWallYawVelocity = 0f;
            }
        
            // Decay and publish floor rotational momentum.
            if (followSurfaceRotation && Mathf.Abs(inheritedFloorYawVelocity) > 0.001f)
            {
                platformYawDelta += inheritedFloorYawVelocity;
                inheritedFloorYawVelocity = platformRotationDecay > 0f
                    ? Mathf.Lerp(inheritedFloorYawVelocity, 0f, platformRotationDecay * Time.deltaTime)
                    : 0f;
                if (Mathf.Abs(inheritedFloorYawVelocity) < 0.001f) inheritedFloorYawVelocity = 0f;
            }

            // Decay and publish wall rotational momentum (seeded by TryWallStick on release).
            if (followSurfaceRotation && Mathf.Abs(inheritedWallYawVelocity) > 0.001f)
            {
                platformYawDelta += inheritedWallYawVelocity;
                inheritedWallYawVelocity = wallRotationDecay > 0f ? Mathf.Lerp(inheritedWallYawVelocity, 0f, wallRotationDecay * Time.deltaTime) : 0f;
                if (Mathf.Abs(inheritedWallYawVelocity) < 0.001f) inheritedWallYawVelocity = 0f;
            }
        }

        /// <summary>
        /// Add an inherited momentum (WORLD) to AskedLocalVelocity (body-local).
        /// </summary>
        /// <param name="momentum">World-space momentum to inherit.</param>
        /// <param name="cancellable">If true, records the local delta into the per-frame ledger so a same-frame
        /// step-up/snap re-ground can refund it. Pass false for a launch (an intended fling is never undone).</param>
        /// <returns>The body-local delta added.</returns>
        private Vector3 CommitMomentum(Vector3 momentum, bool cancellable = true)
        {
            Vector3 local = WorldToBody(momentum);
            AskedLocalVelocity += local;
            if (cancellable) committedMomentumThisFrame += local;
            return local;
        }

        /// <summary>
        /// PUSH hand-off (the pushed-into twin of the leave hand-off). On the frame a sideways platform push ENDS,
        /// inject the last pusher velocity — HORIZONTAL ONLY (we were never attached, so its vertical isn't ours).
        /// Gated by a stability time and leaveSuppressTimer so it can't stack with the floor hand-off on stairs.
        /// </summary>
        private void HandlePushMomentum()
        {
            bool pushedThisFrame = currentPushSurface != null;

            // STABILITY — accumulate how long the SAME platform has pushed, so a one-frame edge clip imparts nothing.
            if (pushedThisFrame)
            {
                if (currentPushSurface == pushStableSurface)
                {
                    pushedTime += Time.deltaTime;
                }
                else
                {
                    pushStableSurface = currentPushSurface; 
                    pushedTime = 0f;
                }
            }

            if (wasPushed && !pushedThisFrame && transferPushMomentum && lastPushMomentum.sqrMagnitude > 1e-6f && pushedTime >= MomentumStabilityTime && leaveSuppressTimer <= 0f)
            {
                Vector3 horizontal = Vector3.ProjectOnPlane(lastPushMomentum, currentUpDirection);
                if (horizontal.sqrMagnitude > 1e-6f) CommitMomentum(horizontal * pushMomentumScale);
            }

            if (pushedThisFrame) lastPushMomentum = currentPushMomentum;
            else pushedTime = 0f;
            wasPushed = pushedThisFrame;
        }

        /// <summary>
        /// Refund a leave-momentum committed THIS frame when a step-up/snap has since re-grounded us.
        /// </summary>
        private void CancelLeaveMomentumIfCommitted()
        {
            if (committedMomentumThisFrame == Vector3.zero) return;
        
            AskedLocalVelocity -= committedMomentumThisFrame;
            committedMomentumThisFrame = Vector3.zero;
        }

        // Platform yaw — swing-twist of the surface's RotationDelta about the current upDirection (lives in SCC_Math).
        private float SurfaceYawAroundUp(SCC_DynamicSurface surface) => SCC_Math.YawAroundAxis(surface.RotationDelta, currentUpDirection);


        /// <summary>
        /// Store our stand-point on the surface (local space) at the END of Update so next frame's carry moves toward
        /// it on the platform's current transform. Seats first every riding frame so the anchor stays
        /// "feet skinWidth above the surface" (else a gap from a landing/descent desync would ratchet upward).
        /// </summary>
        private void UpdateSurfaceAnchor()
        {
            if (currentSurface != null)
            {
                // Seat before anchoring (skipped while ascending — never yank a launching character down).
                if (AskedLocalVelocity.y <= 0.01f) SeatOnSurface();

                surfaceAnchorLocal = currentSurface.transform.InverseTransformPoint(transform.position);
                hasSurfaceAnchor = true;
            }
            else
            {
                hasSurfaceAnchor = false;
            }
        }


        /// <summary>
        /// Downward seat while riding: close the feet→surface gap so the capsule rests skinWidth above it (the snap's
        /// resting state, for dynamic ground). Swept down to first contact; no-op when already seated.
        /// </summary>
        private void SeatOnSurface()
        {
            CapsuleFeetPoints(transform.position, out var point1, out var point2);

            // Reach matches GroundCheck's descent-extended acceptance (same CurrentSurfaceDrop): a gap GroundCheck
            // grounded on must always be within the seat's range, or a fast-descending platform would be accepted
            // as ground but never seated.
            float maxSeat = groundCheckDistance + skinWidth * 2f + CurrentSurfaceDrop();

            // CENTER-LINE WALKABLE GATE — see IsCenterOverWalkableGround.
            if (!IsCenterOverWalkableGround(transform.position, maxSeat)) return;
            if (!Physics.CapsuleCast(point1, point2, radius, -currentUpDirection, out var hit, maxSeat, ~toIgnore, QueryTriggerInteraction.Ignore)) return;

            float seatAmount = hit.distance - skinWidth;
            if (seatAmount <= 0f) return;

            transform.position -= currentUpDirection * seatAmount;
        }

        /// <summary>
        /// Zero-radius downward ray from the feet center: true if the surface directly below is within range and
        /// walkable. Gates SeatOnSurface / CheckForGroundSnap so the CapsuleCast can't grab a platform's trailing
        /// corner once the center is past the edge.
        /// </summary>
        /// <param name="origin">Capsule center to ray down from.</param>
        /// <param name="range">Max distance to the surface below.</param>
        private bool IsCenterOverWalkableGround(Vector3 origin, float range)
        {
            Vector3 rayOrigin = origin + currentUpDirection * skinWidth;
            if (!Physics.Raycast(rayOrigin, -currentUpDirection, out var hit,
                    range + skinWidth, ~toIgnore, QueryTriggerInteraction.Ignore))
                return false;
            return Vector3.Angle(currentUpDirection, hit.normal) <= maxSlopeAngle;
        }


        #endregion

        #region Gravity & Orientation

        /// <summary>
        /// The reference up axis, exposed so the camera/look system can layer its transient yaw around the SAME axis.
        /// </summary>
        public Vector3 CurrentUpDirection => currentUpDirection;

        /// <summary>
        /// AskedLocalVelocity is in transform.localSpace: x = character right, z = character forward, y = up.
        /// BodyToWorld/WorldToBody are simple transform rotation conversions — external scripts can write
        /// AskedLocalVelocity directly without any manual conversion.
        /// Trade-off: inherited momentum rotates with the character's heading (camera turns in air affect direction).
        /// </summary>
        /// <param name="local">Body-local vector to convert.</param>
        private Vector3 BodyToWorld(Vector3 local)  => transform.rotation * local;

        /// <param name="world">World-space vector to convert.</param>
        private Vector3 WorldToBody(Vector3 world)  => Quaternion.Inverse(transform.rotation) * world;

        /// <summary>
        /// Sets upDirection. transform.rotation is already updated by UpdateUpAlignment before this is called.
        /// </summary>
        /// <param name="newUp">The up axis to apply.</param>
        private void ApplyUpDirection(Vector3 newUp) => currentUpDirection = newUp;

        /// <summary>
        /// Convert a WORLD velocity into the body-local frame. Public so the movement manager can feed world-space
        /// input into AskedLocalVelocity while the field itself stays body-local.
        /// </summary>
        /// <param name="world">World-space velocity to convert.</param>
        public Vector3 WorldToLocalVelocity(Vector3 world) => WorldToBody(world);

        /// <summary>
        /// Convert a body-local velocity into WORLD space. Public twin of WorldToLocalVelocity so states doing
        /// world-space math (wall normals, ballistic impulses) can round-trip a value through AskedLocalVelocity.
        /// </summary>
        /// <param name="local">Body-local velocity to convert.</param>
        public Vector3 LocalToWorldVelocity(Vector3 local) => BodyToWorld(local);

        /// <summary>
        /// Set the target up axis (opposite of gravity), in WORLD space. The body reorients toward it
        /// smoothly (UpdateUpAlignment). Pass a surface normal to "stand on" that surface. Ignored if zero.
        /// </summary>
        /// <param name="worldUp">The new target up axis, in world space.</param>
        public void SetUpDirection(Vector3 worldUp)
        {
            if (worldUp.sqrMagnitude < 1e-6f) return;
            upDirectionTarget = worldUp.normalized;
            orbitBlocked = false; // a fresh reorientation is allowed to orbit again
        }

        /// <summary>
        /// Lerp the actual up axis toward the target (timed + eased) and rebuild the body rotation from newUp + the
        /// preserved heading, so upDirection == transform.up EXACTLY — no roll, which keeps the swept casts and the
        /// collider agreeing when tilted. Orbits the position around upRotationPivot (else the capsule centre), falling
        /// back to a feet pivot if the orbit would embed the capsule, and holds the whole reorientation if the new
        /// pose can't be cleared of geometry.
        /// </summary>
        private void UpdateUpAlignment()
        {
            if (upDirectionTarget.sqrMagnitude < 1e-6f) upDirectionTarget = Vector3.up;
            Vector3 target = upDirectionTarget.normalized;

            // SETTLED — already on target: do nothing but keep the frame in sync. Rebuilding every frame would
            // re-project the forward and wobble it by float noise (jitter).
            if (Vector3.Dot(currentUpDirection, target) > 0.999999f) { ApplyUpDirection(target); orbitBlocked = false; return; }

            // Timed + eased (DOTween curve). Re-capture the start axis and restart the timer whenever the target
            // changes; advance it each frame and slerp start→target by the eased progress. 0 = instant.
            Vector3 newUp;
            if (target != reorientTarget)
            {
                reorientTarget = target;
                reorientStartUp = currentUpDirection;
                reorientTimer = 0f;
            }
            reorientTimer += Time.deltaTime;
            if (upAlignDuration <= 0f)
                newUp = target;
            else
            {
                float progress = Mathf.Clamp01(reorientTimer / upAlignDuration);
                float eased = DOVirtual.EasedValue(0f, 1f, progress, upAlignEase);
                newUp = Vector3.Slerp(reorientStartUp, target, eased).normalized;
                if (progress >= 1f || Vector3.Angle(newUp, target) < 0.1f) newUp = target;
            }
            if (!hasHeading) { headingRotation = transform.rotation; hasHeading = true; }

            // REBUILD from newUp + the preserved heading (forward projected onto the new up-plane). LookRotation with
            // a forward already ⊥ newUp yields a rotation whose up is EXACTLY newUp — no roll, no transform.up vs
            // upDirection drift (the prerequisite for collisions to hold when tilted).
            Vector3 forward = Vector3.ProjectOnPlane(headingRotation * Vector3.forward, newUp);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(headingRotation * Vector3.right, newUp);
            Quaternion newRot = Quaternion.LookRotation(forward.normalized, newUp);

            // Orbit the position about the pivot. If that would embed the capsule, fall back to a feet pivot, LATCHED
            // for the rest of this reorientation so we don't toggle poses frame to frame (jitter).
            Quaternion deltaQ = newRot * Quaternion.Inverse(headingRotation);
            Vector3 pivot = upRotationPivot != null ? upRotationPivot.position : transform.TransformPoint(center);
            Vector3 orbitedPos = pivot + deltaQ * (transform.position - pivot);

            Vector3 candidatePos = transform.position; // feet pivot
            if (!orbitBlocked)
            {
                if (CapsuleOverlaps(orbitedPos, newRot)) orbitBlocked = true;
                else candidatePos = orbitedPos;
            }

            // Verify the new pose is clear (the rotating kinematic capsule could sweep through a wall). If it stays
            // embedded in a non-convex mesh we can't expel from, HOLD the reorientation this frame (up/heading
            // untouched) — it resumes once the player steps clear.
            Vector3 resolvedPos = candidatePos;
            if (!TryResolvePose(ref resolvedPos, newRot))
                return;

            Vector3 savedVelWorld = BodyToWorld(AskedLocalVelocity);
            transform.position = resolvedPos;
            headingRotation = newRot;
            transform.rotation = newRot;
            ApplyUpDirection(newUp);
            AskedLocalVelocity = WorldToBody(savedVelWorld);
        }

        /// <summary>
        /// Solid-overlap test for a capsule at an arbitrary pose — the reorientation orbit safety.
        /// </summary>
        /// <param name="pos">Capsule center to test.</param>
        /// <param name="rot">Capsule orientation to test.</param>
        private bool CapsuleOverlaps(Vector3 pos, Quaternion rot)
        {
            CapsuleBodyPoints(pos, rot, out var p1, out var p2);
            return Physics.CheckCapsule(p1, p2, radius, ~toIgnore, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Expel the capsule pose (pos, rot) from any overlap by MTV de-penetration, a few iterations. The caller
        /// holds the rotation instead of clipping through if this fails (stuck in a non-convex mesh that
        /// ComputePenetration can't push out of).
        /// </summary>
        /// <param name="pos">Capsule center, adjusted in place to the resolved position.</param>
        /// <param name="rot">Capsule orientation to resolve at.</param>
        /// <param name="iterations">Max de-penetration passes.</param>
        /// <returns>True if the pose ended up (or already was) clear; false if stuck.</returns>
        private bool TryResolvePose(ref Vector3 pos, Quaternion rot, int iterations = 4)
        {
            for (int it = 0; it < iterations; it++)
            {
                CapsuleBodyPoints(pos, rot, out var p1, out var p2);

                int count = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, penetrationOverlaps,
                    ~toIgnore, QueryTriggerInteraction.Ignore);

                Vector3 push = Vector3.zero;
                bool overlap = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = penetrationOverlaps[i];
                    if (IsSelf(other)) continue;
                    overlap = true;
                    if (TryDepenetrate(other, pos, rot, out Vector3 dir, out float dist))
                        push += dir * (dist + 0.001f);
                }

                if (!overlap) return true;                       // clear
                if (push.sqrMagnitude < 1e-10f) return false;    // overlapping but unresolvable (non-convex mesh)
                pos += push;
            }
            return !CapsuleOverlaps(pos, rot);
        }

        /// <summary>
        /// Drive the player's yaw heading (called by the look system each frame). Folds the input yaw and
        /// platformYawDelta into the persistent heading around the current up, then applies the wall-ride's rotation
        /// about the cylinder's real spin axis (ridingWallHeadingDelta, identity off a wall). Transient camera-FX yaw
        /// is layered on afterward by the camera, not here, so it never bakes in.
        /// </summary>
        /// <param name="inputYawDelta">Yaw input this frame, in degrees.</param>
        public void ApplyLookYaw(float inputYawDelta)
        {
            // Seed from the authored facing the first time we are driven, so startup orientation holds.
            if (!hasHeading) { headingRotation = transform.rotation; hasHeading = true; }

            float yawThisFrame = inputYawDelta + platformYawDelta;

            // Keep inherited momentum world-stable through heading changes: save in world space, restore after.
            Vector3 savedVelWorld = BodyToWorld(AskedLocalVelocity);
            // Input + floor yaw about up; then the wall-ride rotation about the cylinder's actual spin axis (world
            // space, so the FACING follows the orbit correctly under tilted gravity).
            Quaternion rotated = ridingWallHeadingDelta * Quaternion.AngleAxis(yawThisFrame, currentUpDirection) * headingRotation;
            ridingWallHeadingDelta = Quaternion.identity; // consumed — reset so it can't linger past the ride
            // Re-orthogonalize body up back to gravity up: the spin-axis rotation tilts up too, but it must stay on
            // currentUpDirection (no creeping roll). Projecting the turned forward keeps the azimuth change; a no-op
            // for a pure yaw-about-up frame (off a wall).
            Vector3 fwd = Vector3.ProjectOnPlane(rotated * Vector3.forward, currentUpDirection);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(rotated * Vector3.right, currentUpDirection);
            headingRotation = Quaternion.LookRotation(fwd.normalized, currentUpDirection);
            // Layer the TRANSIENT yaw offset on top of the heading (never baked into headingRotation, so it can't
            // accumulate). Re-applied every frame here so it persists; identity when the offset is 0.
            transform.rotation = Quaternion.AngleAxis(yawOffset, currentUpDirection) * headingRotation;
            AskedLocalVelocity = WorldToBody(savedVelWorld);
        }

        private float yawOffset;

        /// <summary>
        /// A TRANSIENT yaw offset layered on top of the heading (around the current up axis), in degrees. Unlike
        /// <see cref="ApplyLookYaw"/> (which accumulates INTO the heading), this is ABSOLUTE — it REPLACES the previous
        /// offset, so assigning 0 removes it entirely. It also turns the movement direction (same frame), with world
        /// velocity preserved across the change. Takes effect immediately; ApplyLookYaw re-applies it every frame.
        /// </summary>
        public float CurrentYawOffset
        {
            get => yawOffset;
            set
            {
                if (!hasHeading) { headingRotation = transform.rotation; hasHeading = true; }
                Vector3 savedVelWorld = BodyToWorld(AskedLocalVelocity);
                yawOffset = value;
                transform.rotation = Quaternion.AngleAxis(yawOffset, currentUpDirection) * headingRotation;
                AskedLocalVelocity = WorldToBody(savedVelWorld);
            }
        }


        /// <summary>
        /// Integrate gravity along up (body-local .y). Grounded: clamp a sinking Y to 0. With gravityUsesTargetUp
        /// OFF while reorienting, pull along the TARGET up instead (so a slow animation doesn't drift gravity through
        /// the in-between angles). Terminal speed clamped at 50.
        /// </summary>
        private void ApplyGravity()
        {
            if (isGrounded)
            {
                if (AskedLocalVelocity.y <= 0f) AskedLocalVelocity.y = 0f; // cancel sinking into the surface
            }
            else if (gravityUsesTargetUp && Vector3.Dot(currentUpDirection, upDirectionTarget.normalized) < 0.999999f)
            {
                // Pull along the TARGET up (world impulse mapped into the visual-up frame); clamp along that same
                // gravity direction. Reduces to the simple branch once aligned.
                Vector3 gUp = upDirectionTarget.normalized;
                AskedLocalVelocity += WorldToBody(-gUp * (currentGravity * Time.deltaTime));
                Vector3 gDownLocal = WorldToBody(-gUp);
                float vDown = Vector3.Dot(AskedLocalVelocity, gDownLocal);
                if (maxFallSpeed > 0f && vDown > maxFallSpeed) AskedLocalVelocity -= gDownLocal * (vDown - maxFallSpeed);
            }
            else
            {
                AskedLocalVelocity.y -= currentGravity * Time.deltaTime;
                if (maxFallSpeed > 0f && AskedLocalVelocity.y < -maxFallSpeed) AskedLocalVelocity.y = -maxFallSpeed;
            }
        }
    
    
        #endregion

        #region CollideAndSlide
        /// <summary>
        /// The main movement pass: sweep AskedLocalVelocity (converted to world) against geometry, sliding along walls,
        /// climbing steps (CheckForStepUp) and snapping to ground (CheckForGroundSnap), then write the final position.
        /// Bounded by maxBounces. AskedLocalVelocity is NOT clipped against the planes here — the movement manager re-derives
        /// it each frame, so clipping fought that and jittered in obtuse corners.
        /// </summary>
        private void CollideAndSlide()
        {
            int maxBounces = 5;
            Vector3 validPosition = transform.position;

            // preserveSlopeInertia: on the leave frame, replace Y with the slope-consistent inertia (v_y = -(v_h·n)/(up·n))
            // instead of appliedVelocity.y (which holds a fake drop from edge corner-following). GATE: the fall just
            // started and only gravity touched Y (Y minus committed platform momentum == -gravity*dt), so jumps survive.
            if (preserveSlopeInertia && wasGrounded && !isGrounded)
            {
                if (!hasSteppedUp &&
                    Mathf.Abs((AskedLocalVelocity.y - momentumCommittedY) + currentGravity * Time.deltaTime) < 0.001f)
                {
                    Vector3 horizontalVel = Vector3.ProjectOnPlane(appliedVelocity, currentUpDirection);
                    float slopeY = SCC_Math.SlopeConsistentUp(horizontalVel, lastValidGroundNormal, currentUpDirection);
                    slopeY *= slopeY >= 0f ? slopeInertiaUpScale : slopeInertiaDownScale; // by sign: up- vs down-slope
                    // ADD on top of the committed platform momentum (assigning slopeY alone would wipe it).
                    AskedLocalVelocity.y = slopeY + momentumCommittedY;
                }
            }
            hasSteppedUp = false; // true only during the frame a step-up occurs (re-set by CheckForStepUp)

            // AskedLocalVelocity is body-local; collision resolves in WORLD. Convert once — every read below is world.
            Vector3 worldVel = BodyToWorld(AskedLocalVelocity);
            Vector3 finalVelocity = worldVel;
            if (isGrounded)
            {
                finalVelocity = SCC_Math.AlignVelocityWithSlope(
                    worldVel, groundNormal, currentUpDirection, flattenVelocityOnSlope, onlyWhenGoingDown);
            }

            Vector3 remainingMovement = finalVelocity * Time.deltaTime;

            // LEAVE-FRAME DOUBLE-COUNT FIX — deduct the carry's contribution (the carry already moved us by the
            // platform delta this frame, and the committed momentum would integrate it again → ~2× speed kick).
            remainingMovement -= momentumCarryComp;
            momentumCarryComp = Vector3.zero;

            int collisionCount = 0; // collisionNormals is a reused field (size = maxBounces), scratched per call

            for (int i = 0; i < maxBounces; i++)
            {
                float dist = remainingMovement.magnitude;
                Vector3 direction = remainingMovement.normalized;

                // Endpoints around the ROTATED centre so the cast capsule tracks the tilted body.
                CapsuleBodyPoints(validPosition, transform.rotation, out var point1, out var point2);

                // BACKSTEP the cast origin against the movement so an initial overlap is still seen (a CapsuleCast
                // ignores colliders it starts inside → a bulged-in mover would tunnel). hitDist converts back to a
                // distance from our actual position.
                float backstep = skinWidth * BackstepSkinFactor;
                Vector3 castP1 = point1 - direction * backstep;
                Vector3 castP2 = point2 - direction * backstep;

                if (Physics.CapsuleCast(castP1, castP2, radius, direction, out var hit, dist + backstep + skinWidth, ~toIgnore))
                {
                    float hitDist = Mathf.Max(0f, hit.distance - backstep);

                    // Push SCCPushable equipped Rigidbodies
                    if (pushRigidbodies)
                    {
                        Rigidbody hitRb = hit.collider.attachedRigidbody;
                        if (hitRb != null && !hitRb.isKinematic && hit.collider.TryGetComponent<SCC_Pushable>(out SCC_Pushable pushable))
                        {
                            Vector3 pushDir = Vector3.ProjectOnPlane(-hit.normal, currentUpDirection);
                            if (!(pushDir.sqrMagnitude > 0.0001f)) break;
                        
                            pushDir.Normalize();
                            float intoSpeed = Mathf.Max(0f, Vector3.Dot(finalVelocity, pushDir));
                            float targetSpeed = intoSpeed * (1f - pushable.PushedResistance);
                            float currentPushVel = Vector3.Dot(hitRb.linearVelocity + pushable.ScheduledVelocity, pushDir);
                            // Ignore an airborne body's negative push velocity so friction differences don't skew the result.
                            float effectiveVel = pushable.IsGrounded ? currentPushVel : Mathf.Max(currentPushVel, 0f);
                            float speedToAdd = Mathf.Clamp(targetSpeed - effectiveVel, 0f, targetSpeed);

                            // Stack the impulse; applied together in the next FixedUpdate.
                            if (speedToAdd > 0.001f) pushable.SchedulePush(pushDir * speedToAdd, hit.point);
                        }
                    }

                    Vector3 effectiveNormal = hit.normal;
                    float slopeAngle = Vector3.Angle(currentUpDirection, effectiveNormal);

                    // STEEP WALL while grounded: an upward-facing surface too steep to walk but not an overhang — treat
                    // as a vertical wall (block-and-slide), not a ramp. Used by the jump-into-wall lift and crease-slide below.
                    bool steepWallGrounded = isGrounded && slopeAngle > maxSlopeAngle && slopeAngle < maxWallAngle.y
                        && Vector3.Dot(hit.normal, currentUpDirection) >= 0f;

                    // Ceiling / inclined overhang (past the wall band): deflect along it, dropping only the
                    // into-component. Gated: neutralizeJumpOnCeiling covers airborne, isGrounded the walk-into case.
                    if (slopeAngle > maxWallAngle.y && (neutralizeJumpOnCeiling || isGrounded))
                        DeflectVelocityAlongCeiling(effectiveNormal);

                    // Step up (multiple per frame allowed, bounded by maxBounces, to chain-climb close stairs).
                    // Only attempt when the hit face is too steep to walk — navigable slopes are handled by collide-and-slide.
                    if (handleStepUp && (!onlyWhenGrounded || isGrounded) && slopeAngle > maxSlopeAngle)
                    {
                        if (CheckForStepUp(ref validPosition, remainingMovement, hitDist, hit.normal)) continue;
                    }

                    float snapDist = Mathf.Max(0f, hitDist - skinWidth);
                    validPosition += direction * snapDist;

                    // Leftover measured from where we ACTUALLY stopped (snapDist), not hit.distance — else every
                    // bounce drops skinWidth of travel. The projection below strips the into-wall part.
                    float remainingDist = Mathf.Max(0f, dist - snapDist);
                    remainingMovement = direction * remainingDist;

                    // Jump INTO a steep wall: commit the vertical lift here (the bounce loop keeps finding the same
                    // wall at distance 0). Intent read from AskedLocalVelocity.y (the jump), NOT remainingMovement.y
                    // (walking up a walkable slope also has +Y).
                    if (steepWallGrounded && AskedLocalVelocity.y > 0f)
                    {
                        float yMove = Vector3.Dot(remainingMovement, currentUpDirection);
                        if (yMove > 0f)
                        {
                            CapsuleBodyPoints(validPosition, transform.rotation, out var cp1, out var cp2);
                            if (Physics.CapsuleCast(cp1, cp2, radius, currentUpDirection, out var ceilHit,
                                    yMove + skinWidth, ~toIgnore))
                            {
                                yMove = Mathf.Max(0f, ceilHit.distance - skinWidth);
                                if (neutralizeJumpOnCeiling) AskedLocalVelocity.y = 0f;
                            }
                            validPosition += currentUpDirection * yMove;
                        }
                        remainingMovement -= currentUpDirection * Vector3.Dot(remainingMovement, currentUpDirection);
                        Vector3 flatNormal = Vector3.ProjectOnPlane(hit.normal, currentUpDirection).normalized;
                        if (flatNormal.sqrMagnitude > 0.0001f)
                        {
                            Vector3 tangent = Vector3.Cross(remainingMovement, flatNormal);
                            remainingMovement = Vector3.Cross(flatNormal, tangent);
                        }
                        else
                            remainingMovement = Vector3.zero;
                        continue;
                    }

                    // Already sliding parallel to or away from this surface — project and continue
                    // without counting a new collision.
                    if (collisionCount > 0 && Vector3.Dot(remainingMovement, effectiveNormal) >= -0.001f)
                    {
                        Vector3 tangent = Vector3.Cross(remainingMovement, effectiveNormal);
                        remainingMovement = Vector3.Cross(effectiveNormal, tangent);
                        continue;
                    }

                    collisionNormals[collisionCount] = effectiveNormal;
                    collisionCount++;

                    if (collisionCount == 1)
                    {
                        // Grounded into a steep wall: slide along the crease between ground and wall so we stay on
                        // the surface. Inside the count branch so a 2nd wall hit goes to count==2 (crease-stop)
                        // instead of looping — the concave-corner jitter cause.
                        if (steepWallGrounded)
                        {
                            Vector3 creaseDir = Vector3.Cross(groundNormal, hit.normal);
                            if (creaseDir.sqrMagnitude > 0.0001f)
                            {
                                creaseDir.Normalize();
                                remainingMovement = creaseDir * Vector3.Dot(remainingMovement, creaseDir);
                            }
                            else
                                remainingMovement = Vector3.zero;
                        }
                        else
                        {
                            Vector3 tangent = Vector3.Cross(remainingMovement, effectiveNormal);
                            remainingMovement = Vector3.Cross(effectiveNormal, tangent);
                        }
                    }
                    else if (collisionCount == 2)
                    {
                        Vector3 slideDir = Vector3.Cross(collisionNormals[0], collisionNormals[1]);
                        if (slideDir.sqrMagnitude > 0.0001f)
                        {
                            slideDir.Normalize();
                            float projectDot = Vector3.Dot(remainingMovement, slideDir);
                            remainingMovement = slideDir * projectDot;
                            // In obtuse corners the crease movement can re-enter one of the walls
                            // (float precision or angle > 90°) — stop instead of oscillating.
                            if (Vector3.Dot(remainingMovement, collisionNormals[0]) < -0.001f ||
                                Vector3.Dot(remainingMovement, collisionNormals[1]) < -0.001f)
                            {
                                remainingMovement = Vector3.zero;
                                break;
                            }
                        }
                        else
                        {
                            remainingMovement = Vector3.zero;
                            break;
                        }
                    }
                    else
                    {
                        remainingMovement = Vector3.zero;
                        break;
                    }
                }
                else
                {
                    validPosition += remainingMovement;
                    CheckForGroundSnap(ref validPosition);
                    break;
                }
            }

            // AskedLocalVelocity is deliberately NOT clipped against the planes — the bounce loop resolves POSITION and
            // the movement manager re-derives x/z each frame, so clipping fought it (clip→re-add→clip) and jittered in corners.
            transform.position = validPosition;
        }


        /// <summary>
        /// Try to step up over an obstacle in the path: clearance cast up, wall cast forward at step height, then a
        /// down-ray to find the tread. On success, raise the feet onto it, re-ground, re-acquire currentSurface (so a
        /// moving platform's carry survives the step) and open the leave-suppress grace.
        /// </summary>
        /// <param name="validPosition">Current valid position, raised in place on a successful step.</param>
        /// <param name="leftover">Remaining movement this bounce, used as the step direction.</param>
        /// <param name="hitDistance">Distance to the riser that triggered this attempt.</param>
        /// <param name="hitNormal">Normal of the riser that triggered this attempt.</param>
        /// <returns>True if it stepped.</returns>
        private bool CheckForStepUp(ref Vector3 validPosition, Vector3 leftover, float hitDistance = 0f, Vector3 hitNormal = default)
        {
            Vector3 stepPos = validPosition;

            // ----- CapsuleCast up to check for a ceiling above the step (return if blocked) -----
            CapsuleFeetPoints(stepPos, out var point1, out var point2);

            Physics.CapsuleCast(point1, point2, radius, currentUpDirection, out var upHit, maxStepHeight + skinWidth, ~toIgnore);
            if (upHit.collider) return false;

            // ----- CapsuleCast forward at step height to test for a wall (return if blocked) -----
            stepPos += currentUpDirection * maxStepHeight;
            CapsuleFeetPoints(stepPos, out point1, out point2);

            // Nothing to step toward if leftover is essentially zero.
            if (leftover.sqrMagnitude <= 0.0001f) return false;
            Vector3 moveDir = leftover.normalized;

            // Forward check at step height: actual movement + skinWidth only (we're above the current riser).
            Physics.CapsuleCast(point1, point2, radius, moveDir, out var forwardHit, leftover.magnitude + skinWidth, ~toIgnore);
            if (forwardHit.collider) return false;

            // DOWN-RAY to find the tread, probed from the riser-contact point advanced (radius + skinWidth) along the
            // riser's INWARD direction then lifted to step height (advancing along moveDir under-reaches on diagonal
            // approaches). A ray, not a capsule, so it samples only the tread below. Vertical normal → fall back to moveDir.
            Vector3 riserInward = -Vector3.ProjectOnPlane(hitNormal, currentUpDirection);
            Vector3 moveFlat = Vector3.ProjectOnPlane(moveDir, currentUpDirection);
            Vector3 probePoint;
            if (riserInward.sqrMagnitude > 0.01f)
            {
                riserInward.Normalize();
                Vector3 horizOffset = Vector3.ProjectOnPlane(
                    moveFlat * hitDistance + riserInward * (radius + skinWidth), currentUpDirection);
                probePoint = validPosition + horizOffset + currentUpDirection * maxStepHeight;
            }
            else
            {
                probePoint = stepPos + moveFlat * (hitDistance + radius + skinWidth);
            }
            Vector3 rayOrigin = probePoint + currentUpDirection * skinWidth;          // start just above tread level
            float   rayLength = maxStepHeight + skinWidth * 3f;

            if (!Physics.Raycast(rayOrigin, -currentUpDirection, out var downRay, rayLength, ~toIgnore))
                return false;

            Vector3 landPoint    = downRay.point;
            Vector3 treadNormal  = downRay.normal;
            Collider treadCollider = downRay.collider;

            // Re-probe a bit further inward (moveDir) for the tread's flat-face normal/height, not its front edge.
            Vector3 probeOrigin = downRay.point + currentUpDirection * (skinWidth * 2f) + moveDir * skinWidth;
            if (Physics.Raycast(probeOrigin, -currentUpDirection, out var probeHit, skinWidth * 4f, ~toIgnore))
            {
                treadNormal    = probeHit.normal;
                landPoint      = probeHit.point;
                treadCollider  = probeHit.collider;
            }

            // Step height measured ALONG up (not world Y).
            float actualStepHeight = Vector3.Dot(landPoint - validPosition, currentUpDirection);
            if (actualStepHeight > maxStepHeight || actualStepHeight < 0.001f)
                return false;

            float angle = Vector3.Angle(currentUpDirection, treadNormal);
            if (angle > maxSlopeAngle) return false;

            // Raise the feet along up to rest skinWidth above the tread (horizontal position preserved).
            validPosition += currentUpDirection * (actualStepHeight + skinWidth);

            hasSteppedUp = true;
            isGrounded = true;
            // RE-ACQUIRE the surface (GroundCheck may have blinked it null this frame) so UpdateSurfaceAnchor keeps
            // the carry alive across the step — else on a moving platform we'd slide back relative to it.
            currentSurface = ResolveDynamicSurface(treadCollider);
            // Suppress the leave hand-off for a grace window (a step can blink the ground check after) and refund a
            // commit already made this frame — we stepped, we didn't leave.
            leaveSuppressTimer = LeaveSuppressGrace;
            CancelLeaveMomentumIfCommitted();

            return true;
        }


        /// <summary>
        /// Snap the feet down onto ground just below (small step-down) so we stay grounded instead of
        /// briefly floating then re-falling. Gated by a centre-line walkable-ground ray (so a ledge releases, not
        /// snaps). Runs on dynamic surfaces too — the up-push (ResolvePenetrations) and this down-pull can't both
        /// fire a frame (when seated, snapAmount ≤ 0), so there's no oscillation.
        /// </summary>
        /// <param name="validPosition">Current valid position, lowered in place on a successful snap.</param>
        private void CheckForGroundSnap(ref Vector3 validPosition)
        {
            if (!handleGroundSnap) return;
            if (AskedLocalVelocity.y > 0.1f) return;
            if (!wasGrounded) return;

            // Only a snap from a GroundCheck-airborne frame recovers a genuinely lost ground (a step-down / blink
            // HandleSurfaceMomentum may have misread as a leave); a snap while already grounded is cosmetic tracking.
            bool recoveringGround = !isGrounded;

            CapsuleFeetPoints(validPosition, out var point1, out var point2);

            // CENTER-LINE WALKABLE GATE (see IsCenterOverWalkableGround) — a zero-radius down-ray from the feet
            // centre, so unlike the radius-0.5 CapsuleCast below it can't grab the trailing corner of the platform
            // we just walked off (which would snap us down over open air). Also rejects an unwalkable surface below.
            if (!IsCenterOverWalkableGround(validPosition, maxSnapDistance + skinWidth))
                return;

            if (!Physics.CapsuleCast(point1, point2, radius, -currentUpDirection, out var downHit, maxSnapDistance + skinWidth, ~toIgnore))
                return;

            // Re-probe outward for the true normal (the raw CapsuleCast normal is skewed at a tread edge → false
            // angle rejection). Snap AMOUNT still comes from downHit.distance; only the normal is replaced.
            Vector3 toHit = Vector3.ProjectOnPlane(downHit.point - validPosition, currentUpDirection);
            Vector3 probeOffset = toHit.sqrMagnitude > 1e-4f ? toHit.normalized * skinWidth : Vector3.zero;
            Vector3 probeOrigin = downHit.point + probeOffset + currentUpDirection * skinWidth;
            Vector3 snapNormal  = downHit.normal;
            if (Physics.Raycast(probeOrigin, -currentUpDirection, out var probeHit, maxSnapDistance + skinWidth * 2f, ~toIgnore))
                snapNormal = probeHit.normal;

            if (Vector3.Angle(currentUpDirection, snapNormal) > maxSlopeAngle) return;

            float snapAmount = downHit.distance - skinWidth;
            if (snapAmount <= 0f) return;

            // Block on the TOP sphere only so a riser wall beside us doesn't prevent the snap. Triggers ignored
            // (our own ComputePenetration trigger overlaps this sphere and would block every snap).
            Vector3 snappedTopSphere = point2 + -currentUpDirection * snapAmount;
            if (Physics.CheckSphere(snappedTopSphere, radius - skinWidth, ~toIgnore, QueryTriggerInteraction.Ignore)) return;

            validPosition -= currentUpDirection * snapAmount;
            isGrounded = true;
            // RE-ACQUIRE the surface so the carry tracks where we actually rest (static → null).
            currentSurface = ResolveDynamicSurface(downHit.collider);

            // Arm the grace + refund ONLY when the snap recovered a lost ground. A cosmetic snap fires every frame on a
            // vertically-moving platform — arming it there keeps leaveSuppressTimer permanently hot and kills the real
            // leave-momentum on jump/fall-off (the "V+H platform loses inertia" bug).
            if (recoveringGround)
            {
                leaveSuppressTimer = LeaveSuppressGrace;
                CancelLeaveMomentumIfCommitted();
            }
        }

        #endregion
    }
}
