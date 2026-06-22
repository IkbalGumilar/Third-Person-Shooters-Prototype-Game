using UnityEngine;

public class EquippedWeapon : MonoBehaviour
{
    public Transform muzzlePoint;
    public Transform shellEjectPoint;
    public Transform rightHandIKTarget;
    public Transform leftHandIKTarget;

    [Header("Offset Sources")]
    public Transform holdOffsetTransform;
    public Transform aimHoldOffsetTransform;
    public Transform rightHandAimRotationOffsetTransform;
}
