using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class SplitBodyManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private BodyHalf torsoPrefab;
    [SerializeField] private BodyHalf legsPrefab;

    [Header("Whole Body")]
    [SerializeField] private Transform wholeBodyCameraTarget;
    [SerializeField] private GameObject wholeVisual;
    [SerializeField] private CharacterController wholeCC;
    [SerializeField] private PlayerMovement wholeMovement;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera vcam; 
    [SerializeField] private Transform cinemachineCameraTarget;

    [Header("Split Settings")]
    [SerializeField] private Vector3 torsoSpawnOffset = new Vector3(0.2f, 0f, 0f);
    [SerializeField] private Vector3 legsSpawnOffset = new Vector3(-0.2f, 0f, 0f);

    [Header("Recombine")]
    [SerializeField] private float recombineDistance = 1.2f;

    [Header("Pickup")]
    [SerializeField] private Transform wholeHoldPoint;
    [SerializeField] private float pickupDistance = 2.0f;
    [SerializeField] private LayerMask grabbableMask;
    [SerializeField] private Vector3 dropForwardOffset = new Vector3(0f, 0f, 1.0f);
    [SerializeField] private float dropSideOffset = 0.6f;
    [SerializeField] private float dropUpOffset = 0.2f;
    [SerializeField] private float dropGroundRayHeight = 2.0f;
    [SerializeField] private float dropGroundRayDistance = 6.0f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float dropIgnoreSeconds = 0.25f;


    private ObjectGrabbable heldGrabbable;
    private Rigidbody heldRb;

    private Vector3 _wholeCamLocalPos;
    private Quaternion _wholeCamLocalRot;
    private BodyHalf torsoInstance;
    private BodyHalf legsInstance;
    private BodyHalf activeHalf;
    private bool isSplit;
    private PlayerMovement CurrentMovement =>
        isSplit ? activeHalf?.movement : wholeMovement;

    private bool IsCarryingLegs =>
    heldGrabbable != null &&
    heldGrabbable.TryGetComponent<BodyHalfCargo>(out var cargo) &&
    cargo.Half.type == HalfType.Legs;


    private void OnSplit()
    {
        if (isSplit) return;
        Split();
    }

    private void OnSwap()
    {
        if (IsCarryingLegs) return;
        if (!isSplit || torsoInstance == null || legsInstance == null) return;
        SwapControl();
    }

    private void OnCombine()
    {
        if (IsCarryingLegs) return;
        if (!isSplit || torsoInstance == null || legsInstance == null) return;
        TryRecombine();
    }

    private void Awake()
    {
        EnsureCameraTargetAssigned();

        if (vcam != null && cinemachineCameraTarget != null)
            vcam.Follow = cinemachineCameraTarget;
    }


    private void Start()
    {
        if (wholeBodyCameraTarget != null)
        {
            _wholeCamLocalPos = wholeBodyCameraTarget.localPosition;
            _wholeCamLocalRot = wholeBodyCameraTarget.localRotation;
        }

        if (cinemachineCameraTarget != null)
            cinemachineCameraTarget.position = CurrentFollowPoint.position;
    }



    private void Split()
    {
        isSplit = true;

        if (wholeMovement) wholeMovement.enabled = false;
        if (wholeCC) wholeCC.enabled = false;
        if (wholeVisual) wholeVisual.SetActive(false);

        Vector3 basePos = transform.position;
        Quaternion baseRot = transform.rotation;

        torsoInstance = Instantiate(torsoPrefab, basePos + torsoSpawnOffset, baseRot);
        legsInstance = Instantiate(legsPrefab, basePos + legsSpawnOffset, baseRot);

        // Share same camera target for Look()
        torsoInstance.movement.SetCameraTarget(cinemachineCameraTarget);
        legsInstance.movement.SetCameraTarget(cinemachineCameraTarget);

        // Start controlling torso
        EnsureCameraTargetAssigned();
        SetActiveHalf(torsoInstance);

    }

    private void SetActiveHalf(BodyHalf half)
    {
        activeHalf = half;

        torsoInstance.SetActiveControl(activeHalf == torsoInstance);
        legsInstance.SetActiveControl(activeHalf == legsInstance);

        activeHalf.movement.SetCursorLocked(true);

        EnsureCameraTargetAssigned();

        // snap follow point immediately
        if (cinemachineCameraTarget != null)
            cinemachineCameraTarget.position = CurrentFollowPoint.position;

        activeHalf.movement.SyncLookFromTarget();
    }


    private void LateUpdate()
    {
        if (cinemachineCameraTarget == null) return;

        Transform follow = CurrentFollowPoint;
        if (follow == null) return;

        cinemachineCameraTarget.position = follow.position;

        //Debug.Log($"Follow point: {follow.name} | camTarget pos: {cinemachineCameraTarget.position}");
    }



    private void SwapControl()
    {
        SetActiveHalf(activeHalf == torsoInstance ? legsInstance : torsoInstance);
    }

    private void TryRecombine()
    {
        if (Vector3.Distance(torsoInstance.transform.position, legsInstance.transform.position) > recombineDistance)
            return;

        // Disable halves FIRST so they don't keep rotating the shared camera target this frame
        if (torsoInstance?.movement) torsoInstance.movement.SetCanControl(false);
        if (legsInstance?.movement) legsInstance.movement.SetCanControl(false);

        // Desired follow point: midpoint of each half's camera target (or their root)
        Vector3 torsoFollow = torsoInstance.cameraTarget ? torsoInstance.cameraTarget.position : torsoInstance.transform.position;
        Vector3 legsFollow = legsInstance.cameraTarget ? legsInstance.cameraTarget.position : legsInstance.transform.position;
        Vector3 desiredFollowPos = (torsoFollow + legsFollow) * 0.5f;

        // Move root so wholeBodyCameraTarget ends up at desiredFollowPos
        if (wholeBodyCameraTarget != null)
        {
            Vector3 delta = desiredFollowPos - wholeBodyCameraTarget.position;
            transform.position += delta;
        }
        else
        {
            transform.position = desiredFollowPos;
        }


        // Clean up split state BEFORE snapping camera target (so CurrentFollowPoint uses whole target)
        Destroy(torsoInstance.gameObject);
        Destroy(legsInstance.gameObject);

        torsoInstance = null;
        legsInstance = null;
        activeHalf = null;
        isSplit = false;

        if (wholeBodyCameraTarget != null)
        {
            wholeBodyCameraTarget.localPosition = _wholeCamLocalPos;
            wholeBodyCameraTarget.localRotation = _wholeCamLocalRot;
        }

        // Re-enable whole
        if (wholeVisual) wholeVisual.SetActive(true);
        if (wholeCC) wholeCC.enabled = true;
        if (wholeMovement) wholeMovement.enabled = true;

        EnsureCameraTargetAssigned();

        // Snap the cinemachine follow target immediately to avoid a pop
        if (cinemachineCameraTarget != null)
            cinemachineCameraTarget.position = CurrentFollowPoint.position;

        // Sync yaw/pitch so mouse look doesn't "jump"
        if (wholeMovement) wholeMovement.SyncLookFromTarget();
    }

    private void TryPickUpGrabbable()
    {
        Transform hold = CurrentHoldPoint;
        if (hold == null) return;

        // Search around the currently controlled body
        Vector3 origin = isSplit && activeHalf != null ? activeHalf.transform.position : transform.position;

        Collider[] hits = Physics.OverlapSphere(origin, pickupDistance, grabbableMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return;

        ObjectGrabbable best = null;
        float bestD = float.MaxValue;

        foreach (var h in hits)
        {
            ObjectGrabbable g = h.GetComponentInParent<ObjectGrabbable>();
            if (g == null) continue;

            float d = Vector3.Distance(origin, g.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = g;
            }
        }

        if (best == null) return;

        heldGrabbable = best;
        heldRb = best.GetComponent<Rigidbody>();

        // Snap once so it doesn’t “yo-yo” from where it was
        if (heldRb != null)
        {
            heldRb.position = hold.position;
            heldRb.rotation = hold.rotation;
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
        }

        heldGrabbable.Grab(hold);
    }


    private void DropHeld()
    {
        if (heldGrabbable == null) return;

        Transform holdOwner = isSplit && activeHalf != null ? activeHalf.transform : transform;

        // choose a safe desired position
        Vector3 right = holdOwner.right;
        Vector3 forward = holdOwner.forward;

        Vector3 desired = holdOwner.position
                        + forward * dropForwardOffset.z
                        + right * dropSideOffset
                        + Vector3.up * dropUpOffset;

        // snap to ground
        Vector3 rayStart = desired + Vector3.up * dropGroundRayHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out var hit, dropGroundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            desired.y = hit.point.y + 0.05f;

        // Temporarily ignore collisions between torso + legs on re-enable
        if (isSplit && torsoInstance != null && heldGrabbable != null)
            IgnoreCollisionTemporarily(torsoInstance.gameObject, heldGrabbable.gameObject, dropIgnoreSeconds);

        // Move it OUT OF the player BEFORE enabling physics
        if (heldRb != null)
        {
            heldRb.isKinematic = true;        // keep frozen during teleport
            heldRb.detectCollisions = false;  // avoid depenetration on teleport
            heldRb.position = desired;
            heldRb.rotation = holdOwner.rotation;
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
        }
        else
        {
            heldGrabbable.transform.position = desired;
            heldGrabbable.transform.rotation = holdOwner.rotation;
        }

        // NOW enable physics via Drop()
        heldGrabbable.Drop();

        // Re-apply who is active (prevents dropped half from stealing camera control)
        if (isSplit && torsoInstance != null && legsInstance != null && activeHalf != null)
        {
            torsoInstance.SetActiveControl(activeHalf == torsoInstance);
            legsInstance.SetActiveControl(activeHalf == legsInstance);

            activeHalf.movement.SetCursorLocked(true);
            activeHalf.movement.SyncLookFromTarget();
        }

        heldGrabbable = null;
        heldRb = null;
    }


    private void IgnoreCollisionTemporarily(GameObject a, GameObject b, float seconds)
    {
        var aCols = a.GetComponentsInChildren<Collider>();
        var bCols = b.GetComponentsInChildren<Collider>();

        foreach (var c1 in aCols)
            foreach (var c2 in bCols)
                Physics.IgnoreCollision(c1, c2, true);

        StartCoroutine(Reenable());

        System.Collections.IEnumerator Reenable()
        {
            yield return new WaitForSeconds(seconds);
            foreach (var c1 in aCols)
                foreach (var c2 in bCols)
                    if (c1 && c2) Physics.IgnoreCollision(c1, c2, false);
        }
    }


    private Transform CurrentHoldPoint
    {
        get
        {
            if (isSplit && activeHalf != null)
                return activeHalf.holdPoint != null ? activeHalf.holdPoint : activeHalf.transform;

            return wholeHoldPoint != null ? wholeHoldPoint : transform;
        }
    }


    private void EnsureCameraTargetAssigned()
    {
        if (cinemachineCameraTarget == null) return;

        if (wholeMovement != null)
            wholeMovement.SetCameraTarget(cinemachineCameraTarget);

        if (torsoInstance != null && torsoInstance.movement != null)
            torsoInstance.movement.SetCameraTarget(cinemachineCameraTarget);

        if (legsInstance != null && legsInstance.movement != null)
            legsInstance.movement.SetCameraTarget(cinemachineCameraTarget);
    }


    private Transform CurrentFollowPoint
    {
        get
        {
            if (isSplit && activeHalf != null)
                return activeHalf.cameraTarget != null ? activeHalf.cameraTarget : activeHalf.transform;

            return wholeBodyCameraTarget != null ? wholeBodyCameraTarget : transform;
        }
    }



    private void OnMove(InputValue v)
    {
        CurrentMovement?.SetMoveInput(v.Get<Vector2>());
    }

    private void OnLookMouse(InputValue v)
    {
        var val = v.Get<Vector2>();
        Debug.Log("LOOK MOUSE INPUT: " + val);
        CurrentMovement?.SetLookMouse(val);
    }


    private void OnLookStick(InputValue v)
    {
        //Debug.Log("LookStick: " + v.Get<Vector2>());
        CurrentMovement?.SetLookStick(v.Get<Vector2>());
    }

    private void OnJump()
    {
        CurrentMovement?.QueueJump();
    }

    private void OnPickUp()
    {
        if (heldGrabbable != null)
        {
            DropHeld();
            return;
        }

        // Only torso can pick up legs
        if (isSplit && activeHalf != null && activeHalf.type != HalfType.Torso) return;

        TryPickUpGrabbable();
    }


}
