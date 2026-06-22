using System.Collections;
using UnityEngine;

[System.Serializable]
public class WeaponSlot
{
    public Weapon weapon;
    public GameObject weaponObject;
}

public class PlayerWeaponEquip : MonoBehaviour
{
    public Weapon startingWeapon;
    public WeaponSlot[] weaponSlots;
    public int currentWeaponIndex;
    public Transform weaponSocket;
    public GameObject existingWeaponObject;
    public PlayerShoot playerShoot;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerMovement playerMovement;
    public PlayerBlockController blockController;
    public PlayerMeleeController meleeController;
    public bool allowNumberKeySwitch = true;
    public bool allowMouseWheelSwitch = false;
    public bool useAssignedHotkeys = true;
    public WeaponSlot[] hotkeyWeaponSlots = new WeaponSlot[4];
    public bool allowInput = true;
    public bool equipOnStart;
    public bool playSwitchAnimationOnFirstEquip;
    public bool playSwitchAnimationOnlyWhenGrounded = true;
    public float weaponSwapDelay = 0.18f;
    public float switchAimIKResumeLeadTime = 0.12f;
    public float switchAimIKFadeOutTime = 0.08f;
    public bool preserveWeaponPrefabLocalTransform = true;

    private GameObject equippedWeaponObject;
    private bool equippedObjectWasInstantiated;
    private Coroutine equipRoutine;
    private float switchAimIKStartTime;
    private float switchAimIKResumeTime;
    private float switchAimIKEndTime;
    private KontrolPemain kontrolPemain;

    public Weapon CurrentWeapon { get; private set; }
    public GameObject CurrentEquippedWeaponObject => equippedWeaponObject;
    public EquippedWeapon CurrentEquippedWeapon { get; private set; }
    public bool IsSwitching { get; private set; }
    public float AimIKSwitchBlend => GetAimIKSwitchBlend();
    public bool ShouldSuppressAimIKForSwitch => IsSwitching && AimIKSwitchBlend <= 0.001f;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Start()
    {
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        blockController = blockController != null ? blockController : GetComponent<PlayerBlockController>();
        meleeController = meleeController != null ? meleeController : GetComponent<PlayerMeleeController>();

        if (!equipOnStart)
        {
            UnequipWeapon();
            return;
        }

        if (weaponSlots != null && weaponSlots.Length > 0)
        {
            EquipWeapon(Mathf.Clamp(currentWeaponIndex, 0, weaponSlots.Length - 1), playSwitchAnimationOnFirstEquip);
        }
        else if (startingWeapon != null)
        {
            EquipWeapon(startingWeapon, null, playSwitchAnimationOnFirstEquip);
        }
    }

    void Update()
    {
        if (!allowInput || IsActionBlockingSwitch())
        {
            return;
        }

        if (allowNumberKeySwitch)
        {
            int keyCount = useAssignedHotkeys
                ? 4
                : weaponSlots != null ? weaponSlots.Length : 0;

            for (int i = 0; i < keyCount && i < 9; i++)
            {
                if (IsHotkeyPressedThisFrame(i))
                {
                    if (useAssignedHotkeys)
                    {
                        TryEquipHotkey(i);
                        return;
                    }

                    EquipWeapon(i);
                    return;
                }
            }
        }

        if (allowMouseWheelSwitch)
        {
            if (kontrolPemain != null && kontrolPemain.Pemain.Next.WasPressedThisFrame())
            {
                EquipNextWeapon();
            }
            else if (kontrolPemain != null && kontrolPemain.Pemain.Previous.WasPressedThisFrame())
            {
                EquipPreviousWeapon();
            }
        }
    }

    bool IsHotkeyPressedThisFrame(int index)
    {
        if (kontrolPemain == null)
        {
            return false;
        }

        return index switch
        {
            0 => kontrolPemain.Pemain.Hotkey1.WasPressedThisFrame(),
            1 => kontrolPemain.Pemain.Hotkey2.WasPressedThisFrame(),
            2 => kontrolPemain.Pemain.Hotkey3.WasPressedThisFrame(),
            3 => kontrolPemain.Pemain.Hotkey4.WasPressedThisFrame(),
            _ => false
        };
    }

