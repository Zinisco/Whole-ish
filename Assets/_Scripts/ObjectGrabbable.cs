using UnityEngine;

public class ObjectGrabbable : MonoBehaviour
{
    [SerializeField] private float gravityMultiplier = 3f;

    private Rigidbody objectRigidbody;
    private Transform objectGrabPointTransform;

    private int originalLayer;
    private BodyHalfCargo bodyHalfCargo;

    private Collider[] cachedColliders;

    private void Awake()
    {
        objectRigidbody = GetComponent<Rigidbody>();
        bodyHalfCargo = GetComponent<BodyHalfCargo>();

        // Cache all colliders (including child colliders if your model has them)
        cachedColliders = GetComponentsInChildren<Collider>();
    }

    public void Grab(Transform grabPoint)
    {
        objectGrabPointTransform = grabPoint;

        if (bodyHalfCargo != null)
            bodyHalfCargo.Half.SetHeldState(true);

        // Turn OFF physics interaction while held (prevents CC explosions)
        if (objectRigidbody != null)
        {
            objectRigidbody.isKinematic = true;
            objectRigidbody.useGravity = false;
            objectRigidbody.detectCollisions = false; // IMPORTANT
            objectRigidbody.linearVelocity = Vector3.zero;
            objectRigidbody.angularVelocity = Vector3.zero;
        }

        originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("HeldObject");

        // Optional: also disable colliders while held (extra-safe)
        // If you do this, you can remove it if you WANT the held object to bump the world.
        foreach (var c in cachedColliders)
            c.enabled = false;
    }

    public void Drop()
    {
        objectGrabPointTransform = null;

        // Re-enable colliders first or after placement (we’ll place before Drop() in SplitBodyManager)
        foreach (var c in cachedColliders)
            c.enabled = true;

        if (objectRigidbody != null)
        {
            objectRigidbody.detectCollisions = true;
            objectRigidbody.isKinematic = false;
            objectRigidbody.useGravity = true;

            objectRigidbody.linearVelocity = Vector3.zero;
            objectRigidbody.angularVelocity = Vector3.zero;
        }

        gameObject.layer = originalLayer;

        if (bodyHalfCargo != null)
            bodyHalfCargo.Half.SetHeldState(false);
    }

    private void LateUpdate()
    {
        if (objectGrabPointTransform != null)
        {
            transform.position = objectGrabPointTransform.position;
            transform.rotation = objectGrabPointTransform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (objectRigidbody != null && !objectRigidbody.isKinematic)
        {
            objectRigidbody.AddForce(
                Physics.gravity * (gravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }
}
