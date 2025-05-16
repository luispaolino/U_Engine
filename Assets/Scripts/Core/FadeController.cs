using UnityEngine;
using System.Collections;

public class FadeController : MonoBehaviour
{
    public static FadeController I { get; private set; }

    [Tooltip("The material used by your FullScreenFade Render Feature")]
    public Material fadeMaterial;

    const string k_IntensityProp = "Intensity"; // name of the slider in your shader

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (fadeMaterial == null)
            Debug.LogError("FadeController: assign the URP pass material!");
    }

    /// <summary>Fade from opaque (1) → transparent (0)</summary>
    public Coroutine FadeIn (float duration = .6f)  => StartCoroutine(Fade(1f, 0f, duration));

    /// <summary>Fade from transparent (0) → opaque (1)</summary>
    public Coroutine FadeOut(float duration = .6f)  => StartCoroutine(Fade(0f, 1f, duration));

    IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        fadeMaterial.SetFloat(k_IntensityProp, from);

        while (t < time)
        {
            t += Time.unscaledDeltaTime; 
            float val = Mathf.Lerp(from, to, t / time);
            fadeMaterial.SetFloat(k_IntensityProp, val);
            yield return null;
        }

        fadeMaterial.SetFloat(k_IntensityProp, to);
    }
}
