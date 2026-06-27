using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class RuntimeFxaaEffect : MonoBehaviour
{
    private const string ShaderName = "Hidden/RuntimeFXAA";

    [Range(0.0312f, 0.0833f)] public float spanMax = 0.0625f;
    [Range(0.001f, 0.0312f)] public float reduceMin = 0.0078125f;
    [Range(0.01f, 0.25f)] public float reduceMul = 0.125f;

    private Material material;

    void OnDisable()
    {
        if (material != null)
        {
            Destroy(material);
            material = null;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Material fxaaMaterial = GetMaterial();
        if (fxaaMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        fxaaMaterial.SetVector("_TexelSize", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));
        fxaaMaterial.SetFloat("_SpanMax", spanMax);
        fxaaMaterial.SetFloat("_ReduceMin", reduceMin);
        fxaaMaterial.SetFloat("_ReduceMul", reduceMul);
        Graphics.Blit(source, destination, fxaaMaterial);
    }

    Material GetMaterial()
    {
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
        {
            return null;
        }

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return material;
    }
}
