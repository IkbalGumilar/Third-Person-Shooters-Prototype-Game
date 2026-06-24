using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerAimIK : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public CameraControler cameraControler;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerWeaponEquip weaponEquip;
    public PlayerShoot playerShoot;
    public Transform aimLookTarget;
    public Transform rightHand;
    public Transform leftHand;
    public Transform rightClavicle;
    public Transform leftClavicle;
    public Transform spineAimBone;
    public Transform chestAimBone;
    public Transform upperChestAimBone;
    public Transform rightUpperArm;
    public Transform rightLowerArm;
    public Transform leftUpperArm;
    public Transform leftLowerArm;

    [Header("Activation")]
    public bool requireAimInput = true;
    public float weightSmoothSpeed = 14f;
    public float maxAimAngle = 85f;

    [Header("Look At")]
    public bool useLookAtIK = true;
    [Range(0f, 1f)] public float lookAtBodyWeight = 0.28f;
    [Range(0f, 1f)] public float lookAtHeadWeight = 0.65f;
    [Range(0f, 1f)] public float lookAtEyesWeight = 0f;
    [Range(0f, 1f)] public float lookAtClampWeight = 0.35f;

    [Header("Right Hand Aim")]
    public bool useRightHandRotationIK = true;
    public bool disableRightHandRotationWhenWeaponPivotAims = true;
    public bool useEquippedRightHandTarget = true;
    public bool allowRightHandRotationIK;
    [Range(0f, 1f)] public float rightHandRotationWeight = 1f;
    public Vector3 rightHandRotationOffset;

    [Header("Right Hand Position")]
    public bool useRightHandPositionIK;
    public bool syncRightHandTargetFromEquippedWeapon = true;
    [Range(0f, 1f)] public float rightHandPositionWeight = 0.15f;
    [Range(0f, 1f)] public float rightHandTargetRotationWeight = 1f;
    public Transform rightHandPositionTarget;

    [Header("Left Hand Grip")]
    public bool useLeftHandIK = true;
    public bool syncLeftHandTargetFromEquippedWeapon = true;
    public bool disableLeftHandIKForOneHandWeapons = true;
    public bool allowLeftHandRotationIK;
    [Range(0f, 1f)] public float leftHandPositionWeight = 0.75f;
    [Range(0f, 1f)] public float leftHandRotationWeight = 0.75f;
    public Transform leftHandTarget;

    [Header("Two Hand Grip Lock")]
    public bool lockLeftHandToTwoHandGrip = true;
    [Range(0f, 1f)] public float twoHandGripLockWeight = 1f;
    [Range(1, 4)] public int twoHandGripLockIterations = 2;
    [Range(0f, 1f)] public float twoHandGripClavicleWeight = 0.25f;
    [Range(0f, 1f)] public float twoHandGripUpperArmWeight = 0.75f;
    [Range(0f, 1f)] public float twoHandGripLowerArmWeight = 1f;
    [Range(0f, 1f)] public float twoHandGripHandRotationWeight = 0f;

    [Header("Upper Body Aim")]
    public bool useUpperBodyAim = true;
    [Range(0f, 1f)] public float spineAimWeight = 0.1f;
    [Range(0f, 1f)] public float chestAimWeight = 0.16f;
    [Range(0f, 1f)] public float upperChestAimWeight = 0.22f;
    public float maxUpperBodyAimAngle = 28f;
    public Vector3 upperBodyAimOffset;

    [Header("Clavicle Aim")]
    public bool useClavicleAim = true;
    [Range(0f, 1f)] public float rightClavicleAimWeight = 0.45f;
    [Range(0f, 1f)] public float leftClavicleAimWeight = 0.3f;
    public float maxClavicleAimAngle = 35f;
    public Vector3 rightClavicleAimOffset;
    public Vector3 leftClavicleAimOffset;

    [Header("Arm Aim")]
    public bool useArmAim = true;
    [Range(0f, 1f)] public float rightUpperArmAimWeight = 0.35f;
    [Range(0f, 1f)] public float rightLowerArmAimWeight = 0.25f;
    [Range(0f, 1f)] public float rightHandAimWeight = 0.18f;
    [Range(0f, 1f)] public float leftUpperArmAimWeight = 0.22f;
    [Range(0f, 1f)] public float leftLowerArmAimWeight = 0.14f;
    [Range(0f, 1f)] public float leftHandAimWeight = 0.16f;
    public float maxArmAimAngle = 28f;
    public Vector3 rightArmAimOffset;
    public Vector3 leftArmAimOffset;

    [Header("Body Recoil")]
    public bool useBodyRecoil = true;
    public float fallbackBodyRecoilSnappiness = 18f;
    public float fallbackBodyRecoilReturnSpeed = 10f;
    public float fallbackMaxBodyRecoilAngle = 8f;

    private float currentWeight;
    private float suppressAimUntil;
    private float suppressPoseUntil;
    private bool externalPoseOverride;
    private Vector2 targetBodyRecoil;
    private Vector2 currentBodyRecoil;
    private Weapon recoilWeapon;
    private KontrolPemain kontrolPemain;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        ResolveReferences();
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

    void Update()
    {
        ResolveRuntimeTargets();

        float targetWeight = GetTargetWeight() * GetSwitchAimIKBlend();
        currentWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            Mathf.Max(0.01f, weightSmoothSpeed) * Time.deltaTime
        );

        UpdateBodyRecoil();
    }

    public void SuppressAim(float duration)
    {
        suppressAimUntil = Mathf.Max(suppressAimUntil, Time.time + Mathf.Max(0f, duration));
        currentWeight = 0f;
    }

    public void SetExternalPoseOverride(bool active)
    {
        externalPoseOverride = active;
        if (active)
        {
            currentWeight = 0f;
        }
    }

    public void SuppressPose(float duration)
    {
        suppressPoseUntil = Mathf.Max(suppressPoseUntil, Time.time + Mathf.Max(0f, duration));
        currentWeight = 0f;
    }

    void LateUpdate()
    {
        if (IsPoseOverrideActive())
        {
            return;
        }

        ApplyUpperBodyAim();
        ApplyClavicleAim();
        ApplyArmAim();
        ApplyBodyRecoilPose();
        ApplyTwoHandLeftGripLock();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || aimLookTarget == null)
        {
            return;
        }

        if (currentWeight <= 0.001f)
        {
            ResetIKWeights();
            return;
        }

        ApplyLookAtIK();
        ApplyRightHandIK();
        ApplyLeftHandIK();
    }

    void ResolveReferences()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        cameraControler = cameraControler != null ? cameraControler : GetComponent<CameraControler>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        weaponEquip = weaponEquip != null ? weaponEquip : GetComponent<PlayerWeaponEquip>();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();

        if (aimLookTarget == null && cameraControler != null)
        {
            aimLookTarget = cameraControler.aimLookTarget;
        }

        if (rightHand == null && animator != null && animator.isHuman)
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (leftHand == null && animator != null && animator.isHuman)
        {
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        if (rightClavicle == null && animator != null && animator.isHuman)
        {
            rightClavicle = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        }

        if (leftClavicle == null && animator != null && animator.isHuman)
        {
            leftClavicle = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        }

        if (spineAimBone == null && animator != null && animator.isHuman)
        {
            spineAimBone = animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        if (chestAimBone == null && animator != null && animator.isHuman)
        {
            chestAimBone = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        if (upperChestAimBone == null && animator != null && animator.isHuman)
        {
            upperChestAimBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }

        if (rightUpperArm == null && animator != null && animator.isHuman)
        {
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        if (rightLowerArm == null && animator != null && animator.isHuman)
        {
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        if (leftUpperArm == null && animator != null && animator.isHuman)
        {
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        }

        if (leftLowerArm == null && animator != null && animator.isHuman)
        {
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }
    }

    void ResolveRuntimeTargets()
    {
        if (aimLookTarget == null && cameraControler != null)
        {
            aimLookTarget = cameraControler.aimLookTarget;
        }

        if (syncLeftHandTargetFromEquippedWeapon)
        {
            EquippedWeapon equippedWeapon = weaponEquip != null ? weaponEquip.CurrentEquippedWeapon : null;
            leftHandTarget = equippedWeapon != null ? equippedWeapon.leftHandIKTarget : null;
        }

        if (syncRightHandTargetFromEquippedWeapon)
        {
            EquippedWeapon equippedWeapon = weaponEquip != null ? weaponEquip.CurrentEquippedWeapon : null;
            rightHandPositionTarget = equippedWeapon != null ? equippedWeapon.rightHandIKTarget : null;
        }
    }

    float GetTargetWeight()
    {
        if (IsPoseOverrideActive())
        {
            return 0f;
        }

        if (Time.time < suppressAimUntil)
        {
            return 0f;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon != null && !weapon.useAimIK)
        {
            return 0f;
        }

        float aimWeight = 1f;
        if (requireAimInput)
        {
            aimWeight = weaponAnimator != null
                ? weaponAnimator.AimWeight
                : IsAimPressed() ? 1f : 0f;
        }

        if (weapon != null)
        {
            aimWeight *= Mathf.Clamp01(weapon.aimIKWeight);
        }

        if (!IsAimAngleAllowed())
        {
            return 0f;
        }

        return Mathf.Clamp01(aimWeight);
    }

    bool IsPoseOverrideActive()
    {
        return externalPoseOverride || Time.time < suppressPoseUntil;
    }

    bool IsAimPressed()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.Aim.IsPressed();
    }

    float GetSwitchAimIKBlend()
    {
        return weaponEquip != null ? Mathf.Clamp01(weaponEquip.AimIKSwitchBlend) : 1f;
    }

    bool IsAimAngleAllowed()
    {
        if (aimLookTarget == null || rightHand == null)
        {
            return true;
        }

        Vector3 direction = aimLookTarget.position - rightHand.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, direction);
        return angle <= Mathf.Max(1f, maxAimAngle);
    }

    void ApplyLookAtIK()
    {
        if (!useLookAtIK)
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        float bodyWeight = weapon != null ? weapon.aimIKBodyWeight : lookAtBodyWeight;
        float headWeight = weapon != null ? weapon.aimIKHeadWeight : lookAtHeadWeight;

        animator.SetLookAtWeight(
            currentWeight,
            Mathf.Clamp01(bodyWeight),
            Mathf.Clamp01(headWeight),
            lookAtEyesWeight,
            lookAtClampWeight
        );
        animator.SetLookAtPosition(aimLookTarget.position);
    }

    void ApplyRightHandIK()
    {
        if (rightHand == null)
        {
            return;
        }

        if (ShouldSkipRightHandRotationIK())
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        bool hasEquippedRightHandTarget = useEquippedRightHandTarget && rightHandPositionTarget != null;
        if (useRightHandPositionIK && rightHandPositionTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, currentWeight * rightHandPositionWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandPositionTarget.position);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        }

        if (!allowRightHandRotationIK)
        {
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        if (hasEquippedRightHandTarget && ShouldSkipRightHandRotationIK())
        {
            Weapon targetWeapon = weaponEquip != null ? weaponEquip.CurrentWeapon : null;
            float targetHandWeight = targetWeapon != null ? targetWeapon.aimIKRightHandWeight : rightHandRotationWeight;
            animator.SetIKRotationWeight(
                AvatarIKGoal.RightHand,
                currentWeight * Mathf.Clamp01(targetHandWeight) * rightHandTargetRotationWeight
            );
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandPositionTarget.rotation);
            return;
        }

        if (!useRightHandRotationIK || ShouldSkipRightHandRotationIK())
        {
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        if (!TryGetRightHandAimRotation(out Quaternion targetRotation))
        {
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        float weaponHandWeight = weapon != null ? weapon.aimIKRightHandWeight : rightHandRotationWeight;
        Quaternion weaponOffset = GetRightHandAimOffsetRotation(weapon);

        targetRotation *= weaponOffset;
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, currentWeight * Mathf.Clamp01(weaponHandWeight));
        animator.SetIKRotation(AvatarIKGoal.RightHand, targetRotation);
    }

    bool ShouldSkipRightHandRotationIK()
    {
        return disableRightHandRotationWhenWeaponPivotAims
            && weaponEquip != null
            && weaponEquip.CurrentWeapon != null;
    }

    Quaternion GetRightHandAimOffsetRotation(Weapon weapon)
    {
        EquippedWeapon equippedWeapon = weaponEquip != null ? weaponEquip.CurrentEquippedWeapon : null;
        if (equippedWeapon != null && equippedWeapon.rightHandAimRotationOffsetTransform != null)
        {
            return equippedWeapon.rightHandAimRotationOffsetTransform.localRotation;
        }

        return Quaternion.Euler(weapon != null ? weapon.aimIKRightHandRotationOffset : rightHandRotationOffset);
    }

    bool TryGetRightHandAimRotation(out Quaternion targetRotation)
    {
        targetRotation = rightHand.rotation;

        Vector3 targetPosition = aimLookTarget.position;
        Transform muzzlePoint = GetCurrentMuzzlePoint();
        Transform sourceTransform = muzzlePoint != null ? muzzlePoint : rightHand;
        Vector3 direction = targetPosition - sourceTransform.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Quaternion currentRotation = rightHand.rotation;
        Vector3 currentAimForward = muzzlePoint != null ? muzzlePoint.forward : rightHand.forward;
        if (currentAimForward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Quaternion aimDelta = Quaternion.FromToRotation(currentAimForward.normalized, direction.normalized);
        targetRotation = aimDelta * currentRotation;
        return true;
    }

    Transform GetCurrentMuzzlePoint()
    {
        if (playerShoot != null && playerShoot.muzzleEffectPoint != null)
        {
            return playerShoot.muzzleEffectPoint;
        }

        EquippedWeapon equippedWeapon = weaponEquip != null ? weaponEquip.CurrentEquippedWeapon : null;
        return equippedWeapon != null ? equippedWeapon.muzzlePoint : null;
    }

    void ApplyUpperBodyAim()
    {
        if (!useUpperBodyAim || currentWeight <= 0.001f || aimLookTarget == null)
        {
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon != null && !weapon.useAimIK)
        {
            return;
        }

        Vector3 sourceDirection = GetUpperBodySourceDirection();
        ApplyUpperBodyAimRotation(spineAimBone, sourceDirection, currentWeight * spineAimWeight);
        ApplyUpperBodyAimRotation(chestAimBone, sourceDirection, currentWeight * chestAimWeight);
        ApplyUpperBodyAimRotation(upperChestAimBone, sourceDirection, currentWeight * upperChestAimWeight);
    }

    Vector3 GetUpperBodySourceDirection()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        return direction;
    }

    void ApplyUpperBodyAimRotation(Transform bone, Vector3 sourceDirection, float weight)
    {
        if (bone == null || weight <= 0.001f)
        {
            return;
        }

        Vector3 targetDirection = aimLookTarget.position - bone.position;
        if (targetDirection.sqrMagnitude < 0.0001f || sourceDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Vector3.Angle(sourceDirection, targetDirection);
        float limitedWeight = angle > 0.001f
            ? Mathf.Clamp01(Mathf.Max(1f, maxUpperBodyAimAngle) / angle)
            : 1f;

        Quaternion aimDelta = Quaternion.Slerp(
            Quaternion.identity,
            Quaternion.FromToRotation(sourceDirection.normalized, targetDirection.normalized),
            limitedWeight
        );

        Quaternion targetRotation = aimDelta * bone.rotation * Quaternion.Euler(upperBodyAimOffset);
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    void ApplyClavicleAim()
    {
        if (!useClavicleAim || currentWeight <= 0.001f || aimLookTarget == null)
        {
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon != null && !weapon.useAimIK)
        {
            return;
        }

        bool oneHand = weapon == null || weapon.holdType == WeaponHoldType.OneHand;
        ApplyClavicleAimRotation(rightClavicle, rightHand, currentWeight * rightClavicleAimWeight, rightClavicleAimOffset);

        if (!oneHand)
        {
            ApplyClavicleAimRotation(leftClavicle, leftHand, currentWeight * leftClavicleAimWeight, leftClavicleAimOffset);
        }
    }

    void ApplyClavicleAimRotation(Transform clavicle, Transform hand, float weight, Vector3 offset)
    {
        if (clavicle == null || weight <= 0.001f)
        {
            return;
        }

        Vector3 targetDirection = aimLookTarget.position - clavicle.position;
        Vector3 currentArmDirection = hand != null
            ? hand.position - clavicle.position
            : clavicle.forward;
        if (targetDirection.sqrMagnitude < 0.0001f || currentArmDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Vector3.Angle(currentArmDirection, targetDirection);
        float limitedWeight = angle > 0.001f
            ? Mathf.Clamp01(Mathf.Max(1f, maxClavicleAimAngle) / angle)
            : 1f;

        Quaternion aimDelta = Quaternion.Slerp(
            Quaternion.identity,
            Quaternion.FromToRotation(currentArmDirection.normalized, targetDirection.normalized),
            limitedWeight
        );

        Quaternion offsetRotation = Quaternion.Euler(offset);
        Quaternion targetRotation = aimDelta * clavicle.rotation * offsetRotation;
        clavicle.rotation = Quaternion.Slerp(clavicle.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    void ApplyArmAim()
    {
        if (!useArmAim || currentWeight <= 0.001f || aimLookTarget == null)
        {
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon != null && !weapon.useAimIK)
        {
            return;
        }

        bool oneHand = weapon == null || weapon.holdType == WeaponHoldType.OneHand;
        ApplyLimbAimRotation(rightUpperArm, GetArmEnd(rightLowerArm, rightHand), currentWeight * rightUpperArmAimWeight, rightArmAimOffset);
        ApplyLimbAimRotation(rightLowerArm, rightHand, currentWeight * rightLowerArmAimWeight, rightArmAimOffset);
        ApplyRightHandAimRotation(currentWeight * rightHandAimWeight);

        if (!oneHand)
        {
            ApplyLimbAimRotation(leftUpperArm, GetArmEnd(leftLowerArm, leftHand), currentWeight * leftUpperArmAimWeight, leftArmAimOffset);
            ApplyLimbAimRotation(leftLowerArm, leftHand, currentWeight * leftLowerArmAimWeight, leftArmAimOffset);
            ApplyLeftHandAimRotation(currentWeight * leftHandAimWeight);
        }
    }

    Transform GetArmEnd(Transform preferred, Transform fallback)
    {
        return preferred != null ? preferred : fallback;
    }

    void ApplyLimbAimRotation(Transform bone, Transform end, float weight, Vector3 offset)
    {
        if (bone == null || weight <= 0.001f)
        {
            return;
        }

        Vector3 targetDirection = aimLookTarget.position - bone.position;
        Vector3 limbDirection = end != null
            ? end.position - bone.position
            : bone.forward;
        if (targetDirection.sqrMagnitude < 0.0001f || limbDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Vector3.Angle(limbDirection, targetDirection);
        float limitedWeight = angle > 0.001f
            ? Mathf.Clamp01(Mathf.Max(1f, maxArmAimAngle) / angle)
            : 1f;

        Quaternion aimDelta = Quaternion.Slerp(
            Quaternion.identity,
            Quaternion.FromToRotation(limbDirection.normalized, targetDirection.normalized),
            limitedWeight
        );

        Quaternion targetRotation = aimDelta * bone.rotation * Quaternion.Euler(offset);
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    void ApplyRightHandAimRotation(float weight)
    {
        if (rightHand == null || weight <= 0.001f || !TryGetRightHandAimRotation(out Quaternion targetRotation))
        {
            return;
        }

        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        Quaternion weaponOffset = GetRightHandAimOffsetRotation(weapon);
        rightHand.rotation = Quaternion.Slerp(rightHand.rotation, targetRotation * weaponOffset, Mathf.Clamp01(weight));
    }

    void ApplyLeftHandAimRotation(float weight)
    {
        if (leftHand == null || weight <= 0.001f)
        {
            return;
        }

        Transform reference = leftHandTarget != null ? leftHandTarget : GetCurrentMuzzlePoint();
        Vector3 targetDirection = aimLookTarget.position - leftHand.position;
        Vector3 currentDirection = reference != null
            ? reference.forward
            : leftHand.forward;
        if (targetDirection.sqrMagnitude < 0.0001f || currentDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion aimDelta = Quaternion.FromToRotation(currentDirection.normalized, targetDirection.normalized);
        Quaternion targetRotation = aimDelta * leftHand.rotation * Quaternion.Euler(leftArmAimOffset);
        leftHand.rotation = Quaternion.Slerp(leftHand.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    void ApplyTwoHandLeftGripLock()
    {
        Weapon weapon = GetCurrentWeapon();
        if (!lockLeftHandToTwoHandGrip
            || weapon == null
            || weapon.holdType != WeaponHoldType.TwoHand
            || leftHandTarget == null
            || leftHand == null)
        {
            return;
        }

        float lockWeight = Mathf.Clamp01(twoHandGripLockWeight) * GetSwitchAimIKBlend();
        if (lockWeight <= 0.001f)
        {
            return;
        }

        int iterations = Mathf.Clamp(twoHandGripLockIterations, 1, 4);
        for (int i = 0; i < iterations; i++)
        {
            RotateBoneTowardGrip(leftLowerArm, Mathf.Clamp01(twoHandGripLowerArmWeight) * lockWeight);
            RotateBoneTowardGrip(leftUpperArm, Mathf.Clamp01(twoHandGripUpperArmWeight) * lockWeight);
            RotateBoneTowardGrip(leftClavicle, Mathf.Clamp01(twoHandGripClavicleWeight) * lockWeight);
        }

        if (twoHandGripHandRotationWeight > 0.001f)
        {
            leftHand.rotation = Quaternion.Slerp(
                leftHand.rotation,
                leftHandTarget.rotation,
                Mathf.Clamp01(twoHandGripHandRotationWeight) * lockWeight
            );
        }
    }

    void RotateBoneTowardGrip(Transform bone, float weight)
    {
        if (bone == null || leftHand == null || leftHandTarget == null || weight <= 0.001f)
        {
            return;
        }

        Vector3 currentDirection = leftHand.position - bone.position;
        Vector3 targetDirection = leftHandTarget.position - bone.position;
        if (currentDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.FromToRotation(currentDirection.normalized, targetDirection.normalized) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    Weapon GetCurrentWeapon()
    {
        if (weaponEquip != null && weaponEquip.CurrentWeapon != null)
        {
            return weaponEquip.CurrentWeapon;
        }

        return playerShoot != null ? playerShoot.currentWeapon : null;
    }

    public void ApplyWeaponBodyRecoil(Weapon weapon)
    {
        if (!useBodyRecoil || weapon == null || !weapon.useBodyRecoil)
        {
            return;
        }

        recoilWeapon = weapon;
        float sideRange = Mathf.Abs(weapon.bodyRecoilPerShot.x);
        float side = Random.Range(-sideRange, sideRange);
        float up = weapon.bodyRecoilPerShot.y;
        float maxAngle = weapon.maxBodyRecoilAngle > 0f ? weapon.maxBodyRecoilAngle : fallbackMaxBodyRecoilAngle;
        Vector2 recoilImpulse = new Vector2(side, up);

        targetBodyRecoil += recoilImpulse;
        targetBodyRecoil = Vector2.ClampMagnitude(targetBodyRecoil, Mathf.Max(0.01f, maxAngle));

        currentBodyRecoil += recoilImpulse * Mathf.Clamp01(weapon.bodyRecoilImmediateResponse);
        currentBodyRecoil = Vector2.ClampMagnitude(currentBodyRecoil, Mathf.Max(0.01f, maxAngle));
    }

    void UpdateBodyRecoil()
    {
        Weapon weapon = recoilWeapon != null ? recoilWeapon : weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        float snappiness = weapon != null && weapon.bodyRecoilSnappiness > 0f
            ? weapon.bodyRecoilSnappiness
            : fallbackBodyRecoilSnappiness;
        float returnSpeed = weapon != null && weapon.bodyRecoilReturnSpeed > 0f
            ? weapon.bodyRecoilReturnSpeed
            : fallbackBodyRecoilReturnSpeed;

        targetBodyRecoil = Vector2.Lerp(targetBodyRecoil, Vector2.zero, GetFrameLerp(returnSpeed));
        currentBodyRecoil = Vector2.Lerp(currentBodyRecoil, targetBodyRecoil, GetFrameLerp(snappiness));
    }

    void ApplyBodyRecoilPose()
    {
        if (!useBodyRecoil || currentBodyRecoil.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Weapon weapon = recoilWeapon != null ? recoilWeapon : weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        if (weapon != null && !weapon.useBodyRecoil)
        {
            return;
        }

        bool oneHand = weapon == null || weapon.holdType == WeaponHoldType.OneHand;
        float upperBodyScale = GetUpperBodyRecoilScale(weapon, oneHand);
        ApplyBodyRecoilRotation(spineAimBone, GetRecoilWeight(weapon, RecoilBone.Spine) * upperBodyScale);
        ApplyBodyRecoilRotation(chestAimBone, GetRecoilWeight(weapon, RecoilBone.Chest) * upperBodyScale);
        ApplyBodyRecoilRotation(upperChestAimBone, GetRecoilWeight(weapon, RecoilBone.UpperChest) * upperBodyScale);

        if (oneHand)
        {
            ApplyOneHandArmRecoil(weapon);
        }
        else
        {
            ApplyBodyRecoilRotation(rightClavicle, GetRecoilWeight(weapon, RecoilBone.RightClavicle));
            ApplyBodyRecoilRotation(rightUpperArm, GetRecoilWeight(weapon, RecoilBone.RightUpperArm));
            ApplyBodyRecoilRotation(rightLowerArm, GetRecoilWeight(weapon, RecoilBone.RightLowerArm));
        }

        if (!oneHand)
        {
            ApplyBodyRecoilRotation(leftClavicle, GetRecoilWeight(weapon, RecoilBone.LeftClavicle));
            ApplyBodyRecoilRotation(leftUpperArm, GetRecoilWeight(weapon, RecoilBone.LeftUpperArm));
            ApplyBodyRecoilRotation(leftLowerArm, GetRecoilWeight(weapon, RecoilBone.LeftLowerArm));
        }
    }

    void ApplyBodyRecoilRotation(Transform bone, float weight)
    {
        if (bone == null || weight <= 0.001f)
        {
            return;
        }

        Transform reference = cameraControler != null && cameraControler.cameraTransform != null
            ? cameraControler.cameraTransform
            : Camera.main != null ? Camera.main.transform : transform;
        Vector3 pitchAxis = reference.right;
        Vector3 yawAxis = transform.up;
        Quaternion pitchKick = Quaternion.AngleAxis(-currentBodyRecoil.y * weight, pitchAxis);
        Quaternion yawKick = Quaternion.AngleAxis(currentBodyRecoil.x * weight, yawAxis);
        bone.rotation = yawKick * pitchKick * bone.rotation;
    }

    void ApplyOneHandArmRecoil(Weapon weapon)
    {
        Transform reference = cameraControler != null && cameraControler.cameraTransform != null
            ? cameraControler.cameraTransform
            : Camera.main != null ? Camera.main.transform : transform;
        Vector3 liftAxis = reference.right.sqrMagnitude > 0.0001f ? reference.right : transform.right;
        Vector3 sideAxis = transform.up;

        float shoulderWeight = GetOneHandScaledRecoilWeight(weapon, true, RecoilBone.RightClavicle);
        float upperArmWeight = GetOneHandScaledRecoilWeight(weapon, true, RecoilBone.RightUpperArm);
        float elbowWeight = GetOneHandScaledRecoilWeight(weapon, true, RecoilBone.RightLowerArm);

        ApplyAxisRecoilRotation(rightClavicle, liftAxis, sideAxis, shoulderWeight, 0.65f, 0.25f);
        ApplyAxisRecoilRotation(rightUpperArm, liftAxis, sideAxis, upperArmWeight, 0.35f, 0.15f);
        ApplyAxisRecoilRotation(rightLowerArm, liftAxis, sideAxis, elbowWeight, 1f, 0.1f);
    }

    void ApplyAxisRecoilRotation(Transform bone, Vector3 liftAxis, Vector3 sideAxis, float weight, float liftScale, float sideScale)
    {
        if (bone == null || weight <= 0.001f)
        {
            return;
        }

        Quaternion liftKick = Quaternion.AngleAxis(-currentBodyRecoil.y * weight * liftScale, liftAxis);
        Quaternion sideKick = Quaternion.AngleAxis(currentBodyRecoil.x * weight * sideScale, sideAxis);
        bone.rotation = sideKick * liftKick * bone.rotation;
    }

    float GetFrameLerp(float speed)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Time.deltaTime);
    }

    float GetUpperBodyRecoilScale(Weapon weapon, bool oneHand)
    {
        if (weapon == null)
        {
            return oneHand ? 0f : 1f;
        }

        return oneHand
            ? Mathf.Clamp01(weapon.oneHandUpperBodyRecoilScale)
            : Mathf.Clamp01(weapon.twoHandUpperBodyRecoilScale);
    }

    float GetOneHandScaledRecoilWeight(Weapon weapon, bool oneHand, RecoilBone bone)
    {
        float weight = GetRecoilWeight(weapon, bone);
        if (!oneHand || weapon == null)
        {
            return weight;
        }

        switch (bone)
        {
            case RecoilBone.RightClavicle:
                return weight * Mathf.Max(0f, weapon.oneHandShoulderRecoilScale);
            case RecoilBone.RightUpperArm:
                return weight * Mathf.Max(0f, weapon.oneHandUpperArmRecoilScale);
            case RecoilBone.RightLowerArm:
                return weight * Mathf.Max(0f, weapon.oneHandElbowRecoilScale);
            default:
                return weight;
        }
    }

    float GetRecoilWeight(Weapon weapon, RecoilBone bone)
    {
        if (weapon == null)
        {
            return bone == RecoilBone.RightClavicle ? 0.75f : 0.25f;
        }

        switch (bone)
        {
            case RecoilBone.Spine: return weapon.bodyRecoilSpineWeight;
            case RecoilBone.Chest: return weapon.bodyRecoilChestWeight;
            case RecoilBone.UpperChest: return weapon.bodyRecoilUpperChestWeight;
            case RecoilBone.RightClavicle: return weapon.bodyRecoilRightClavicleWeight;
            case RecoilBone.LeftClavicle: return weapon.bodyRecoilLeftClavicleWeight;
            case RecoilBone.RightUpperArm: return weapon.bodyRecoilRightUpperArmWeight;
            case RecoilBone.RightLowerArm: return weapon.bodyRecoilRightLowerArmWeight;
            case RecoilBone.LeftUpperArm: return weapon.bodyRecoilLeftUpperArmWeight;
            case RecoilBone.LeftLowerArm: return weapon.bodyRecoilLeftLowerArmWeight;
            default: return 0f;
        }
    }

    enum RecoilBone
    {
        Spine,
        Chest,
        UpperChest,
        RightClavicle,
        LeftClavicle,
        RightUpperArm,
        RightLowerArm,
        LeftUpperArm,
        LeftLowerArm
    }

    void ApplyLeftHandIK()
    {
        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        if (!useLeftHandIK
            || leftHandTarget == null
            || (disableLeftHandIKForOneHandWeapons && weapon != null && weapon.holdType == WeaponHoldType.OneHand))
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        float weaponLeftWeight = weapon != null ? weapon.aimIKLeftHandWeight : 1f;
        float finalPositionWeight = currentWeight * leftHandPositionWeight * Mathf.Clamp01(weaponLeftWeight);
        float finalRotationWeight = allowLeftHandRotationIK
            ? currentWeight * leftHandRotationWeight * Mathf.Clamp01(weaponLeftWeight)
            : 0f;

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, finalPositionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, finalRotationWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
        if (allowLeftHandRotationIK)
        {
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
        }
    }

    void ResetIKWeights()
    {
        animator.SetLookAtWeight(0f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
    }
}
