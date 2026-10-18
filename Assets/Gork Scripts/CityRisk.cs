using System.Collections.Generic;
using UnityEngine;

public enum CityRiskKind { Earthquake, Flood, Fire }

/// <summary>Shared disaster damage exposure (not occurrence probability); changes are percentage points.</summary>
public static class CityRisk
{
    public readonly struct Line
    {
        public readonly string label;
        public readonly float amount;
        public Line(string label, float amount) { this.label = label; this.amount = amount; }
    }

    public static float Calculate(CityRiskKind kind, Building[] buildings, List<Line> lines = null)
    {
        lines?.Clear();
        float risk;
        if (kind == CityRiskKind.Earthquake)
        {
            int count = 0;
            float total = 0f, foundation = 0f, floors = 0f, age = 0f, reinforcement = 0f, damage = 0f, ground = 0f;
            foreach (var b in buildings)
            {
                if (b == null) continue;
                count++;
                total += b.Resistance;
                if (b.definition == null) continue;
                foundation += b.definition.baseResistance;
                floors += (b.floors - 1) * b.definition.resistancePerExtraFloor;
                age += b.ageYears / 2;
                reinforcement += b.reinforcement * b.definition.resistancePerReinforcement;
                damage += b.damage;
                ground += b.Plot != null ? b.Plot.Terrain.resistanceModifier : 0;
            }
            if (count == 0) { risk = 42f; Add(lines, "Başlangıç deprem riski", risk); }
            else
            {
                Add(lines, "Yapıların temel dayanıklılığı", 100f - foundation / count);
                Add(lines, "Ek katlar", floors / count);
                Add(lines, "Bina yaşı", age / count);
                Add(lines, "Yapısal hasar", damage / count);
                Add(lines, "Güçlendirmeler", -reinforcement / count);
                Add(lines, "Zemin dayanıklılığı", -ground / count);
                float raw = 100f - (foundation - floors - age + reinforcement - damage + ground) / count;
                float actual = 100f - total / count;
                Add(lines, "Bina dayanıklılığı sınırları", actual - raw);
                risk = Mathf.Clamp(actual, 15f, 95f);
                Add(lines, "Temel risk sınırları", risk - actual);
            }
        }
        else if (kind == CityRiskKind.Fire)
        {
            bool station = false;
            foreach (var b in buildings)
                if (b != null && b.definition != null && (b.definition.service == ServiceType.FireStation ||
                    (!string.IsNullOrEmpty(b.definition.role) && b.definition.role.ToLowerInvariant().Contains("yangın"))))
                    station = true;
            risk = station ? 22f : 58f;
            Add(lines, "Temel yangın riski", 58f);
            if (station) Add(lines, "İtfaiye bulunması", -36f);
        }
        else { risk = 32f; Add(lines, "Temel sel riski", risk); }

        var manager = WeeklyDecisionManager.Instance;
        if (manager != null)
        {
            float modifier = kind == CityRiskKind.Earthquake ? manager.EarthquakeRiskModifier :
                kind == CityRiskKind.Flood ? manager.FloodRiskModifier : manager.FireRiskModifier;
            if (lines != null)
            {
                float accounted = 0f;
                foreach (var decision in manager.decisionHistory)
                {
                    if (decision.durationDays > 0) continue;
                    float value = Delta(decision, kind);
                    Add(lines, decision.title, value);
                    accounted += value;
                }
                foreach (var active in manager.activeTemporaryDecisions)
                {
                    if (active.daysRemaining <= 0) continue;
                    float value = Delta(active.decision, kind);
                    Add(lines, active.decision.title + $" ({active.daysRemaining} gün kaldı)", value);
                    accounted += value;
                }
                Add(lines, "Diğer karar etkileri", modifier - accounted);
            }
            risk += modifier;
        }
        var maintenance = MunicipalEventManager.Instance;
        float vegetation = maintenance == null ? 0f : kind == CityRiskKind.Fire ? maintenance.VegetationFireDamage
            : kind == CityRiskKind.Flood ? maintenance.VegetationFloodDamage : 0f;
        Add(lines, "Temizlenmemiş kuru ot ve ölü ağaçlar", vegetation);
        risk += vegetation;
        float litter = maintenance == null ? 0f : kind == CityRiskKind.Fire ? maintenance.LitterFireDamage
            : kind == CityRiskKind.Flood ? maintenance.LitterFloodDamage : 0f;
        Add(lines, "Temizlenmemiş çöp birikintileri", litter);
        risk += litter;
        float utility = maintenance != null && kind == CityRiskKind.Fire ? maintenance.UtilityFireDamage : 0f;
        Add(lines, "Bakım bekleyen elektrik tesisatı", utility);
        risk += utility;
        float result = Mathf.Clamp(risk, 0f, 100f);
        Add(lines, "0–100 risk sınırı", result - risk);
        return result;
    }

    static float Delta(CityDecision decision, CityRiskKind kind) => kind == CityRiskKind.Earthquake ? decision.earthquakeRiskDelta :
        kind == CityRiskKind.Flood ? decision.floodRiskDelta : decision.fireRiskDelta;

    static void Add(List<Line> lines, string label, float value)
    {
        if (lines != null && Mathf.Abs(value) > .00001f) lines.Add(new Line(label, value));
    }
}
