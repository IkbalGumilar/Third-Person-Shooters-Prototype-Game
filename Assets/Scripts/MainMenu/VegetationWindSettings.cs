using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runtime switch for foliage wind driven by WindZone and CTI shaders.</summary>
public sealed class VegetationWindSettings : MonoBehaviour
{
    public const string PreferenceKey = "Graphics.FoliagePhysics";
    public const string TextureQualityPreferenceKey = "Graphics.TextureQuality";

    [Header("Targets")]
    [SerializeField] private WindZone[] windZones;
    [SerializeField] private Behaviour[] windControllers;
    [SerializeField] private Terrain[] terrains;
    [SerializeField] private Material[] foliageMaterials;
    [SerializeField] private bool autoFindTargets = true;

    [Header("Shader")]
    [SerializeField] private string terrainLodWindProperty = "_TerrainLODWind";
    [SerializeField] private string billboardWindStrengthProperty = "_WindStrength";

    private readonly Dictionary<WindZone, WindZoneState> windZoneDefaults = new();
    private readonly Dictionary<Behaviour, bool> controllerDefaults = new();
    private readonly Dictionary<TerrainData, TerrainWindState> terrainDefaults = new();
    private readonly Dictionary<Terrain, TerrainRenderState> terrainRenderDefaults = new();
    private readonly Dictionary<Material, float> materialWindDefaults = new();
    private int terrainLodWindId;
    private int billboardWindStrengthId;
    private bool initialized;
    private bool applyingRuntimeDefaults;

    private struct WindZoneState
    {
        public float main;
        public float turbulence;
        public float pulseMagnitude;
        public float pulseFrequency;
    }

    private struct TerrainWindState
    {
        public float strength;
        public float amount;
        public float speed;
    }

    private struct TerrainRenderState
    {
        public float treeDistance;
        public float treeBillboardDistance;
        public float treeCrossFadeLength;
        public float detailObjectDistance;
        public float detailObjectDensity;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        ApplySaved();
        ApplyTextureQuality(PlayerPrefs.GetInt(TextureQualityPreferenceKey, QualitySettings.globalTextureMipmapLimit), false);
        StartCoroutine(ApplySavedAfterSceneActivation());
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        terrainLodWindId = Shader.PropertyToID(terrainLodWindProperty);
        billboardWindStrengthId = Shader.PropertyToID(billboardWindStrengthProperty);

        if (autoFindTargets)
        {
            FindSceneTargets();
        }

        EnsureTargetArrays();
        CacheDefaults(true);
        initialized = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneApplier()
    {
        VegetationWindSettings existing = FindAnyObjectByType<VegetationWindSettings>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.ApplyRuntimeDefaults();
            return;
        }

        var applierObject = new GameObject("Runtime Vegetation Settings");
        VegetationWindSettings applier = applierObject.AddComponent<VegetationWindSettings>();
        applier.ApplyRuntimeDefaults();
    }

    public void ApplySaved()
    {
        Initialize();
        SetEnabled(PlayerPrefs.GetInt(PreferenceKey, 1) == 1, false);
    }

