using Lean.Pool;
using System.Collections.Generic;
using UnityEngine;

public class WorldItemPickup : MonoBehaviour
{
    static readonly List<WorldItemPickup> ActivePickups = new List<WorldItemPickup>();

    public InventoryItemData itemData;
    public Weapon weapon;
    public GameObject weaponObject;
    [Min(1)] public int amount = 1;
    public bool autoPickup;
    public bool requireKey = true;
    public KeyCode pickupKey = KeyCode.T;
    public string playerTag = "Player";
    public bool despawnWithLeanPool;
    public bool playPickupAnimation = true;
    public bool ensureTriggerCollider = true;
    public float pickupRadius = 2f;
    public bool useIgnoreRaycastLayer = true;
    public string nonBlockingLayerName = "Pickup";

    private InventoryGridUI inventory;
    private Collider currentPlayer;
    private Transform currentPlayerRoot;

    void Awake()
    {
        ApplyNonBlockingLayer();
        EnsureTriggerCollider();
    }

    void OnEnable()
    {
        RefreshPickupSetup();
    }

    void OnDisable()
    {
        UnregisterCandidate();
    }

    public void RefreshPickupSetup()
    {
        ApplyNonBlockingLayer();
        EnsureTriggerCollider();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        currentPlayer = other;
        currentPlayerRoot = GetPlayerRoot(other);
        RegisterCandidate();
        UpdateClosestPickupState();

        if (autoPickup && !requireKey)
        {
            TryPickupIfClosest(other);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        currentPlayer = other;
        currentPlayerRoot = GetPlayerRoot(other);
        RegisterCandidate();
        UpdateClosestPickupState();
    }

    void OnTriggerExit(Collider other)
    {
        if (currentPlayer == other || IsPlayer(other))
        {
            HidePickupPrompt();
            UnregisterCandidate();
        }
    }

    bool TryPickupIfClosest(Collider other)
    {
        if (!IsClosestPickupForPlayer(GetPlayerRoot(other)))
        {
            return false;
        }

        return TryPickup(other);
    }

    public bool TryPickupCurrentPlayer()
    {
        return currentPlayer != null && TryPickupIfClosest(currentPlayer);
    }

    public bool TryPickup(Collider other)
    {
        if (!HasPickupData() || amount <= 0 || other == null || !IsPlayer(other))
        {
            return false;
        }

        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        if (inventory == null)
        {
            return false;
        }

        if (!inventory.TryAddPickup(this, out int remainingAmount))
        {
            return false;
        }

        PlayPickupAnimation(other);
        amount = remainingAmount;
        if (amount <= 0)
        {
            HidePickupPrompt();
            RemoveFromWorld();
        }
        else
        {
            ShowPickupPrompt();
        }

        return true;
    }

    bool IsPlayer(Collider other)
    {
        if (other.GetComponentInParent<PlayerHealth>() != null)
        {
            return true;
        }

        if (string.IsNullOrEmpty(playerTag))
        {
            return false;
        }

        return other.CompareTag(playerTag);
    }

    Transform GetPlayerRoot(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        return other.transform.root;
    }

    void RegisterCandidate()
    {
        if (currentPlayerRoot == null || !HasPickupData() || amount <= 0)
        {
            return;
        }

        if (!ActivePickups.Contains(this))
        {
            ActivePickups.Add(this);
        }
    }

    void UnregisterCandidate()
    {
        ActivePickups.Remove(this);
        currentPlayer = null;
        currentPlayerRoot = null;
    }

    void UpdateClosestPickupState()
    {
        if (IsClosestPickupForPlayer(currentPlayerRoot))
        {
            ShowPickupPrompt();
        }
        else
        {
            HidePickupPrompt();
        }
    }

    bool IsClosestPickupForPlayer(Transform playerRoot)
    {
        if (playerRoot == null || !HasPickupData() || amount <= 0)
        {
            return false;
        }

        WorldItemPickup closest = null;
        float closestDistance = float.PositiveInfinity;
        Vector3 playerPosition = playerRoot.position;

        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            WorldItemPickup pickup = ActivePickups[i];
            if (pickup == null || !pickup.isActiveAndEnabled || pickup.currentPlayerRoot != playerRoot || !pickup.HasPickupData() || pickup.amount <= 0)
            {
                ActivePickups.RemoveAt(i);
                continue;
            }

            float distance = (pickup.transform.position - playerPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = pickup;
                closestDistance = distance;
            }
        }

        return closest == this;
    }

    void ShowPickupPrompt()
    {
        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        if (inventory == null || !HasPickupData() || amount <= 0)
        {
            return;
        }

        if (!IsClosestPickupForPlayer(currentPlayerRoot))
        {
            return;
        }

        inventory.ShowPickupPrompt(this, GetDisplayName(), amount, pickupKey);
    }

    void HidePickupPrompt()
    {
        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        inventory?.HidePickupPrompt(this);
    }

    void PlayPickupAnimation(Collider other)
    {
        if (!playPickupAnimation || other == null)
        {
            return;
        }

        PlayerPickupAnimator pickupAnimator = other.GetComponentInParent<PlayerPickupAnimator>();
        if (pickupAnimator == null)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                pickupAnimator = playerHealth.gameObject.AddComponent<PlayerPickupAnimator>();
            }
        }

        pickupAnimator?.PlayPickup();
    }

    public string GetDisplayName()
    {
        if (itemData != null)
        {
            return itemData.DisplayName;
        }

        return weapon != null ? weapon.weaponName : "Item";
    }

    public bool HasPickupData()
    {
        return itemData != null || weapon != null;
    }

    void EnsureTriggerCollider()
    {
        if (!ensureTriggerCollider)
        {
            return;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
        }

        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, pickupRadius);
    }

    void ApplyNonBlockingLayer()
    {
        if (!useIgnoreRaycastLayer || string.IsNullOrEmpty(nonBlockingLayerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(nonBlockingLayerName);
        if (layer < 0)
        {
            return;
        }

        SetLayerRecursively(transform, layer);
    }

    void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    void RemoveFromWorld()
    {
        if (despawnWithLeanPool)
        {
            LeanPool.Despawn(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