    public void EquipNextWeapon()
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
        {
            return;
        }

        int nextIndex = FindNextWeaponIndex(1);
        if (nextIndex >= 0)
        {
            EquipWeapon(nextIndex);
        }
    }

    public void EquipPreviousWeapon()
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
        {
            return;
        }

        int nextIndex = FindNextWeaponIndex(-1);
        if (nextIndex >= 0)
        {
            EquipWeapon(nextIndex);
        }
    }

    public void SetHotkeyWeapon(int hotkeyIndex, Weapon weapon, GameObject weaponObject)
    {
        if (hotkeyIndex < 0 || hotkeyIndex >= 4 || weapon == null)
        {
            return;
        }

        EnsureHotkeySlots(hotkeyIndex + 1);
        ClearExistingHotkeyFor(weapon, weaponObject);
        hotkeyWeaponSlots[hotkeyIndex] = new WeaponSlot
        {
            weapon = weapon,
            weaponObject = weaponObject
        };
    }

    public void ClearHotkeyFor(Weapon weapon, GameObject weaponObject)
    {
        ClearExistingHotkeyFor(weapon, weaponObject);
    }

    public int GetHotkeyNumberFor(Weapon weapon, GameObject weaponObject)
    {
        if (weapon == null || hotkeyWeaponSlots == null)
        {
            return 0;
        }

        for (int i = 0; i < hotkeyWeaponSlots.Length; i++)
        {
            if (i >= 4)
            {
                break;
            }

            WeaponSlot slot = hotkeyWeaponSlots[i];
            if (slot == null || slot.weapon != weapon)
            {
                continue;
            }

            if (weaponObject == null || slot.weaponObject == null || slot.weaponObject == weaponObject)
            {
                return i + 1;
            }
        }

        return 0;
    }

    bool TryEquipHotkey(int hotkeyIndex)
    {
        if (hotkeyWeaponSlots == null || hotkeyIndex < 0 || hotkeyIndex >= 4 || hotkeyIndex >= hotkeyWeaponSlots.Length)
        {
            return false;
        }

        WeaponSlot slot = hotkeyWeaponSlots[hotkeyIndex];
        if (slot == null || slot.weapon == null)
        {
            return false;
        }

        if (IsWeaponAlreadyEquipped(slot.weapon, slot.weaponObject))
        {
            return true;
        }

        EquipWeapon(slot.weapon, slot.weaponObject);
        return true;
    }

    void EnsureHotkeySlots(int requiredLength)
    {
        if (hotkeyWeaponSlots != null && hotkeyWeaponSlots.Length >= requiredLength)
        {
            return;
        }

        int newLength = Mathf.Max(requiredLength, 4);
        WeaponSlot[] newSlots = new WeaponSlot[newLength];
        if (hotkeyWeaponSlots != null)
        {
            for (int i = 0; i < hotkeyWeaponSlots.Length; i++)
            {
                newSlots[i] = hotkeyWeaponSlots[i];
            }
        }

        hotkeyWeaponSlots = newSlots;
    }

    void ClearExistingHotkeyFor(Weapon weapon, GameObject weaponObject)
    {
        if (weapon == null || hotkeyWeaponSlots == null)
        {
            return;
        }

        for (int i = 0; i < hotkeyWeaponSlots.Length; i++)
        {
            if (i >= 4)
            {
                break;
            }

            WeaponSlot slot = hotkeyWeaponSlots[i];
            if (slot == null || slot.weapon != weapon)
            {
                continue;
            }

            if (weaponObject == null || slot.weaponObject == null || slot.weaponObject == weaponObject)
            {
                hotkeyWeaponSlots[i] = null;
            }
        }
    }

    int FindNextWeaponIndex(int direction)
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
        {
            return -1;
        }

        direction = direction >= 0 ? 1 : -1;
        int startIndex = Mathf.Clamp(currentWeaponIndex, 0, weaponSlots.Length - 1);

        for (int i = 1; i <= weaponSlots.Length; i++)
        {
            int index = (startIndex + direction * i) % weaponSlots.Length;
            if (index < 0)
            {
                index += weaponSlots.Length;
            }

            WeaponSlot slot = weaponSlots[index];
            if (slot != null && slot.weapon != null)
            {
                return index;
            }
        }

        return -1;
    }

    public void EquipWeapon(int slotIndex)
    {
        EquipWeapon(slotIndex, true);
    }

    void EquipWeapon(int slotIndex, bool playSwitchAnimation)
    {
        if (IsSwitching || IsActionBlockingSwitch())
        {
            return;
        }

        if (weaponSlots == null || slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            return;
        }

        WeaponSlot slot = weaponSlots[slotIndex];
        if (slot == null || slot.weapon == null)
        {
            return;
        }

        if (IsWeaponAlreadyEquipped(slot.weapon, slot.weaponObject))
        {
            currentWeaponIndex = slotIndex;
            return;
        }

        currentWeaponIndex = slotIndex;
        EquipWeapon(slot.weapon, slot.weaponObject, playSwitchAnimation);
    }

    public void EquipWeapon(Weapon weapon)
    {
        EquipWeapon(weapon, null, true);
    }

    public void EquipWeapon(Weapon weapon, GameObject weaponObject)
    {
        EquipWeapon(weapon, weaponObject, true);
    }

    public void UnequipWeapon()
    {
        if (equipRoutine != null)
        {
            StopCoroutine(equipRoutine);
            equipRoutine = null;
        }

        IsSwitching = false;
        ResetSwitchAimIKTiming();
        CurrentWeapon = null;
        ClearEquippedWeapon();
        if (existingWeaponObject != null)
        {
            existingWeaponObject.SetActive(false);
        }

        playerShoot?.SetWeapon(null, null, null);
        weaponAnimator?.SetAimInputEnabled(true, true);
        playerMovement?.SetEquippedWeapon(null);
    }

    void EquipWeapon(Weapon weapon, GameObject slotWeaponObject, bool playSwitchAnimation)
    {
        if (weapon == null || IsActionBlockingSwitch())
        {
            return;
        }

        if (IsWeaponAlreadyEquipped(weapon, slotWeaponObject))
        {
            return;
        }

        if (IsSwitching)
        {
            return;
        }

        if (equipRoutine != null)
        {
            StopCoroutine(equipRoutine);
            IsSwitching = false;
            ResetSwitchAimIKTiming();
            weaponAnimator?.StopSwitchState();
        }

        if (ShouldPlaySwitchAnimation(playSwitchAnimation))
        {
            equipRoutine = StartCoroutine(SwitchWeaponRoutine(weapon, slotWeaponObject));
            return;
        }

        ApplyWeapon(weapon, slotWeaponObject);
    }

    bool ShouldPlaySwitchAnimation(bool requested)
    {
        if (!requested || weaponAnimator == null)
        {
            return false;
        }

        if (!playSwitchAnimationOnlyWhenGrounded)
        {
            return true;
        }

        return playerMovement == null || playerMovement.isGrounded;
    }

    bool IsActionBlockingSwitch()
    {
        return (playerMovement != null && playerMovement.IsGuardBroken)
            || (blockController != null && blockController.IsBlocking)
            || (playerShoot != null && playerShoot.IsReloading)
            || (meleeController != null && meleeController.IsAttacking);
    }

    IEnumerator SwitchWeaponRoutine(Weapon weapon, GameObject slotWeaponObject)
    {
        IsSwitching = true;

        float switchDuration = weaponAnimator.PlaySwitchState(weapon);
        BeginSwitchAimIKTiming(switchDuration);
        float swapDelay = Mathf.Clamp(weaponSwapDelay, 0f, switchDuration);
        if (swapDelay > 0f)
        {
            yield return new WaitForSeconds(swapDelay);
        }

        ApplyWeapon(weapon, slotWeaponObject);

        float remainingDuration = switchDuration - swapDelay;
        if (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        weaponAnimator.StopSwitchState();
        IsSwitching = false;
        ResetSwitchAimIKTiming();
        equipRoutine = null;
    }

    void BeginSwitchAimIKTiming(float switchDuration)
    {
        float now = Time.time;
        float duration = Mathf.Max(0f, switchDuration);
        switchAimIKStartTime = now;
        switchAimIKEndTime = now + duration;
        switchAimIKResumeTime = Mathf.Max(now, switchAimIKEndTime - Mathf.Max(0f, switchAimIKResumeLeadTime));
    }

    void ResetSwitchAimIKTiming()
    {
        switchAimIKStartTime = 0f;
        switchAimIKResumeTime = 0f;
        switchAimIKEndTime = 0f;
    }

    float GetAimIKSwitchBlend()
    {
        if (!IsSwitching)
        {
            return 1f;
        }

        float now = Time.time;
        float fadeOutDuration = Mathf.Max(0f, switchAimIKFadeOutTime);
        if (fadeOutDuration > 0f && now < switchAimIKStartTime + fadeOutDuration)
        {
            float t = Mathf.Clamp01((now - switchAimIKStartTime) / fadeOutDuration);
            return Mathf.SmoothStep(1f, 0f, t);
        }

        if (now < switchAimIKResumeTime)
        {
            return 0f;
        }

        if (switchAimIKEndTime <= switchAimIKResumeTime)
        {
            return 1f;
        }

        float fadeIn = Mathf.InverseLerp(switchAimIKResumeTime, switchAimIKEndTime, now);
        return Mathf.SmoothStep(0f, 1f, fadeIn);
    }

    void ApplyWeapon(Weapon weapon, GameObject slotWeaponObject)
    {
        CurrentWeapon = weapon;

        GameObject weaponObject = ResolveWeaponObject(weapon, slotWeaponObject);
        CurrentEquippedWeapon = weaponObject != null ? weaponObject.GetComponent<EquippedWeapon>() : null;
        if (weaponObject != null)
        {
            SetupWeaponTransform(weaponObject, weapon, CurrentEquippedWeapon);
        }

        Transform muzzlePoint = CurrentEquippedWeapon != null && CurrentEquippedWeapon.muzzlePoint != null
            ? CurrentEquippedWeapon.muzzlePoint
            : playerShoot != null ? playerShoot.muzzleEffectPoint : null;
        Transform shellPoint = CurrentEquippedWeapon != null && CurrentEquippedWeapon.shellEjectPoint != null
            ? CurrentEquippedWeapon.shellEjectPoint
            : playerShoot != null ? playerShoot.shellEjectPoint : null;

        if (playerShoot != null)
        {
            playerShoot.SetWeapon(weapon, muzzlePoint, shellPoint);
        }

        if (weaponAnimator != null)
        {
            weaponAnimator.SetWeapon(weapon);
        }

        if (playerMovement != null)
        {
            playerMovement.SetEquippedWeapon(weapon);
        }
    }

    GameObject ResolveWeaponObject(Weapon weapon, GameObject slotWeaponObject)
    {
        ClearEquippedWeapon();

        equippedObjectWasInstantiated = false;

        if (weapon.weaponPrefab != null)
        {
            equippedWeaponObject = Instantiate(weapon.weaponPrefab);
            equippedObjectWasInstantiated = true;
            return equippedWeaponObject;
        }

        equippedWeaponObject = slotWeaponObject != null ? slotWeaponObject : existingWeaponObject;
        return equippedWeaponObject;
    }

    void ClearEquippedWeapon()
    {
        CurrentEquippedWeapon = null;

        if (equippedWeaponObject == null)
        {
            return;
        }

        if (equippedObjectWasInstantiated)
        {
            Destroy(equippedWeaponObject);
        }
        else
        {
            equippedWeaponObject.SetActive(false);
        }

        equippedWeaponObject = null;
        equippedObjectWasInstantiated = false;
    }

    void SetupWeaponTransform(GameObject weaponObject, Weapon weapon, EquippedWeapon equippedWeapon)
    {
        weaponObject.SetActive(true);

        Transform parent = weaponSocket != null ? weaponSocket : transform;
        weaponObject.transform.SetParent(parent, false);
    }

    bool IsWeaponAlreadyEquipped(Weapon weapon, GameObject slotWeaponObject)
    {
        return weapon != null && CurrentWeapon == weapon;
    }

}
