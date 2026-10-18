using System.Collections.Generic;
using UnityEngine;

/// <summary>Ground type of a plot. Changes land price, building cost, rent and earthquake risk.</summary>
public enum PlotTerrain
{
    Normal,  // Sağlam, verimli zemin
    Beach,   // Kumsal (deniz kenarı)
    Swamp,   // Bataklık
}

public struct TerrainInfo
{
    public string name;
    public string description;
    public float landPriceMultiplier;
    public float buildCostMultiplier;
    public float rentMultiplier;
    public int resistanceModifier;
    [Tooltip("How much stronger an earthquake hits buildings on this ground.")]
    public float quakeMultiplier;
    public Color color;

    public static TerrainInfo Get(PlotTerrain t)
    {
        switch (t)
        {
            case PlotTerrain.Beach:
                return new TerrainInfo
                {
                    name = "Kumsal",
                    description = "Deniz manzarası kirayı artırır ama kumlu zemin sarsıntıyı büyütür.",
                    landPriceMultiplier = 1.5f,
                    buildCostMultiplier = 1.1f,
                    rentMultiplier = 1.3f,
                    resistanceModifier = -5,
                    quakeMultiplier = 1.5f,
                    color = new Color(0.95f, 0.82f, 0.5f),
                };
            case PlotTerrain.Swamp:
                return new TerrainInfo
                {
                    name = "Bataklık",
                    description = "Ucuz ve kolay inşa edilir ama yumuşak zemin depremde sıvılaşır.",
                    landPriceMultiplier = 0.5f,
                    buildCostMultiplier = 0.85f,
                    rentMultiplier = 0.8f,
                    resistanceModifier = -10,
                    quakeMultiplier = 2f,
                    color = new Color(0.55f, 0.65f, 0.4f),
                };
            default:
                return new TerrainInfo
                {
                    name = "Sağlam zemin",
                    description = "Verimli, sağlam toprak. Depremde en güvenli zemin.",
                    landPriceMultiplier = 1f,
                    buildCostMultiplier = 1f,
                    rentMultiplier = 1f,
                    resistanceModifier = 0,
                    quakeMultiplier = 1f,
                    color = new Color(0.55f, 0.85f, 0.45f),
                };
        }
    }

    /// <summary>Short effect list for tooltips/cards, e.g. "Deprem etkisi 2x", "İnşaat %15 ucuz".</summary>
    public List<string> Effects()
    {
        var e = new List<string>();
        if (quakeMultiplier != 1f) e.Add($"Deprem etkisi {quakeMultiplier:0.#}x");
        if (buildCostMultiplier != 1f) e.Add($"İnşaat {Pct(buildCostMultiplier, "pahalı", "ucuz")}");
        if (landPriceMultiplier != 1f) e.Add($"Arsa {Pct(landPriceMultiplier, "pahalı", "ucuz")}");
        if (rentMultiplier != 1f) e.Add($"Kira {Pct(rentMultiplier, "yüksek", "düşük")}");
        if (resistanceModifier != 0) e.Add($"Dayanıklılık {resistanceModifier}");
        return e;
    }

    static string Pct(float m, string up, string down)
    {
        int p = Mathf.RoundToInt(Mathf.Abs(m - 1f) * 100f);
        return $"%{p} {(m > 1f ? up : down)}";
    }
}
