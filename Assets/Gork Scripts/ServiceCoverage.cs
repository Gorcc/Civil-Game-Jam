using UnityEngine;

/// <summary>The same horizontal radius is used for both service decisions and the selection ring.</summary>
public static class ServiceCoverage
{
    public static bool HasRange(BuildingDefinition definition) => definition != null
        && (definition.service == ServiceType.AssemblyArea || definition.service == ServiceType.FireStation);
    public static bool HasRange(Building b) => b != null && HasRange(b.definition);
    public static float Radius(Building b) => HasRange(b) ? Mathf.Max(1f, b.definition.emergencyRadius) : 0;
    public static bool Covers(Building service, Vector3 point)
    {
        if (!HasRange(service)) return false;
        Vector3 delta = service.transform.position - point; delta.y = 0;
        float radius = Radius(service); return delta.sqrMagnitude <= radius * radius;
    }
}
