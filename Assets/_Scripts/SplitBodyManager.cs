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

    private Vector3 _wholeCamLocalPos;
    private Quaternion _wholeCamLocalRot;
    private BodyHalf torsoInstance;
    private BodyHalf legsInstance;
    private BodyHalf activeHalf;
    private bool isSplit;
    private PlayerMovement CurrentMovement =>
        isSplit ? activeHalf?.movement : wholeMovement;

    private void OnSplit()
    {
        if (isSplit) return;
        Split();
    }

    private void OnSwap()
    {
        if (!isSplit || torsoInstance == null || legsInstance == null) return;
        SwapControl();
    }

    private void OnCombine()
    {
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

        // TEMP DEBUG
        Debug.Log($"Follow point: {follow.name} | camTarget pos: {cinemachineCameraTarget.position}");
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
        Debug.Log("LookMouse: " + v.Get<Vector2>());
        CurrentMovement?.SetLookMouse(v.Get<Vector2>());
    }

    private void OnLookStick(InputValue v)
    {
        Debug.Log("LookStick: " + v.Get<Vector2>());
        CurrentMovement?.SetLookStick(v.Get<Vector2>());
    }


    private void OnJump()
    {
        CurrentMovement?.QueueJump();
    }
}
