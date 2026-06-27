using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10000)]
public sealed class CameraObstacleResolver : MonoBehaviour
{
    private static readonly RaycastHit[] Hits = new RaycastHit[32];
    private static readonly Collider[] OverlapHits = new Collider[16];

    public Transform collisionOrigin;
    public Vector3 collisionOriginLocalOffset = new Vector3(0f, 1.4f, 0f);
    public Transform ignoreRoot;
    public LayerMask obstacleMask = ~0;
    [Min(0.01f)] public float collisionRadius = 0.25f;
    [Min(0f)] public float surfaceOffset = 0.06f;
    [Min(0f)] public float minimumDistanceFromOrigin = 0.3f;
    public bool useRaycastFallback = true;
    public bool resolveCameraOverlap = true;
    [Range(1, 16)] public int overlapResolveIterations = 8;
    public bool enableCollision = true;

    private Camera attachedCamera;

    void Awake()
    {
        attachedCamera = GetComponent<Camera>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnPreCull()
    {
        ResolveCameraCollision();
    }

    void LateUpdate()
    {
        ResolveCameraCollision();
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (attachedCamera == null)
        {
            attachedCamera = GetComponent<Camera>();
        }

        if (attachedCamera == null || renderingCamera == attachedCamera)
        {
            ResolveCameraCollision();
        }
    }

    public void ResolveCameraCollision()
    {
        if (!enableCollision || collisionOrigin == null)
        {
            return;
        }

        Vector3 origin = collisionOrigin.TransformPoint(collisionOriginLocalOffset);
        Vector3 desiredPosition = transform.position;
        Vector3 toCamera = desiredPosition - origin;
        float desiredDistance = toCamera.magnitude;
        if (desiredDistance <= 0.001f)
        {
            return;
        }

        Vector3 direction = toCamera / desiredDistance;
        int mask = obstacleMask.value == 0 ? Physics.DefaultRaycastLayers : obstacleMask.value;
        float safeDistance = desiredDistance;
        bool hasBlockingHit = false;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, collisionRadius),
            direction,
            Hits,
            desiredDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        if (TryGetNearestBlockingHit(hitCount, out RaycastHit nearestHit))
        {
            safeDistance = nearestHit.distance;
            hasBlockingHit = true;
        }

        if (useRaycastFallback)
        {
            int rayHitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                Hits,
                desiredDistance,
                mask,
                QueryTriggerInteraction.Ignore
            );

            if (TryGetNearestBlockingHit(rayHitCount, out RaycastHit nearestRayHit)
                && nearestRayHit.distance < safeDistance)
            {
                safeDistance = nearestRayHit.distance;
                hasBlockingHit = true;
            }
        }

        if (!hasBlockingHit && resolveCameraOverlap && IsBlockedAt(desiredPosition, Mathf.Max(0.01f, collisionRadius), mask))
        {
            safeDistance = FindNearestSafeDistance(origin, direction, desiredDistance, Mathf.Max(0.01f, collisionRadius), mask);
            hasBlockingHit = true;
        }

        if (!hasBlockingHit)
        {
            return;
        }

        safeDistance = Mathf.Max(
            Mathf.Max(0f, minimumDistanceFromOrigin),
            safeDistance - Mathf.Max(0f, surfaceOffset)
        );
        transform.position = origin + direction * safeDistance;
    }

    bool TryGetNearestBlockingHit(int hitCount, out RaycastHit nearestHit)
    {
        nearestHit = default(RaycastHit);
        bool hasHit = false;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null || ShouldIgnore(hit.collider.transform))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                hasHit = true;
            }
        }

        return hasHit;
    }

    float FindNearestSafeDistance(Vector3 origin, Vector3 direction, float desiredDistance, float radius, int mask)
    {
        float minDistance = Mathf.Max(0f, minimumDistanceFromOrigin);
        float low = minDistance;
        float high = Mathf.Max(minDistance, desiredDistance);

        if (IsBlockedAt(origin + direction * low, radius, mask))
        {
            return low;
        }

        for (int i = 0; i < overlapResolveIterations; i++)
        {
            float middle = (low + high) * 0.5f;
            if (IsBlockedAt(origin + direction * middle, radius, mask))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return low;
    }

    bool IsBlockedAt(Vector3 position, float radius, int mask)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            OverlapHits,
            mask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = OverlapHits[i];
            if (hit != null && !ShouldIgnore(hit.transform))
            {
                return true;
            }
        }

        return false;
    }

    bool ShouldIgnore(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return true;
        }

        if (ignoreRoot != null && (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
        {
            return true;
        }

        if (collisionOrigin != null && (hitTransform == collisionOrigin || hitTransform.IsChildOf(collisionOrigin)))
        {
            return true;
        }

        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }
}
