using UnityEngine;

/// <summary>Common inputs for walk animations: how much the character walks (0..1) and how fast.</summary>
public abstract class WalkAnimator : MonoBehaviour
{
    [Tooltip("0 = standing, 1 = full walk.")]
    [Range(0f, 1f)] public float amount = 1f;
    [Tooltip("Ground speed in m/s; drives the step rate.")]
    public float speed = 1.4f;
}
