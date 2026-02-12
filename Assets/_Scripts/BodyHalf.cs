using UnityEngine;

public enum HalfType { Torso, Legs }

public class BodyHalf : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterController cc;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider physicsCollider; // CapsuleCollider / BoxCollider used for cargo collisions

    [Header("Info")]
    public HalfType type;
    public PlayerMovement movement;
    public Transform cameraTarget;
    public Transform holdPoint;

    public bool IsBeingHeld { get; private set; }

    private void Awake()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!physicsCollider) physicsCollider = GetComponent<Collider>(); // assign the CapsuleCollider here (NOT the CC)

        // Good defaults if you use Rigidbody for cargo
        if (rb)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public void SetActiveControl(bool active)
    {
        IsBeingHeld = false;

        if (movement) movement.SetCanControl(active);

        if (active)
        {
            // PLAYER MODE
            if (cc) cc.enabled = true;

            if (rb)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (physicsCollider) physicsCollider.enabled = false; // avoid double collision with CC
        }
        else
        {
            // CARGO MODE (on ground)
            if (cc) cc.enabled = false;

            if (rb)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            if (physicsCollider) physicsCollider.enabled = true; // REQUIRED so it doesn't fall through
        }
    }

    public void SetHeldState(bool held)
    {
        IsBeingHeld = held;

        if (!held) return;   // IMPORTANT: do not re-enable control here

        if (movement) movement.SetCanControl(false);

        if (cc) cc.enabled = false;

        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (physicsCollider) physicsCollider.enabled = true;
    }

}
