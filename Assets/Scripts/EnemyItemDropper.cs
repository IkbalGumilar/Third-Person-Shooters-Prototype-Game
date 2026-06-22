using Lean.Pool;
using UnityEngine;

public class EnemyItemDropper : MonoBehaviour
{
    public EnemyLootTable lootTable;
    public EnemyLootDropEntry[] drops;
    public Transform dropOrigin;
    public GameObject defaultPickupPrefab;
    public GameObject pickupEffectPrefab;
    public bool spawnWithLeanPool;
    public Vector2 dropScatterRadius = new Vector2(0.25f, 0.85f);
    public float dropHeightOffset = 0.15f;
    public float pickupRadius = 2f;
    public Vector3 pickupEffectLocalPosition = new Vector3(0f, 0.35f, 0f);
    public Vector3 pickupEffectLocalEuler = new Vector3(-90f, 0f, 0f);
    public Vector3 pickupEffectLocalScale = Vector3.one * 0.1f;
    public bool useIgnoreRaycastLayer = true;
    public string pickupLayerName = "Pickup";
    public Color ammoPickupColor = Color.white;
    public Color healingPickupColor = new Color(0.15f, 1f, 0.25f, 1f);
    public Color weaponPickupColor = new Color(0.25f, 0.55f, 1f, 1f);
    public Color statusEffectPickupColor = new Color(0.75f, 0.35f, 1f, 1f);
    public Color defaultPickupColor = new Color(1f, 0.9f, 0.25f, 1f);

    public void DropLoot()
    {
        DropEntries(lootTable != null ? lootTable.drops : null);
        DropEntries(drops);
    }

    void DropEntries(EnemyLootDropEntry[] entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            EnemyLootDropEntry entry = entries[i];
            if (entry == null || !entry.RollDrop())
            {
                continue;
            }

            SpawnDrop(entry, entry.RollAmount());
        }
    }

    void SpawnDrop(EnemyLootDropEntry entry, int amount)
    {
        if (entry == null || entry.item == null || amount <= 0)
        {
            return;
        }

        Vector3 position = GetDropPosition();
        Quaternion rotation = Quaternion.identity;
        GameObject prefab = entry.pickupPrefab != null ? entry.pickupPrefab : defaultPickupPrefab;
        GameObject pickupObject = prefab != null
            ? SpawnPickupPrefab(prefab, position, rotation)
            : CreateFallbackPickup(position, rotation, entry.item);

        if (pickupObject == null)
        {
            return;
        }

        WorldItemPickup pickup = pickupObject.GetComponent<WorldItemPickup>();
        if (pickup == null)
        {
            pickup = pickupObject.AddComponent<WorldItemPickup>();
        }

        pickup.itemData = entry.item;
        pickup.amount = amount;
        pickup.autoPickup = false;
        pickup.requireKey = true;
        pickup.pickupKey = KeyCode.T;
        pickup.pickupRadius = Mathf.Max(0.1f, pickupRadius);
        pickup.ensureTriggerCollider = true;
        pickup.useIgnoreRaycastLayer = useIgnoreRaycastLayer;
        pickup.nonBlockingLayerName = pickupLayerName;
        AttachPickupEffect(pickupObject);
        pickup.RefreshPickupSetup();
    }

    GameObject SpawnPickupPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return spawnWithLeanPool
            ? LeanPool.Spawn(prefab, position, rotation)
            : Instantiate(prefab, position, rotation);
    }

    GameObject CreateFallbackPickup(Vector3 position, Quaternion rotation, InventoryItemData item)
    {
        GameObject pickupObject = new GameObject($"{item.DisplayName} Pickup");
        pickupObject.name = $"{item.DisplayName} Pickup";
        pickupObject.transform.SetPositionAndRotation(position, rotation);

        SphereCollider trigger = pickupObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, pickupRadius);

        Rigidbody body = pickupObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(pickupObject.transform, false);
        visual.transform.localScale = Vector3.one * 0.25f;
        ApplyPickupVisualColor(visual, item);
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        return pickupObject;
    }

    void AttachPickupEffect(GameObject pickupObject)
    {
        if (pickupObject == null || pickupEffectPrefab == null)
        {
            return;
        }

        Transform existingEffect = pickupObject.transform.Find("Pickup Effect");
        if (existingEffect != null)
        {
            existingEffect.localPosition = pickupEffectLocalPosition;
            existingEffect.localRotation = Quaternion.Euler(pickupEffectLocalEuler);
            existingEffect.localScale = pickupEffectLocalScale;
            existingEffect.gameObject.SetActive(true);
            RestartParticles(existingEffect.gameObject);
            return;
        }

        GameObject effectObject = Instantiate(pickupEffectPrefab, pickupObject.transform.position, pickupObject.transform.rotation);

        effectObject.name = "Pickup Effect";
        effectObject.transform.SetParent(pickupObject.transform, false);
        effectObject.transform.localPosition = pickupEffectLocalPosition;
        effectObject.transform.localRotation = Quaternion.Euler(pickupEffectLocalEuler);
        effectObject.transform.localScale = pickupEffectLocalScale;
        RestartParticles(effectObject);
    }

    void RestartParticles(GameObject effectObject)
    {
        if (effectObject == null)
        {
            return;
        }

        ParticleSystem[] particles = effectObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    void ApplyPickupVisualColor(GameObject visual, InventoryItemData item)
    {
        if (visual == null)
        {
            return;
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = new Material(renderer.sharedMaterial);
        material.color = GetPickupColor(item);
        renderer.sharedMaterial = material;
    }

    Color GetPickupColor(InventoryItemData item)
    {
        if (item == null)
        {
            return defaultPickupColor;
        }

        switch (item.itemType)
        {
            case InventoryItemType.Ammo:
                return ammoPickupColor;
            case InventoryItemType.HealingItem:
                return healingPickupColor;
            case InventoryItemType.Weapon:
                return weaponPickupColor;
            case InventoryItemType.StatusEffectItem:
                return statusEffectPickupColor;
            default:
                return defaultPickupColor;
        }
    }

    Vector3 GetDropPosition()
    {
        Vector3 origin = dropOrigin != null ? dropOrigin.position : transform.position;
        float radius = Random.Range(Mathf.Max(0f, dropScatterRadius.x), Mathf.Max(dropScatterRadius.x, dropScatterRadius.y));
        Vector2 offset = Random.insideUnitCircle.normalized * radius;
        return origin + new Vector3(offset.x, dropHeightOffset, offset.y);
    }
}
