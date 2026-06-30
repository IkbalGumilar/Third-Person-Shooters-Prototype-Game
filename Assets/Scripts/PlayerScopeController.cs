using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerScopeController : MonoBehaviour
{
    public PlayerShoot playerShoot;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerMovement playerMovement;
    public Transform playerVisualRoot;
    public Camera targetCamera;
    public CameraControler cameraControler;
    public GameObject scopeOverlayObject;
    public Transform weaponVisualRoot;
    public Transform aimScaleTarget;
    public float fovSmoothSpeed = 14f;
    public float scopedAimScaleMultiplier = 0.45f;
    public float aimScaleSmoothSpeed = 14f;
    [Header("Cinemachine Scope Camera")]
    public CinemachineCamera scopeCinemachineCamera;
    public CinemachineSplineDolly scopeDolly;
    public int scopeCameraPriority = 20;
    public float scopeDollyStartPosition = 0f;
    public float scopeDollyEndPosition = 1f;
    public float scopeDollyMoveSpeed = 6f;
    [Header("Scope FOV Zoom")]
    public float scopeDefaultFov = 60f;
    public float scopeMinimumFov = 10f;
    public float scopeMaximumFov = 80f;
    public float scopeScrollFovSensitivity = 0.15f;
    public float scopeFovSmoothSpeed = 42f;
    public float scopeFovPerSensitivityStep = 5f;
    public float scopeSensitivityPerTenFov = 0.1f;
    public float scopeMinimumSensitivityMultiplier = 0.1f;
    public float scopeZoomMemoryDuration = 5f;
    [Header("Scope Crouch Offset")]
    public float scopeCrouchYOffset = -1f;
    public float scopeCrouchOffsetSmoothSpeed = 16f;

    private float defaultFov = 60f;
    private Vector3 defaultAimScale = Vector3.one;
    private GameObject spawnedScopeOverlay;
    private Weapon activeOverlayWeapon;
    private KontrolPemain kontrolPemain;
    private bool hasDefaultCinemachineFov;
    private float defaultCinemachineFov;
    private float scopeDollyTargetPosition;
    private float scopeZoomFov;
    private float scopeZoomMemoryTimer;
    private bool wasScopeCameraActive;
    private bool scopeCameraReturning;
    private Transform scopeSplineTransform;
    private Vector3 scopeSplineBaseLocalPosition;
    private bool hasScopeSplineBasePosition;
    private readonly Dictionary<Renderer, bool> scopeHiddenRenderers = new Dictionary<Renderer, bool>();
    private bool playerVisualsHiddenForScope;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        playerVisualRoot = playerVisualRoot != null ? playerVisualRoot : transform;
        cameraControler = cameraControler != null ? cameraControler : GetComponent<CameraControler>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            defaultFov = targetCamera.fieldOfView;
        }

        if (aimScaleTarget == null && cameraControler != null)
        {
            aimScaleTarget = cameraControler.aimLookTarget;
        }

        if (aimScaleTarget != null)
        {
            defaultAimScale = aimScaleTarget.localScale;
        }

        scopeZoomFov = scopeDefaultFov;
        CacheScopeSplineTransform();
        SetOverlayActive(false);
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
        kontrolPemain?.UI.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
        kontrolPemain?.UI.Disable();
        SetPlayerVisualsHiddenForScope(false);
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Update()
    {
        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        bool isScoped = ShouldScope(weapon);
        bool useScopeCamera = isScoped && weapon != null && weapon.useCinemachineScopeCamera;
        UpdateScopeCamera(useScopeCamera);
        SetPlayerVisualsHiddenForScope(ShouldHidePlayerVisualsForScope(useScopeCamera));
        UpdateCameraFov(weapon, isScoped, useScopeCamera);
        UpdateSensitivity(weapon, isScoped, useScopeCamera);
        UpdateOverlay(weapon, isScoped);
        UpdateWeaponVisibility(weapon, isScoped);
        UpdateAimScale(isScoped);
    }

    bool ShouldScope(Weapon weapon)
    {
        if (weapon == null || !weapon.hasScope)
        {
            return false;
        }

        bool isAiming = playerShoot != null
            ? playerShoot.IsAiming
            : weaponAnimator == null || weaponAnimator.IsAiming;

        return isAiming && IsAimPressed();
    }

    bool IsAimPressed()
    {
        return MobileInputBridge.AimHeld
            || kontrolPemain != null && kontrolPemain.Pemain.Aim.IsPressed();
    }

    void UpdateCameraFov(Weapon weapon, bool isScoped, bool useScopeCamera)
    {
        CinemachineCamera tpsCamera = cameraControler != null ? cameraControler.cinemachineCamera : null;
        if (tpsCamera != null)
        {
            if (!hasDefaultCinemachineFov)
            {
                defaultCinemachineFov = tpsCamera.Lens.FieldOfView > 0f
                    ? tpsCamera.Lens.FieldOfView
                    : defaultFov;
                hasDefaultCinemachineFov = true;
            }

            float targetCinemachineFov = isScoped && weapon != null && !useScopeCamera
                ? weapon.scopedFov
                : defaultCinemachineFov;
            UpdateCinemachineFov(tpsCamera, targetCinemachineFov, fovSmoothSpeed);
            float scopeCameraFov = scopeCinemachineCamera != null ? scopeZoomFov : defaultCinemachineFov;
            float scopeFovSpeed = useScopeCamera ? scopeFovSmoothSpeed : fovSmoothSpeed;
            UpdateCinemachineFov(scopeCinemachineCamera, scopeCameraFov, scopeFovSpeed);
            return;
        }

        if (targetCamera == null)
        {
            return;
        }

        float targetCameraFov = isScoped && weapon != null && !useScopeCamera ? weapon.scopedFov : defaultFov;
        targetCamera.fieldOfView = Mathf.Lerp(
            targetCamera.fieldOfView,
            targetCameraFov,
            fovSmoothSpeed * Time.deltaTime
        );
    }

    void UpdateScopeCamera(bool active)
    {
        UpdateScopeSplineCrouchOffset();

        if (active)
        {
            scopeCameraReturning = false;
        }
        else if (wasScopeCameraActive)
        {
            scopeCameraReturning = true;
        }

        if (scopeDolly != null)
        {
            if (active && !wasScopeCameraActive)
            {
                scopeDollyTargetPosition = scopeDollyEndPosition;
            }
            else if (!active)
            {
                scopeDollyTargetPosition = scopeDollyStartPosition;
            }

            scopeDolly.CameraPosition = Mathf.MoveTowards(
                scopeDolly.CameraPosition,
                scopeDollyTargetPosition,
                Mathf.Max(0f, scopeDollyMoveSpeed) * Time.deltaTime
            );

            if (scopeCameraReturning
                && Mathf.Abs(scopeDolly.CameraPosition - scopeDollyStartPosition) <= 0.001f)
            {
                scopeCameraReturning = false;
            }
        }
        else if (!active)
        {
            scopeCameraReturning = false;
        }

        if (scopeCinemachineCamera != null)
        {
            scopeCinemachineCamera.Priority = active || scopeCameraReturning
                ? scopeCameraPriority
                : 0;
        }

        if (active)
        {
            scopeZoomMemoryTimer = Mathf.Max(0f, scopeZoomMemoryDuration);
            if (kontrolPemain != null)
            {
                float scroll = kontrolPemain.UI.ScrollWheel.ReadValue<Vector2>().y;
                scopeZoomFov = Mathf.Clamp(
                    scopeZoomFov - scroll * Mathf.Max(0f, scopeScrollFovSensitivity),
                    Mathf.Min(scopeMinimumFov, scopeMaximumFov),
                    Mathf.Max(scopeMinimumFov, scopeMaximumFov)
                );
            }
        }
        else
        {
            scopeZoomMemoryTimer -= Time.deltaTime;
            if (scopeZoomMemoryTimer <= 0f)
            {
                scopeZoomMemoryTimer = 0f;
                scopeZoomFov = scopeDefaultFov;
            }
        }

        wasScopeCameraActive = active;
    }

    void SetPlayerVisualsHiddenForScope(bool hidden)
    {
        if (playerVisualsHiddenForScope == hidden)
        {
            return;
        }

        playerVisualsHiddenForScope = hidden;
        if (hidden)
        {
            scopeHiddenRenderers.Clear();
            if (playerVisualRoot == null)
            {
                return;
            }

            Renderer[] renderers = playerVisualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (aimScaleTarget != null
                    && (renderer.transform == aimScaleTarget || renderer.transform.IsChildOf(aimScaleTarget)))
                {
                    continue;
                }

                scopeHiddenRenderers[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
            return;
        }

        foreach (KeyValuePair<Renderer, bool> entry in scopeHiddenRenderers)
        {
            if (entry.Key != null)
            {
                entry.Key.forceRenderingOff = entry.Value;
            }
        }
        scopeHiddenRenderers.Clear();
    }

    bool ShouldHidePlayerVisualsForScope(bool scopeCameraActive)
    {
        if (!scopeCameraActive)
        {
            return false;
        }

        if (scopeDolly == null)
        {
            return true;
        }

        float travel = scopeDollyEndPosition - scopeDollyStartPosition;
        if (Mathf.Abs(travel) <= 0.0001f)
        {
            return true;
        }

        float progress = (scopeDolly.CameraPosition - scopeDollyStartPosition) / travel;
        return progress >= 0.5f;
    }

    void CacheScopeSplineTransform()
    {
        if (hasScopeSplineBasePosition || scopeDolly == null || scopeDolly.Spline == null)
        {
            return;
        }

        scopeSplineTransform = scopeDolly.Spline.transform;
        scopeSplineBaseLocalPosition = scopeSplineTransform.localPosition;
        hasScopeSplineBasePosition = true;
    }

    void UpdateScopeSplineCrouchOffset()
    {
        CacheScopeSplineTransform();
        if (scopeSplineTransform == null)
        {
            return;
        }

        float crouchOffset = playerMovement != null && playerMovement.IsCrouching
            ? scopeCrouchYOffset
            : 0f;
        Vector3 targetPosition = scopeSplineBaseLocalPosition + Vector3.up * crouchOffset;
        scopeSplineTransform.localPosition = Vector3.Lerp(
            scopeSplineTransform.localPosition,
            targetPosition,
            1f - Mathf.Exp(-Mathf.Max(0f, scopeCrouchOffsetSmoothSpeed) * Time.deltaTime)
        );
    }

    void UpdateCinemachineFov(CinemachineCamera camera, float targetFov, float smoothSpeed)
    {
        if (camera == null)
        {
            return;
        }

        LensSettings lens = camera.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, Mathf.Max(0f, smoothSpeed) * Time.deltaTime);
        camera.Lens = lens;
    }

    void UpdateSensitivity(Weapon weapon, bool isScoped, bool useScopeCamera)
    {
        if (cameraControler == null)
        {
            return;
        }

        if (useScopeCamera)
        {
            float fovStepsFromDefault = (scopeDefaultFov - scopeZoomFov)
                / Mathf.Max(0.01f, scopeFovPerSensitivityStep);
            float multiplier = 1f - fovStepsFromDefault * scopeSensitivityPerTenFov;
            cameraControler.scopeSensitivityMultiplier = Mathf.Max(scopeMinimumSensitivityMultiplier, multiplier);
            return;
        }

        cameraControler.scopeSensitivityMultiplier = isScoped && weapon != null
            ? weapon.scopedSensitivityMultiplier
            : 1f;
    }

    void UpdateOverlay(Weapon weapon, bool isScoped)
    {
        if (weapon != activeOverlayWeapon && weapon != null && weapon.scopeOverlayPrefab == null)
        {
            if (spawnedScopeOverlay != null)
            {
                Destroy(spawnedScopeOverlay);
            }

            activeOverlayWeapon = weapon;
        }

        if (weapon != null && weapon.scopeOverlayPrefab != null && activeOverlayWeapon != weapon)
        {
            if (spawnedScopeOverlay != null)
            {
                Destroy(spawnedScopeOverlay);
            }

            spawnedScopeOverlay = Instantiate(weapon.scopeOverlayPrefab);
            spawnedScopeOverlay.SetActive(false);
            activeOverlayWeapon = weapon;
        }

        GameObject overlay = spawnedScopeOverlay != null ? spawnedScopeOverlay : scopeOverlayObject;
        if (overlay != null && overlay.activeSelf != isScoped)
        {
            overlay.SetActive(isScoped);
        }
    }

    void UpdateWeaponVisibility(Weapon weapon, bool isScoped)
    {
        if (weaponVisualRoot == null)
        {
            return;
        }

        bool shouldShow = !(weapon != null && weapon.hideWeaponWhenScoped && isScoped);
        if (weaponVisualRoot.gameObject.activeSelf != shouldShow)
        {
            weaponVisualRoot.gameObject.SetActive(shouldShow);
        }
    }

    void UpdateAimScale(bool isScoped)
    {
        if (aimScaleTarget == null)
        {
            return;
        }

        if (cameraControler != null && cameraControler.autoScaleAim)
        {
            cameraControler.SetAimScaleMultiplier(isScoped ? scopedAimScaleMultiplier : 1f);
            return;
        }

        Vector3 targetScale = isScoped
            ? defaultAimScale * scopedAimScaleMultiplier
            : defaultAimScale;

        aimScaleTarget.localScale = Vector3.Lerp(
            aimScaleTarget.localScale,
            targetScale,
            aimScaleSmoothSpeed * Time.deltaTime
        );
    }

    void SetOverlayActive(bool active)
    {
        if (scopeOverlayObject != null)
        {
            scopeOverlayObject.SetActive(active);
        }
    }
}
