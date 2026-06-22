using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHUD : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public PlayerMovement playerMovement;
    public PlayerShoot playerShoot;
    public HudValueBar healthBar;
    public TMP_Text healthText;
    public Slider shieldPointSlider;
    public Image shieldPointImage;
    public RectTransform shieldPointRect;
    public Image shieldFillImage;
    public RectTransform shieldFillRect;
    public Color shieldFillColor = new Color(0.25f, 0.75f, 1f, 0.9f);
    public Color shieldOverchargeColor = new Color(0.55f, 0.9f, 1f, 1f);
    public HudValueBar staminaBar;
    public GameObject bossHealthObject;
    public HudValueBar bossHealthBar;
    public HudValueBar bossRegenHealthBar;
    public TMP_Text bossNameText;
    public Transform bossDistanceOrigin;
    public float bossHudVisibleRange = 35f;
    public float bossSearchInterval = 0.25f;
    public TMP_Text magazineText;
    public GameObject chamberObject;
    public TMP_Text reserveAmmoText;
    private Enemy cachedVisibleBoss;
    private float nextBossSearchTime;
    private int lastDisplayedCurrentHealth = -1;
    private int lastDisplayedMaxHealth = -1;
    private int lastDisplayedMagazineAmmo = -1;
    private int lastDisplayedReserveAmmo = -1;

    void Awake()
    {
        AutoBind();
    }

    void Update()
    {
        AutoBind();
        UpdateBars();
        UpdateAmmoText();
        UpdateBossHealthBar();
    }

    void AutoBind()
    {
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (playerShoot == null)
        {
            playerShoot = FindAnyObjectByType<PlayerShoot>();
        }

        if (healthBar == null)
        {
            Transform health = transform.Find("Health");
            healthBar = health != null ? health.GetComponent<HudValueBar>() : null;
        }

        if (healthText == null)
        {
            Transform healthTextTransform = transform.Find("Health/Health Text");
            healthText = healthTextTransform != null ? healthTextTransform.GetComponent<TMP_Text>() : null;
        }

        if (shieldPointSlider == null || shieldPointRect == null || shieldPointImage == null || shieldFillRect == null || shieldFillImage == null)
        {
            Transform shieldPoint = transform.Find("Health/Shield point");
            if (shieldPoint == null)
            {
                shieldPoint = transform.Find("Health/Shield Point");
            }

            if (shieldPoint == null)
            {
                shieldPoint = transform.Find("Health/Shield");
            }

            if (shieldPoint != null)
            {
                shieldPointSlider = shieldPointSlider != null ? shieldPointSlider : shieldPoint.GetComponent<Slider>();
                shieldPointRect = shieldPointRect != null ? shieldPointRect : shieldPoint.GetComponent<RectTransform>();
                shieldPointImage = shieldPointImage != null ? shieldPointImage : shieldPoint.GetComponent<Image>();
                EnsureShieldFill(shieldPoint);
            }
        }

        if (staminaBar == null)
        {
            Transform stamina = transform.Find("Stamina");
            staminaBar = stamina != null ? stamina.GetComponent<HudValueBar>() : null;
        }

        if (magazineText == null)
        {
            Transform magazine = transform.Find("Bullet Icon/Magazine");
            magazineText = magazine != null ? magazine.GetComponent<TMP_Text>() : null;
        }

        if (chamberObject == null)
        {
            Transform chamber = transform.Find("Bullet Icon/Magazine/Chamber");
            chamberObject = chamber != null ? chamber.gameObject : null;
        }

        if (reserveAmmoText == null)
        {
            Transform reserveAmmo = transform.Find("Bullet Icon/Bullet");
            reserveAmmoText = reserveAmmo != null ? reserveAmmo.GetComponent<TMP_Text>() : null;
        }

        if (bossHealthObject == null)
        {
            Transform bossHealth = transform.Find("Boss Health");
            bossHealthObject = bossHealth != null ? bossHealth.gameObject : null;
        }

        if (bossHealthBar == null && bossHealthObject != null)
        {
            bossHealthBar = bossHealthObject.GetComponent<HudValueBar>();
        }

        if (bossRegenHealthBar == null && bossHealthObject != null)
        {
            Transform bossRegenHealth = bossHealthObject.transform.Find("Regen Health");
            if (bossRegenHealth == null)
            {
                bossRegenHealth = bossHealthObject.transform.Find("Regen");
            }

            if (bossRegenHealth == null)
            {
                bossRegenHealth = bossHealthObject.transform.Find("Regen Fill");
            }

            bossRegenHealthBar = bossRegenHealth != null ? bossRegenHealth.GetComponent<HudValueBar>() : null;
        }

        if (bossNameText == null && bossHealthObject != null)
        {
            Transform bossName = bossHealthObject.transform.Find("BOSS NAME");
            bossNameText = bossName != null ? bossName.GetComponent<TMP_Text>() : null;
        }

        if (bossDistanceOrigin == null && playerHealth != null)
        {
            bossDistanceOrigin = playerHealth.transform;
        }
    }

    void UpdateBars()
    {
        if (healthBar != null && playerHealth != null)
        {
            healthBar.SetValue(playerHealth.CurrentHealth, playerHealth.MaxHealth, playerHealth.RegenHealth);
        }

        if (healthText != null && playerHealth != null)
        {
            int currentHealth = Mathf.CeilToInt(Mathf.Clamp(playerHealth.CurrentHealth, 0f, playerHealth.MaxHealth));
            int maxHealth = Mathf.CeilToInt(playerHealth.MaxHealth);
            if (currentHealth != lastDisplayedCurrentHealth || maxHealth != lastDisplayedMaxHealth)
            {
                healthText.text = $"{currentHealth}/{maxHealth}";
                lastDisplayedCurrentHealth = currentHealth;
                lastDisplayedMaxHealth = maxHealth;
            }
        }

        UpdateShieldBar();

        if (staminaBar != null && playerMovement != null)
        {
            staminaBar.SetValue(playerMovement.CurrentStamina, playerMovement.maxStamina);
        }
    }

    void UpdateShieldBar()
    {
        if (shieldPointRect == null && shieldPointImage == null)
        {
            return;
        }

        float normalized = playerHealth != null && playerHealth.MaxShield > 0f
            ? Mathf.Clamp01(playerHealth.CurrentShield / playerHealth.MaxShield)
            : 0f;

        GameObject shieldObject = shieldPointRect != null
            ? shieldPointRect.gameObject
            : shieldPointImage != null ? shieldPointImage.gameObject : null;

        if (shieldObject != null)
        {
            bool shouldShow = playerHealth != null && playerHealth.MaxShield > 0f && normalized > 0f;
            if (shieldObject.activeSelf != shouldShow)
            {
                shieldObject.SetActive(shouldShow);
            }
        }

        if (shieldPointSlider != null)
        {
            shieldPointSlider.minValue = 0f;
            shieldPointSlider.maxValue = 1f;
            shieldPointSlider.fillRect = shieldFillRect;
            shieldPointSlider.SetValueWithoutNotify(normalized);
        }

        if (shieldFillImage != null)
        {
            bool isOvercharged = playerHealth != null && playerHealth.MaxShield > 0f &&
                                 playerHealth.CurrentShield >= playerHealth.MaxShield * 1.01f;
            shieldFillImage.color = isOvercharged ? shieldOverchargeColor : shieldFillColor;
        }
    }

    void EnsureShieldFill(Transform shieldPoint)
    {
        if (shieldPoint == null)
        {
            return;
        }

        if (shieldFillRect == null || shieldFillImage == null)
        {
            Transform fill = shieldPoint.Find("Fill");
            if (fill == null)
            {
                fill = shieldPoint.Find("Shield Fill");
            }

            if (fill != null)
            {
                shieldFillRect = shieldFillRect != null ? shieldFillRect : fill.GetComponent<RectTransform>();
                shieldFillImage = shieldFillImage != null ? shieldFillImage : fill.GetComponent<Image>();
            }
        }

        if (shieldFillRect == null || shieldFillImage == null)
        {
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(shieldPoint, false);
            shieldFillRect = fillObject.GetComponent<RectTransform>();
            shieldFillImage = fillObject.GetComponent<Image>();
        }

        shieldFillRect.anchorMin = Vector2.zero;
        shieldFillRect.anchorMax = Vector2.one;
        shieldFillRect.offsetMin = Vector2.zero;
        shieldFillRect.offsetMax = Vector2.zero;
        shieldFillRect.pivot = new Vector2(0f, 0.5f);
        shieldFillRect.localScale = Vector3.one;

        shieldFillImage.raycastTarget = false;
        shieldFillImage.color = shieldFillColor;

        if (shieldPointImage != null)
        {
            shieldFillImage.sprite = shieldPointImage.sprite;
            shieldFillImage.type = shieldPointImage.sprite != null ? shieldPointImage.type : Image.Type.Simple;
            shieldFillImage.preserveAspect = shieldPointImage.preserveAspect;
        }

        if (shieldPointSlider != null)
        {
            shieldPointSlider.fillRect = shieldFillRect;
            shieldPointSlider.targetGraphic = shieldPointImage;
        }
    }

    void UpdateAmmoText()
    {
        if (playerShoot == null)
        {
            return;
        }

        if (magazineText != null)
        {
            int magazineAmmo = playerShoot.CurrentMagazineAmmo;
            if (magazineAmmo != lastDisplayedMagazineAmmo)
            {
                magazineText.text = magazineAmmo.ToString();
                lastDisplayedMagazineAmmo = magazineAmmo;
            }
        }

        if (chamberObject != null)
        {
            bool shouldShowChamber = playerShoot.HasChamberedRound;
            if (chamberObject.activeSelf != shouldShowChamber)
            {
                chamberObject.SetActive(shouldShowChamber);
            }
        }

        if (reserveAmmoText != null)
        {
            int reserveAmmo = playerShoot.ReserveAmmo;
            if (reserveAmmo != lastDisplayedReserveAmmo)
            {
                reserveAmmoText.text = reserveAmmo.ToString();
                lastDisplayedReserveAmmo = reserveAmmo;
            }
        }
    }

    void UpdateBossHealthBar()
    {
        if (bossHealthObject == null || bossHealthBar == null)
        {
            return;
        }

        Enemy boss = FindVisibleBoss();
        bool shouldShow = boss != null;
        if (bossHealthObject.activeSelf != shouldShow)
        {
            bossHealthObject.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            return;
        }

        bossHealthBar.SetValue(boss.CurrentHealth, boss.MaxHealth, boss.RegenHealth);
        if (bossNameText != null)
        {
            string bossName = !string.IsNullOrEmpty(boss.enemyData.enemyName)
                ? boss.enemyData.enemyName
                : "Boss";
            if (bossNameText.text != bossName)
            {
                bossNameText.text = bossName;
            }
        }
    }

    Enemy FindVisibleBoss()
    {
        if (bossDistanceOrigin == null)
        {
            return null;
        }

        if (cachedVisibleBoss != null && !cachedVisibleBoss.IsDead && IsBossVisible(cachedVisibleBoss) && Time.time < nextBossSearchTime)
        {
            return cachedVisibleBoss;
        }

        nextBossSearchTime = Time.time + Mathf.Max(0.05f, bossSearchInterval);
        IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
        Enemy closestBoss = null;
        float closestDistanceSqr = bossHudVisibleRange * bossHudVisibleRange;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || enemy.enemyData == null || enemy.enemyData.enemyType != EnemyType.Boss)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - bossDistanceOrigin.position).sqrMagnitude;
            if (distanceSqr <= closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestBoss = enemy;
            }
        }

        cachedVisibleBoss = closestBoss;
        return cachedVisibleBoss;
    }

    bool IsBossVisible(Enemy enemy)
    {
        if (enemy == null || enemy.enemyData == null || enemy.enemyData.enemyType != EnemyType.Boss || bossDistanceOrigin == null)
        {
            return false;
        }

        float visibleRangeSqr = bossHudVisibleRange * bossHudVisibleRange;
        return (enemy.transform.position - bossDistanceOrigin.position).sqrMagnitude <= visibleRangeSqr;
    }
}
