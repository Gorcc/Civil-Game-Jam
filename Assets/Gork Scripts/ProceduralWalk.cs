using UnityEngine;

/// <summary>
/// Walk cycle without animation clips: rotates the humanoid bones of a T-posed
/// character (arms down and swinging, legs stepping, knees bending).
/// Rotations are applied in the character root's space on top of the captured T-pose,
/// so it works for any Synty humanoid that faces +Z.
/// </summary>
public class ProceduralWalk : WalkAnimator
{
    public float armDown = 74f;
    public float armSwing = 24f;
    public float stride = 26f;
    public float kneeBend = 42f;
    public float elbowBend = 12f;
    public float strideLength = 1.5f;   // meters per full cycle (two steps)

    Transform lUpperArm, rUpperArm, lLowerArm, rLowerArm, lUpperLeg, rUpperLeg, lLowerLeg, rLowerLeg, spine;
    Quaternion rlUA, rrUA, rlLA, rrLA, rlUL, rrUL, rlLL, rrLL, rSpine;
    float phase;
    bool ready;

    void Awake()
    {
        var anim = GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman) return;

        lUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        lLowerArm = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rLowerArm = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        lUpperLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rUpperLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        lLowerLeg = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rLowerLeg = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        // No controller: the Animator would only fight our bone rotations.
        anim.enabled = false;

        rlUA = Rest(lUpperArm); rrUA = Rest(rUpperArm);
        rlLA = Rest(lLowerArm); rrLA = Rest(rLowerArm);
        rlUL = Rest(lUpperLeg); rrUL = Rest(rUpperLeg);
        rlLL = Rest(lLowerLeg); rrLL = Rest(rLowerLeg);
        rSpine = Rest(spine);
        phase = Random.Range(0f, Mathf.PI * 2f);
        ready = lUpperArm && rUpperArm && lUpperLeg && rUpperLeg && lLowerLeg && rLowerLeg;
    }

    /// <summary>Bone rotation relative to the character root, in the T-pose.</summary>
    Quaternion Rest(Transform bone) => bone != null ? Quaternion.Inverse(transform.rotation) * bone.rotation : Quaternion.identity;

    void Set(Transform bone, Quaternion delta, Quaternion rest)
    {
        if (bone != null) bone.rotation = transform.rotation * delta * rest;
    }

    void LateUpdate()
    {
        if (!ready) return;

        phase += Time.deltaTime * (speed / strideLength) * Mathf.PI * 2f * Mathf.Max(amount, 0.0001f);
        float s = Mathf.Sin(phase) * amount;
        float c = Mathf.Cos(phase);

        Vector3 X = Vector3.right, Z = Vector3.forward, Y = Vector3.up;

        // Arms hang down (rotate about the forward axis), then swing opposite to the legs.
        Quaternion lArm = Quaternion.AngleAxis(s * armSwing, X) * Quaternion.AngleAxis(armDown, Z);
        Quaternion rArm = Quaternion.AngleAxis(-s * armSwing, X) * Quaternion.AngleAxis(-armDown, Z);
        Set(lUpperArm, lArm, rlUA);
        Set(rUpperArm, rArm, rrUA);
        Set(lLowerArm, lArm * Quaternion.AngleAxis(-elbowBend, X), rlLA);
        Set(rLowerArm, rArm * Quaternion.AngleAxis(-elbowBend, X), rrLA);

        // Legs: negative angle about X swings forward; knee bends while the leg swings through.
        Quaternion lLeg = Quaternion.AngleAxis(-s * stride, X);
        Quaternion rLeg = Quaternion.AngleAxis(s * stride, X);
        Set(lUpperLeg, lLeg, rlUL);
        Set(rUpperLeg, rLeg, rrUL);
        Set(lLowerLeg, lLeg * Quaternion.AngleAxis(Mathf.Max(0f, c) * kneeBend * amount, X), rlLL);
        Set(rLowerLeg, rLeg * Quaternion.AngleAxis(Mathf.Max(0f, -c) * kneeBend * amount, X), rrLL);

        // Slight torso twist.
        Set(spine, Quaternion.AngleAxis(s * 4f, Y), rSpine);
    }
}
