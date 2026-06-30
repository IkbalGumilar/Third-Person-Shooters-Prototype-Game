using UnityEngine;

public static class MobileInputBridge
{
    public static Vector2 MoveInput { get; private set; }
    public static bool RunHeld { get; private set; }
    public static bool AimHeld { get; private set; }
    public static bool ShootHeld { get; private set; }
    public static bool BlockHeld { get; private set; }
    public static bool MobileUIActive { get; private set; }

    private static int jumpFrame = -1;
    private static int rollFrame = -1;
    private static int crouchFrame = -1;
    private static int crawlFrame = -1;
    private static int reloadFrame = -1;
    private static int shootFrame = -1;
    private static int meleeFrame = -1;
    private static int pickupFrame = -1;
    private static int inventoryUseFrame = -1;

    public static void SetMove(Vector2 value)
    {
        MoveInput = Vector2.ClampMagnitude(value, 1f);
    }

    public static void SetRunHeld(bool held) => RunHeld = held;
    public static void SetAimHeld(bool held) => AimHeld = held;
    public static void SetShootHeld(bool held)
    {
        ShootHeld = held;
        if (held)
        {
            shootFrame = Time.frameCount;
        }
    }

    public static void SetBlockHeld(bool held) => BlockHeld = held;
    public static void SetMobileUIActive(bool active) => MobileUIActive = active;
    public static void QueueJump() => jumpFrame = Time.frameCount;
    public static void QueueRoll() => rollFrame = Time.frameCount;
    public static void QueueCrouch() => crouchFrame = Time.frameCount;
    public static void QueueCrawl() => crawlFrame = Time.frameCount;
    public static void QueueReload() => reloadFrame = Time.frameCount;
    public static void QueueMelee() => meleeFrame = Time.frameCount;
    public static void QueuePickup() => pickupFrame = Time.frameCount;
    public static void QueueInventoryUse() => inventoryUseFrame = Time.frameCount;

    public static bool ConsumeJump() => Consume(ref jumpFrame);
    public static bool ConsumeRoll() => Consume(ref rollFrame);
    public static bool ConsumeCrouch() => Consume(ref crouchFrame);
    public static bool ConsumeCrawl() => Consume(ref crawlFrame);
    public static bool ConsumeReload() => Consume(ref reloadFrame);
    public static bool ConsumeShootPressedThisFrame() => Consume(ref shootFrame);
    public static bool ConsumeMelee() => Consume(ref meleeFrame);
    public static bool ConsumePickup() => Consume(ref pickupFrame);
    public static bool ConsumeInventoryUse() => Consume(ref inventoryUseFrame);

    public static void Clear()
    {
        MoveInput = Vector2.zero;
        RunHeld = false;
        AimHeld = false;
        ShootHeld = false;
        BlockHeld = false;
        MobileUIActive = false;
        jumpFrame = -1;
        rollFrame = -1;
        crouchFrame = -1;
        crawlFrame = -1;
        reloadFrame = -1;
        shootFrame = -1;
        meleeFrame = -1;
        pickupFrame = -1;
        inventoryUseFrame = -1;
    }

    private static bool Consume(ref int frame)
    {
        if (frame < 0 || Time.frameCount - frame > 1)
        {
            return false;
        }

        frame = -1;
        return true;
    }
}
