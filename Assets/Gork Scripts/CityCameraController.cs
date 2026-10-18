using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// City-builder style camera rig. This transform is the pivot on the ground;
/// the camera is a child orbiting it at a fixed pitch.
/// WASD/arrows/middle-drag: pan, Q/E or right-drag: rotate, scroll: zoom.
/// </summary>
public class CityCameraController : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;

    [Header("Pan")]
    public float panSpeed = 40f;
    public bool edgePan = false;
    public float edgeSize = 12f;

    [Header("Rotate")]
    public float keyRotateSpeed = 120f;
    public float mouseRotateSpeed = 0.25f;

    [Header("Zoom")]
    public float minDistance = 15f;
    public float maxDistance = 140f;
    [Range(0.02f, 0.5f)] public float zoomStep = 0.12f;
    public float minPitch = 35f;
    public float maxPitch = 60f;

    [Header("Start")]
    public float startDistance = 80f;
    public float startYaw = 45f;

    [Header("Smoothing")]
    public float smooth = 10f;

    [Header("Bounds (XZ)")]
    public bool useBounds = true;
    public Vector2 boundsMin = new Vector2(0f, 0f);
    public Vector2 boundsMax = new Vector2(100f, 100f);

    Vector3 targetPos;
    float yaw, targetYaw;
    float distance, targetDistance;
    bool coveragePreview;
    float coverageDistance, previousDistance;
    Vector3 previousPosition;
    bool dragPanning;
    Vector3 dragStartWorld;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        targetPos = transform.position;
        ResetView();
    }

    void OnValidate()
    {
        if (cam != null && !Application.isPlaying) ResetView();
    }

    void ResetView()
    {
        yaw = targetYaw = startYaw;
        distance = targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        ApplyTransform();
    }

    void Update()
    {
        if (PlayerDetails.IsSetupOpen || SettingsUI.IsOpen)
        {
            dragPanning = false;
            targetPos = transform.position;
            targetYaw = yaw;
            targetDistance = distance;
            return;
        }
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        float dt = Time.unscaledDeltaTime;

        // Keyboard pan, relative to camera yaw.
        Vector2 move = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

            if (kb.qKey.isPressed) targetYaw += keyRotateSpeed * dt;
            if (kb.eKey.isPressed) targetYaw -= keyRotateSpeed * dt;
        }

        if (edgePan && Application.isFocused)
        {
            Vector2 mp = mouse.position.ReadValue();
            if (mp.x < edgeSize) move.x -= 1f;
            else if (mp.x > Screen.width - edgeSize) move.x += 1f;
            if (mp.y < edgeSize) move.y -= 1f;
            else if (mp.y > Screen.height - edgeSize) move.y += 1f;
        }

        if (move.sqrMagnitude > 0f)
        {
            // Pan faster when zoomed out.
            float zoomFactor = Mathf.Lerp(0.5f, 1.5f, Mathf.InverseLerp(minDistance, maxDistance, distance));
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 dir = yawRot * new Vector3(move.x, 0f, move.y).normalized;
            targetPos += dir * panSpeed * zoomFactor * dt;
        }

        // Middle-drag pan: keep the grabbed ground point under the cursor.
        if (mouse.middleButton.wasPressedThisFrame && !overUI && GroundPoint(mouse.position.ReadValue(), out dragStartWorld))
            dragPanning = true;
        if (!mouse.middleButton.isPressed)
            dragPanning = false;
        if (dragPanning && GroundPoint(mouse.position.ReadValue(), out Vector3 current))
        {
            Vector3 delta = dragStartWorld - current;
            delta.y = 0f;
            targetPos += delta;
            transform.position += delta;
        }

        // Right-drag rotate.
        if (mouse.rightButton.isPressed && !overUI)
            targetYaw += mouse.delta.ReadValue().x * mouseRotateSpeed;

        // Scroll zoom.
        float scroll = mouse.scroll.ReadValue().y;
        if (!overUI && Mathf.Abs(scroll) > 0.01f)
            targetDistance *= 1f - Mathf.Sign(scroll) * zoomStep;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, coveragePreview ? Mathf.Max(maxDistance, coverageDistance) : maxDistance);

        if (useBounds && !coveragePreview)
        {
            targetPos.x = Mathf.Clamp(targetPos.x, boundsMin.x, boundsMax.x);
            targetPos.z = Mathf.Clamp(targetPos.z, boundsMin.y, boundsMax.y);
        }

        float t = 1f - Mathf.Exp(-smooth * dt);
        if (!dragPanning) transform.position = Vector3.Lerp(transform.position, targetPos, t);
        yaw = Mathf.LerpAngle(yaw, targetYaw, t);
        distance = Mathf.Lerp(distance, targetDistance, t);
        ApplyTransform();
    }

    void ApplyTransform()
    {
        if (cam == null) return;
        float pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.InverseLerp(minDistance, maxDistance, distance));
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);
        cam.transform.localPosition = pitchRot * (Vector3.back * distance) + ShakeOffset();
        cam.transform.localRotation = pitchRot;
    }

    // ---------- Screen shake ----------

    float shakeAmplitude, shakeDuration, shakeTime;

    /// <summary>Shakes the camera; amplitude in meters at mid zoom, fades out over duration.</summary>
    public void Shake(float amplitude, float duration)
    {
        // Keep the stronger of an ongoing and a new shake.
        float remaining = shakeTime > 0f ? shakeAmplitude * (shakeTime / shakeDuration) : 0f;
        if (amplitude < remaining) return;
        shakeAmplitude = amplitude;
        shakeDuration = Mathf.Max(0.05f, duration);
        shakeTime = shakeDuration;
    }

    Vector3 ShakeOffset()
    {
        if (shakeTime <= 0f) return Vector3.zero;
        shakeTime -= Time.unscaledDeltaTime;
        float fade = Mathf.Clamp01(shakeTime / shakeDuration);
        // Scale with zoom so it reads the same near and far.
        float amp = shakeAmplitude * fade * Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(minDistance, maxDistance, distance));
        float t = Time.unscaledTime * 22f;
        return new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, t) - 0.5f, Mathf.PerlinNoise(t, 0.7f) - 0.5f) * 2f * amp;
    }

    bool GroundPoint(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screenPos);
        var ground = new Plane(Vector3.up, Vector3.zero);
        if (!ground.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    public void SetBounds(Vector3 min, Vector3 max)
    {
        boundsMin = new Vector2(min.x, min.z);
        boundsMax = new Vector2(max.x, max.z);
    }

    public void BeginCoveragePreview(Vector3 center, float radius)
    {
        if (!coveragePreview)
        {
            previousDistance = targetDistance;
            previousPosition = targetPos;
        }
        coveragePreview = true;
        dragPanning = false;
        radius = Mathf.Max(1f, radius);
        float halfVertical = (cam != null ? cam.fieldOfView : 60f) * .5f * Mathf.Deg2Rad;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        // Reserve the bottom confirmation panel and top HUD; fit the entire radius with margin.
        float usableAngle = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Min(.52f, aspect * .8f));
        coverageDistance = Mathf.Max(minDistance, Mathf.Max(maxDistance, radius * 1.15f / Mathf.Sin(usableAngle)) * .5f);
        targetDistance = coverageDistance;
        Vector3 forward = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
        targetPos = new Vector3(center.x, 0f, center.z) - forward * radius * .55f;
    }

    public void EndCoveragePreview()
    {
        if (!coveragePreview) return;
        coveragePreview = false;
        targetDistance = Mathf.Clamp(previousDistance, minDistance, maxDistance);
        targetPos = previousPosition;
    }

    public void FocusOnClose(Vector3 worldPos)
    {
        dragPanning = false;
        FocusOn(worldPos);
        targetDistance = Mathf.Clamp(Mathf.Min(targetDistance, 25f), minDistance, maxDistance);
    }

    public void FocusOn(Vector3 worldPos, bool instant = false)
    {
        targetPos = new Vector3(worldPos.x, 0f, worldPos.z);
        if (instant)
        {
            transform.position = targetPos;
            if (!Application.isPlaying) ResetView();
            else ApplyTransform();
        }
    }
}
