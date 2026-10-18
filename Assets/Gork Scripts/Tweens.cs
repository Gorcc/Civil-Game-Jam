using System;
using System.Collections;
using UnityEngine;

public static class Ease
{
    public static float Linear(float t) => t;
    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    public static float InCubic(float t) => t * t * t;
    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

    public static float OutBack(float t)
    {
        const float s = 1.70158f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    public static float InBack(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }
}

/// <summary>Coroutine tweens. Run with StartCoroutine on any MonoBehaviour.</summary>
public static class Tweens
{
    public static IEnumerator Scale(Transform t, Vector3 from, Vector3 to, float duration, Func<float, float> ease, float delay = 0f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (t == null) yield break;
        t.localScale = from;
        float e = 0f;
        while (e < duration)
        {
            if (t == null) yield break;
            e += Time.deltaTime;
            t.localScale = Vector3.LerpUnclamped(from, to, ease(Mathf.Clamp01(e / duration)));
            yield return null;
        }
        if (t != null) t.localScale = to;
    }

    public static IEnumerator PopIn(Transform t, float duration, float delay = 0f)
    {
        if (t == null) yield break;
        Vector3 target = t.localScale;
        t.localScale = Vector3.zero;
        yield return Scale(t, Vector3.zero, target, duration, Ease.OutBack, delay);
    }

    public static IEnumerator PopOut(Transform t, float duration, float delay = 0f, bool destroy = true)
    {
        if (t == null) yield break;
        yield return Scale(t, t.localScale, Vector3.zero, duration, Ease.InBack, delay);
        if (destroy && t != null) UnityEngine.Object.Destroy(t.gameObject);
    }
}