    public void SetEnabled(bool active, bool save)
    {
        Initialize();
        RefreshSceneTargets();

        for (int i = 0; i < windZones.Length; i++)
        {
            WindZone windZone = windZones[i];
            if (windZone == null)
            {
                continue;
            }

            if (!windZoneDefaults.TryGetValue(windZone, out WindZoneState state))
            {
                state = CaptureWindZone(windZone);
                windZoneDefaults[windZone] = state;
            }

            windZone.windMain = active ? state.main : 0f;
            windZone.windTurbulence = active ? state.turbulence : 0f;
            windZone.windPulseMagnitude = active ? state.pulseMagnitude : 0f;
            windZone.windPulseFrequency = active ? state.pulseFrequency : 0f;
        }

        for (int i = 0; i < windControllers.Length; i++)
        {
            Behaviour controller = windControllers[i];
            if (controller == null)
            {
                continue;
            }

            if (!controllerDefaults.TryGetValue(controller, out bool defaultEnabled))
            {
                defaultEnabled = controller.enabled;
                controllerDefaults[controller] = defaultEnabled;
            }

            SetWindMultiplier(controller, active ? 1f : 0f);
            controller.enabled = active && defaultEnabled;
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            TerrainData terrainData = terrain != null ? terrain.terrainData : null;
            if (terrainData == null)
            {
                continue;
            }

            if (!terrainDefaults.TryGetValue(terrainData, out TerrainWindState state))
            {
                state = CaptureTerrainWind(terrainData);
                terrainDefaults[terrainData] = state;
            }

            terrainData.wavingGrassStrength = active ? state.strength : 0f;
            terrainData.wavingGrassAmount = active ? state.amount : 0f;
            terrainData.wavingGrassSpeed = active ? state.speed : 0f;
        }

        for (int i = 0; i < foliageMaterials.Length; i++)
        {
            Material material = foliageMaterials[i];
            if (material == null || !material.HasProperty(billboardWindStrengthId))
            {
                continue;
            }

            if (!materialWindDefaults.TryGetValue(material, out float defaultStrength))
            {
                defaultStrength = material.GetFloat(billboardWindStrengthId);
                materialWindDefaults[material] = defaultStrength;
            }

            material.SetFloat(billboardWindStrengthId, active ? defaultStrength : 0f);
        }

        if (!active)
        {
            Shader.SetGlobalVector(terrainLodWindId, Vector4.zero);
        }

        if (save)
        {
            PlayerPrefs.SetInt(PreferenceKey, active ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(PreferenceKey);
        SetEnabled(true, true);
    }

    public void ApplyTextureQuality(int mipLimit, bool save)
    {
        Initialize();
        RefreshSceneTargets();

        int clampedMipLimit = Mathf.Clamp(mipLimit, 0, 3);
        QualitySettings.globalTextureMipmapLimit = clampedMipLimit;
        float distanceMultiplier = GetFoliageDistanceMultiplier(clampedMipLimit);
        float densityMultiplier = GetFoliageDensityMultiplier(clampedMipLimit);

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
            {
                continue;
            }

            if (!terrainRenderDefaults.TryGetValue(terrain, out TerrainRenderState state))
            {
                state = CaptureTerrainRender(terrain);
                terrainRenderDefaults[terrain] = state;
            }

            terrain.treeDistance = state.treeDistance * distanceMultiplier;
            terrain.treeBillboardDistance = state.treeBillboardDistance * distanceMultiplier;
            terrain.treeCrossFadeLength = state.treeCrossFadeLength * distanceMultiplier;
            terrain.detailObjectDistance = state.detailObjectDistance * distanceMultiplier;
            terrain.detailObjectDensity = Mathf.Clamp01(state.detailObjectDensity * densityMultiplier);
        }

        if (save)
        {
            PlayerPrefs.SetInt(TextureQualityPreferenceKey, clampedMipLimit);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator ApplySavedAfterSceneActivation()
    {
        yield return null;
        ApplyRuntimeDefaults();
        yield return null;
        ApplyRuntimeDefaults();
        yield return new WaitForSecondsRealtime(0.25f);
        ApplyRuntimeDefaults();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyRuntimeDefaults();
    }

    private void ApplyRuntimeDefaults()
    {
        if (applyingRuntimeDefaults)
        {
            return;
        }

        applyingRuntimeDefaults = true;
        ApplySaved();
        ApplyTextureQuality(PlayerPrefs.GetInt(TextureQualityPreferenceKey, QualitySettings.globalTextureMipmapLimit), false);
        applyingRuntimeDefaults = false;
    }

    private void RefreshSceneTargets()
    {
        if (!autoFindTargets)
        {
            EnsureTargetArrays();
            CacheDefaults(false);
            return;
        }

        FindSceneTargets();
        EnsureTargetArrays();
        CacheDefaults(false);
    }

    private void FindSceneTargets()
    {
        windZones = FindObjectsByType<WindZone>(FindObjectsInactive.Include);
        terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include);

        var controllers = new List<Behaviour>();
        Behaviour[] behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "CTI_CustomWind")
            {
                controllers.Add(behaviour);
            }
        }

        windControllers = controllers.ToArray();

        var materials = new HashSet<Material>();
        if (foliageMaterials != null)
        {
            for (int i = 0; i < foliageMaterials.Length; i++)
            {
                AddFoliageMaterial(materials, foliageMaterials[i]);
            }
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int j = 0; j < sharedMaterials.Length; j++)
            {
                AddFoliageMaterial(materials, sharedMaterials[j]);
            }
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            TerrainData terrainData = terrains[i] != null ? terrains[i].terrainData : null;
            if (terrainData == null)
            {
                continue;
            }

            TreePrototype[] treePrototypes = terrainData.treePrototypes;
            for (int j = 0; j < treePrototypes.Length; j++)
            {
                GameObject prefab = treePrototypes[j].prefab;
                if (prefab == null)
                {
                    continue;
                }

                Renderer[] prefabRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < prefabRenderers.Length; k++)
                {
                    Material[] sharedMaterials = prefabRenderers[k].sharedMaterials;
                    for (int l = 0; l < sharedMaterials.Length; l++)
                    {
                        AddFoliageMaterial(materials, sharedMaterials[l]);
                    }
                }
            }
        }

        foliageMaterials = new Material[materials.Count];
        materials.CopyTo(foliageMaterials);
    }

    private void EnsureTargetArrays()
    {
        if (windZones == null)
        {
            windZones = Array.Empty<WindZone>();
        }

        if (windControllers == null)
        {
            windControllers = Array.Empty<Behaviour>();
        }

        if (terrains == null)
        {
            terrains = Array.Empty<Terrain>();
        }

        if (foliageMaterials == null)
        {
            foliageMaterials = Array.Empty<Material>();
        }
    }

    private void CacheDefaults(bool clearExisting)
    {
        if (clearExisting)
        {
            windZoneDefaults.Clear();
            controllerDefaults.Clear();
            terrainDefaults.Clear();
            terrainRenderDefaults.Clear();
            materialWindDefaults.Clear();
        }

        for (int i = 0; i < windZones.Length; i++)
        {
            WindZone windZone = windZones[i];
            if (windZone != null && !windZoneDefaults.ContainsKey(windZone))
            {
                windZoneDefaults[windZone] = CaptureWindZone(windZone);
            }
        }

        for (int i = 0; i < windControllers.Length; i++)
        {
            Behaviour controller = windControllers[i];
            if (controller != null && !controllerDefaults.ContainsKey(controller))
            {
                controllerDefaults[controller] = controller.enabled;
            }
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            TerrainData terrainData = terrain != null ? terrain.terrainData : null;
            if (terrainData != null && !terrainDefaults.ContainsKey(terrainData))
            {
                terrainDefaults[terrainData] = CaptureTerrainWind(terrainData);
            }

            if (terrain != null && !terrainRenderDefaults.ContainsKey(terrain))
            {
                terrainRenderDefaults[terrain] = CaptureTerrainRender(terrain);
            }
        }

        for (int i = 0; i < foliageMaterials.Length; i++)
        {
            Material material = foliageMaterials[i];
            if (material != null && material.HasProperty(billboardWindStrengthId) && !materialWindDefaults.ContainsKey(material))
            {
                materialWindDefaults[material] = material.GetFloat(billboardWindStrengthId);
            }
        }
    }

    private void AddFoliageMaterial(HashSet<Material> materials, Material material)
    {
        if (material != null && material.HasProperty(billboardWindStrengthId))
        {
            materials.Add(material);
        }
    }

    private static WindZoneState CaptureWindZone(WindZone windZone)
    {
        return new WindZoneState
        {
            main = windZone.windMain,
            turbulence = windZone.windTurbulence,
            pulseMagnitude = windZone.windPulseMagnitude,
            pulseFrequency = windZone.windPulseFrequency
        };
    }

    private static TerrainWindState CaptureTerrainWind(TerrainData terrainData)
    {
        return new TerrainWindState
        {
            strength = terrainData.wavingGrassStrength,
            amount = terrainData.wavingGrassAmount,
            speed = terrainData.wavingGrassSpeed
        };
    }

    private static TerrainRenderState CaptureTerrainRender(Terrain terrain)
    {
        return new TerrainRenderState
        {
            treeDistance = terrain.treeDistance,
            treeBillboardDistance = terrain.treeBillboardDistance,
            treeCrossFadeLength = terrain.treeCrossFadeLength,
            detailObjectDistance = terrain.detailObjectDistance,
            detailObjectDensity = terrain.detailObjectDensity
        };
    }

    private static float GetFoliageDistanceMultiplier(int mipLimit)
    {
        return mipLimit switch
        {
            0 => 1f,
            1 => 0.75f,
            2 => 0.5f,
            _ => 0.3f
        };
    }

    private static float GetFoliageDensityMultiplier(int mipLimit)
    {
        return mipLimit switch
        {
            0 => 1f,
            1 => 0.8f,
            2 => 0.55f,
            _ => 0.35f
        };
    }

    private static void SetWindMultiplier(Behaviour controller, float value)
    {
        Type type = controller.GetType();
        FieldInfo field = type.GetField("WindMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(controller, value);
            return;
        }

        PropertyInfo property = type.GetProperty("WindMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType == typeof(float))
        {
            property.SetValue(controller, value);
        }
    }
}
