using UnityEngine;

public class PlayerRecoil : MonoBehaviour
{
    public Transform recoilTarget;
    public float fallbackSnappiness = 18f;
    public float fallbackReturnSpeed = 10f;
    public float fallbackMaxOffset = 0.35f;

    private Vector3 baseLocalPosition;
    private Vector2 targetOffset;
    private Vector2 currentOffset;
    private Weapon activeWeapon;
    private bool hasBasePosition;
    private CameraControler cameraControler;
    private PlayerAimIK playerAimIK;

    void Awake()
    {
        cameraControler = GetComponent<CameraControler>();
        playerAimIK = GetComponent<PlayerAimIK>();
        CacheBasePosition();
    }

    void LateUpdate()
    {
        if (recoilTarget == null)
        {
            return;
        }

        CacheBasePosition();

        float snappiness = activeWeapon != null ? activeWeapon.recoilSnappiness : fallbackSnappiness;
        float returnSpeed = activeWeapon != null ? activeWeapon.recoilReturnSpeed : fallbackReturnSpeed;

        targetOffset = Vector2.Lerp(targetOffset, Vector2.zero, returnSpeed * Time.deltaTime);
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, snappiness * Time.deltaTime);

        if (ShouldUseCameraAimOffset())
        {
            cameraControler.SetAimRecoilOffset(currentOffset);
            return;
        }

        recoilTarget.localPosition = baseLocalPosition + new Vector3(currentOffset.x, currentOffset.y, 0f);
    }

    void OnDisable()
    {
        if (recoilTarget != null && hasBasePosition)
        {
            recoilTarget.localPosition = baseLocalPosition;
        }

        if (cameraControler != null)
        {
            cameraControler.SetAimRecoilOffset(Vector2.zero);
        }
    }

    public void ApplyRecoil(Weapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        activeWeapon = weapon;
        playerAimIK = playerAimIK != null ? playerAimIK : GetComponent<PlayerAimIK>();
        playerAimIK?.ApplyWeaponBodyRecoil(weapon);

        if (recoilTarget == null)
        {
            return;
        }

        CacheBasePosition();

        float sideRange = Mathf.Abs(weapon.recoilPerShot.x);
        float side = Random.Range(-sideRange, sideRange);
        float up = weapon.recoilPerShot.y;
        float maxOffset = weapon.maxRecoilOffset > 0f ? weapon.maxRecoilOffset : fallbackMaxOffset;

        targetOffset += new Vector2(side, up);
        targetOffset = Vector2.ClampMagnitude(targetOffset, maxOffset);
    }

    public void SetTarget(Transform target)
    {
        recoilTarget = target;
        hasBasePosition = false;
        targetOffset = Vector2.zero;
        currentOffset = Vector2.zero;
        CacheBasePosition();
    }

    void CacheBasePosition()
    {
        if (hasBasePosition || recoilTarget == null)
        {
            return;
        }

        baseLocalPosition = recoilTarget.localPosition;
        hasBasePosition = true;
    }

    bool ShouldUseCameraAimOffset()
    {
        return cameraControler != null
            && cameraControler.stickAimToCameraRay
            && recoilTarget == cameraControler.aimLookTarget;
    }
}
