using UnityEngine;

[RequireComponent(typeof(BodyHalf))]
public class BodyHalfCargo : MonoBehaviour
{
    public BodyHalf Half { get; private set; }

    private void Awake()
    {
        Half = GetComponent<BodyHalf>();
    }
}
