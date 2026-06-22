using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraControler : MonoBehaviour
{
    public bool allowLookInput = true;
    public bool requireLockedHiddenCursorForLook = true;
    public float lookSensitivity = 8f;
    public float sensitivityMultiplier = 1f;
    [HideInInspector] public float scopeSensitivityMultiplier = 1f;
    public PlayerWeaponAnimator weaponAnimator;
    public Transform cameraTransform;
    public Camera aimCamera;
    public float minVerticalAngle = -10f;
    public float maxVerticalAngle = 10f;
    public Transform aimTarget;
    public Transform aimLookTarget;
    public PlayerMovement playerMovement;
    public bool moveAimTargetWithStance = true;
    public float stanceAimHeightMultiplier = 1f;
    public Transform upperBodyTarget;
    public Transform headTarget;
    public bool applyManualUpperBodyAim = true;
    public bool applyManualHeadAim = true;
    public float minUpperBodyYaw = -60f;
    public float maxUpperBodyYaw = 60f;
    public float upperBodyYawWeight = 0.5f;
    public float upperBodyPitchWeight = 0.7f;
    public float headLookWeight = 0.8f;
    public float maxHeadLookAngle = 70f;
    public bool rotatePlayerWithCamera = true;
    public bool rotatePlayerOnlyWhileAiming = true;
    public float playerRotationSmoothTime = 0.08f;
    public float yawSmoothTime = 0.06f;
    public float pitchSmoothTime = 0.04f;
    public bool stickAimToCameraRay = true;
    public bool useAimTargetAsRayOrigin;
    public bool useAimTargetForwardForRay;
    public LayerMask aimRaycastMask = ~0;
    public float aimRaycastMaxDistance = 200f;
    public float aimNoHitDistance = 200f;
    public float aimSurfaceOffset = 0.02f;
    public float aimStickSmoothSpeed = 30f;
    public bool autoScaleAim = true;
    public bool keepAimScreenSize = true;
    [Range(0.001f, 0.2f)] public float aimScreenHeightPercent = 0.035f;
    public float aimScaleMaxDistance = 200f;
    public float aimMinLocalScale = 0.04f;
    public float aimMaxLocalScale = 10f;
    public float aimScaleSmoothSpeed = 20f;
    public float aimScaleMultiplier = 1f;
    public Renderer aimRenderer;
    public Color aimNormalColor = Color.white;
    public Color aimEnemyColor = Color.red;
    public float aimColorSmoothSpeed = 20f;
    [Header("Cinemachine")]
    public bool useCinemachine = true;
    [Tooltip("Assign the Cinemachine Camera created in the scene. This script never creates or configures Cinemachine objects.")]
    public CinemachineCamera cinemachineCamera;

    private KontrolPemain kontrolPemain;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly RaycastHit[] AimRaycastHits = new RaycastHit[32];
    private float targetYaw;
    private float targetPitch;
    private float yaw;
    private float pitch;
    private float yawVelocity;
    private float pitchVelocity;
    private float playerYawVelocity;
    private Quaternion upperBodyStartLocalRotation;
    private Animator animator;
    private Vector3 defaultAimLocalScale = Vector3.one;
    private bool hasDefaultAimLocalScale;
    private Vector2 aimRecoilOffset;
    private MaterialPropertyBlock aimPropertyBlock;
    private Color currentAimColor;
    private Vector3 defaultAimTargetLocalPosition;
    private bool hasDefaultAimTargetLocalPosition;

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
        kontrolPemain = null;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (aimCamera == null)
        {
            aimCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
        }

        if (aimTarget == null)
        {
            aimTarget = transform;
        }

        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        CacheDefaultAimTargetLocalPosition();

        if (aimLookTarget == null)
        {
            aimLookTarget = FindAimLookTarget();
        }

        CacheDefaultAimScale();
        CacheAimRenderer();
        currentAimColor = aimNormalColor;
        ApplyAimColor(currentAimColor);

        SetupCinemachine();

        if (cameraTransform != null && (!useCinemachine || cinemachineCamera == null))
        {
            cameraTransform.SetParent(transform, true);
        }

        if (upperBodyTarget == null)
        {
            upperBodyTarget = FindUpperBodyTarget();
        }

        if (upperBodyTarget != null)
        {
            upperBodyStartLocalRotation = upperBodyTarget.localRotation;
        }

        if (headTarget == null)
        {
            headTarget = FindHeadTarget();
        }

        Vector3 startEuler = aimTarget.eulerAngles;
        targetYaw = startEuler.y;
        targetPitch = Mathf.DeltaAngle(0f, startEuler.x);
        yaw = targetYaw;
        pitch = targetPitch;
    }

    void Update()
    {
        if (CanReadLookInput())
        {
            Look();
        }
    }

    void LateUpdate()
    {
        UpdateAimTargetStanceHeight();
        RotatePlayerWithCamera();
        ApplyAimTargetRotation();
        SyncCinemachineTargets();
        UpdateAimLookTargetFromCameraRay();
        if (applyManualUpperBodyAim)
        {
            ApplyUpperBodyAim();
        }
        KeepCameraLookingAtAim();
        if (applyManualHeadAim)
        {
            ApplyHeadAim();
        }
    }

    void Look()
    {
        Vector2 lookInput = kontrolPemain != null
            ? kontrolPemain.Pemain.Tampak.ReadValue<Vector2>()
            : Vector2.zero;
        float currentSensitivity = lookSensitivity * sensitivityMultiplier * scopeSensitivityMultiplier;
        targetYaw += lookInput.x * currentSensitivity * Time.deltaTime;
        targetPitch -= lookInput.y * currentSensitivity * Time.deltaTime;
        targetPitch = Mathf.Clamp(targetPitch, minVerticalAngle, maxVerticalAngle);

        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmoothTime);
        pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, pitchSmoothTime);

        ApplyAimTargetRotation();
    }

    void ApplyAimTargetRotation()
    {
        if (aimTarget != null)
        {
            aimTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    void CacheDefaultAimTargetLocalPosition()
    {
        if (hasDefaultAimTargetLocalPosition || aimTarget == null)
        {
            return;
        }

        defaultAimTargetLocalPosition = aimTarget.localPosition;
        hasDefaultAimTargetLocalPosition = true;
    }

    void UpdateAimTargetStanceHeight()
    {
        if (!moveAimTargetWithStance || aimTarget == null || playerMovement == null)
        {
            return;
        }

        CacheDefaultAimTargetLocalPosition();
        float heightDrop = Mathf.Max(0f, playerMovement.StandingControllerHeight - playerMovement.CurrentControllerHeight);
        Vector3 targetPosition = defaultAimTargetLocalPosition;
        targetPosition.y -= heightDrop * Mathf.Max(0f, stanceAimHeightMultiplier);
        aimTarget.localPosition = targetPosition;
    }

    bool CanReadLookInput()
    {
        if (!allowLookInput)
        {
            return false;
        }

        if (!requireLockedHiddenCursorForLook)
        {
            return true;
        }

        return Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
    }

    void ApplyUpperBodyAim()
    {
        if (upperBodyTarget == null)
        {
            return;
        }

        float relativeYaw = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
        float clampedYaw = Mathf.Clamp(relativeYaw, minUpperBodyYaw, maxUpperBodyYaw);
        Quaternion baseRotation = (animator != null && animator.enabled)
            ? upperBodyTarget.localRotation
            : upperBodyStartLocalRotation;

        upperBodyTarget.localRotation = baseRotation;

        Quaternion yawOffset = Quaternion.AngleAxis(clampedYaw * upperBodyYawWeight, transform.up);
        Quaternion pitchOffset = Quaternion.AngleAxis(pitch * upperBodyPitchWeight, aimTarget.right);
        upperBodyTarget.rotation = yawOffset * pitchOffset * upperBodyTarget.rotation;
    }

    void RotatePlayerWithCamera()
    {
        if (!rotatePlayerWithCamera
            || (rotatePlayerOnlyWhileAiming && !IsAimActive()))
        {
            return;
        }

        float currentYaw = transform.eulerAngles.y;
        float smoothedYaw = Mathf.SmoothDampAngle(
            currentYaw,
            yaw,
            ref playerYawVelocity,
            playerRotationSmoothTime
        );

        transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
    }

    bool IsAimActive()
    {
        if (weaponAnimator != null && weaponAnimator.AimWeight > 0.01f)
        {
            return true;
        }

        return kontrolPemain != null && kontrolPemain.Pemain.Aim.IsPressed();
    }

    void UpdateAimLookTargetFromCameraRay()
    {
        if (!stickAimToCameraRay || cameraTransform == null || aimLookTarget == null)
        {
            return;
        }

        // The ray starts at the physical Main Camera. In TPS mode the direction
        // comes from the player aim pivot, then Cinemachine centers the camera on
        // this target through Hard Look At.
        Vector3 rayDirection = useAimTargetForwardForRay && aimTarget != null
            ? aimTarget.forward
            : cameraTransform.forward;
        Vector3 rayOrigin = useAimTargetAsRayOrigin && aimTarget != null
            ? aimTarget.position
            : cameraTransform.position;
        Ray ray = new Ray(rayOrigin, rayDirection);
        Vector3 targetPosition = ray.origin + ray.direction * Mathf.Max(0.1f, aimNoHitDistance);
        bool isAimingAtEnemy = false;

        if (TryGetCameraAimHit(ray, out RaycastHit hit))
        {
            targetPosition = hit.point + hit.normal * aimSurfaceOffset;
            isAimingAtEnemy = IsEnemyTarget(hit.collider);
        }

        Vector3 recoilWorldOffset = cameraTransform.right * aimRecoilOffset.x + cameraTransform.up * aimRecoilOffset.y;
        float positionLerp = GetFrameLerp(aimStickSmoothSpeed);
        aimLookTarget.position = Vector3.Lerp(aimLookTarget.position, targetPosition + recoilWorldOffset, positionLerp);

        UpdateAimLookTargetScale();
        UpdateAimColor(isAimingAtEnemy);
    }

    bool TryGetCameraAimHit(Ray ray, out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            AimRaycastHits,
            Mathf.Max(0.1f, aimRaycastMaxDistance),
            aimRaycastMask,
            QueryTriggerInteraction.Ignore
        );

        bool hasHit = false;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = AimRaycastHits[i];
            if (hit.collider == null || IsOwnTransform(hit.collider.transform))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                hasHit = true;
            }
        }

        return hasHit;
    }

    bool IsOwnTransform(Transform candidate)
    {
        return candidate == transform || candidate.IsChildOf(transform);
    }

    bool IsEnemyTarget(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Enemy enemy = hitCollider.GetComponentInParent<Enemy>();
        return enemy != null && !enemy.IsDead;
    }

    void UpdateAimLookTargetScale()
    {
        if (!autoScaleAim || aimLookTarget == null)
        {
            return;
        }

        CacheDefaultAimScale();

        float distance = cameraTransform != null
            ? Vector3.Distance(cameraTransform.position, aimLookTarget.position)
            : aimScaleMaxDistance;
        float baseLargestAxis = Mathf.Max(
            Mathf.Abs(defaultAimLocalScale.x),
            Mathf.Abs(defaultAimLocalScale.y),
            Mathf.Abs(defaultAimLocalScale.z)
        );
        if (baseLargestAxis <= 0.0001f)
        {
            baseLargestAxis = 1f;
        }

        float targetLargestAxis = keepAimScreenSize
            ? GetWorldSizeForScreenHeight(distance)
            : GetDistanceBasedAimScale(distance);
        targetLargestAxis *= Mathf.Max(0f, aimScaleMultiplier);
        targetLargestAxis = Mathf.Clamp(
            targetLargestAxis,
            Mathf.Max(0f, aimMinLocalScale),
            Mathf.Max(aimMinLocalScale, aimMaxLocalScale)
        );

        Vector3 targetScale = defaultAimLocalScale / baseLargestAxis * targetLargestAxis;
        aimLookTarget.localScale = Vector3.Lerp(
            aimLookTarget.localScale,
            targetScale,
            GetFrameLerp(aimScaleSmoothSpeed)
        );
    }

    float GetWorldSizeForScreenHeight(float distance)
    {
        if (aimCamera == null)
        {
            return GetDistanceBasedAimScale(distance);
        }

        if (aimCamera.orthographic)
        {
            return aimCamera.orthographicSize * 2f * aimScreenHeightPercent;
        }

        float verticalWorldHeight = 2f
            * Mathf.Max(0.01f, distance)
            * Mathf.Tan(aimCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return verticalWorldHeight * aimScreenHeightPercent;
    }

    float GetDistanceBasedAimScale(float distance)
    {
        float distancePercent = Mathf.InverseLerp(0f, Mathf.Max(0.01f, aimScaleMaxDistance), distance);
        return Mathf.Lerp(
            Mathf.Max(0f, aimMinLocalScale),
            Mathf.Max(aimMinLocalScale, aimMaxLocalScale),
            distancePercent
        );
    }

    public void SetAimScaleMultiplier(float multiplier)
    {
        aimScaleMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetAimRecoilOffset(Vector2 offset)
    {
        aimRecoilOffset = offset;
    }

    void CacheDefaultAimScale()
    {
        if (hasDefaultAimLocalScale || aimLookTarget == null)
        {
            return;
        }

        defaultAimLocalScale = aimLookTarget.localScale;
        hasDefaultAimLocalScale = true;
    }

    void CacheAimRenderer()
    {
        if (aimRenderer != null || aimLookTarget == null)
        {
            return;
        }

        aimRenderer = aimLookTarget.GetComponent<Renderer>();
        if (aimRenderer == null)
        {
            aimRenderer = aimLookTarget.GetComponentInChildren<Renderer>();
        }
    }

    void UpdateAimColor(bool isAimingAtEnemy)
    {
        CacheAimRenderer();
        Color targetColor = isAimingAtEnemy ? aimEnemyColor : aimNormalColor;
        currentAimColor = Color.Lerp(currentAimColor, targetColor, GetFrameLerp(aimColorSmoothSpeed));
        ApplyAimColor(currentAimColor);
    }

    void ApplyAimColor(Color color)
    {
        if (aimRenderer == null)
        {
            return;
        }

        if (aimPropertyBlock == null)
        {
            aimPropertyBlock = new MaterialPropertyBlock();
        }

        aimRenderer.GetPropertyBlock(aimPropertyBlock);
        aimPropertyBlock.SetColor(ColorPropertyId, color);
        aimPropertyBlock.SetColor(BaseColorPropertyId, color);
        aimRenderer.SetPropertyBlock(aimPropertyBlock);
    }

    float GetFrameLerp(float speed)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Time.deltaTime);
    }

    void KeepCameraLookingAtAim()
    {
        if (useCinemachine && cinemachineCamera != null)
        {
            return;
        }

        if (cameraTransform == null || aimLookTarget == null)
        {
            return;
        }

        Vector3 direction = aimLookTarget.position - cameraTransform.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        cameraTransform.rotation = Quaternion.LookRotation(direction.normalized, transform.up);
    }

    void SetupCinemachine()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (cameraTransform == null && aimCamera != null)
        {
            cameraTransform = aimCamera.transform;
        }

        // Cinemachine setup is intentionally manual in the scene. Do not add,
        // discover, or mutate a Brain, virtual camera, or its components here.
    }

    void SyncCinemachineTargets()
    {
        // Targets are assigned manually on the Cinemachine Camera in the scene.
    }

    void ApplyHeadAim()
    {
        if (headTarget == null || aimLookTarget == null)
        {
            return;
        }

        Vector3 direction = aimLookTarget.position - headTarget.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion currentRotation = headTarget.rotation;
        Quaternion targetRotation = Quaternion.FromToRotation(headTarget.forward, direction.normalized) * currentRotation;
        targetRotation = Quaternion.RotateTowards(currentRotation, targetRotation, maxHeadLookAngle);
        headTarget.rotation = Quaternion.Slerp(currentRotation, targetRotation, headLookWeight);
    }

    Transform FindAimLookTarget()
    {
        if (aimTarget == null)
        {
            return null;
        }

        if (aimTarget.childCount > 0)
        {
            return aimTarget.GetChild(0);
        }

        return aimTarget;
    }

    Transform FindUpperBodyTarget()
    {
        if (animator != null && animator.isHuman)
        {
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
            {
                return spine;
            }

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
            {
                return chest;
            }

            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperChest != null)
            {
                return upperChest;
            }
        }

        Transform found = FindChildByName(transform, "Spine_01");
        if (found != null)
        {
            return found;
        }

        found = FindChildByName(transform, "Spine_02");
        if (found != null)
        {
            return found;
        }

        return FindChildByName(transform, "Hips");
    }

    Transform FindHeadTarget()
    {
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                return head;
            }
        }

        return FindChildByName(transform, "Head");
    }

    Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildByName(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
