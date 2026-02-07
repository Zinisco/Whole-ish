using UnityEngine;

public enum HalfType { Torso, Legs }

public class BodyHalf : MonoBehaviour
{
    public HalfType type;
    public PlayerMovement movement;   // drag reference in prefab
    public Transform cameraTarget;    // a child transform for camera follow (optional)

    public void SetActiveControl(bool active)
    {
        movement.SetCanControl(active);
    }
}
