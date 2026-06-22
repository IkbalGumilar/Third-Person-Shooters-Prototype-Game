using UnityEngine;

[System.Serializable]
public class EnemyLootDropEntry
{
    public InventoryItemData item;

    [Range(0f, 100f)]
    public float dropChance = 10f;

    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;
    public GameObject pickupPrefab;

    public int RollAmount()
    {
        int min = Mathf.Max(1, minAmount);
        int max = Mathf.Max(min, maxAmount);
        return Random.Range(min, max + 1);
    }

    public bool RollDrop()
    {
        return item != null && Random.Range(0f, 100f) <= Mathf.Clamp(dropChance, 0f, 100f);
    }
}

[CreateAssetMenu(fileName = "Enemy Loot Table", menuName = "Scriptable Objects/Enemy Loot Table")]
public class EnemyLootTable : ScriptableObject
{
    public EnemyLootDropEntry[] drops;
}
