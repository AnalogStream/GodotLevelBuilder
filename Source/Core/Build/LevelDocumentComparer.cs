using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Build;

/// <summary>
/// Structural comparison of two LevelDocuments. Used by the round-trip test to
/// assert a saved-then-loaded document matches the original. Returns a list of
/// human-readable differences (empty list = equal).
/// </summary>
public static class LevelDocumentComparer
{
    public static List<string> Diff(LevelDocument a, LevelDocument b)
    {
        var diffs = new List<string>();
        if (a == null || b == null)
        {
            diffs.Add($"null document (a={a != null}, b={b != null})");
            return diffs;
        }

        Eq(diffs, "SchemaVersion", a.SchemaVersion, b.SchemaVersion);
        Eq(diffs, "Name", a.Name, b.Name);
        EqF(diffs, "Grid.CellSize", a.Grid.CellSize, b.Grid.CellSize);

        Count(diffs, "Materials.Entries", a.Materials.Entries.Count, b.Materials.Entries.Count);
        for (int i = 0; i < a.Materials.Entries.Count && i < b.Materials.Entries.Count; i++)
        {
            MaterialEntry ma = a.Materials.Entries[i], mb = b.Materials.Entries[i];
            Eq(diffs, $"Material[{i}].Id", ma.Id, mb.Id);
            Eq(diffs, $"Material[{i}].MaterialPath", ma.MaterialPath, mb.MaterialPath);
        }

        Count(diffs, "Storeys", a.Storeys.Count, b.Storeys.Count);
        for (int i = 0; i < a.Storeys.Count && i < b.Storeys.Count; i++)
        {
            DiffStorey(diffs, i, a.Storeys[i], b.Storeys[i]);
        }

        return diffs;
    }

    private static void DiffStorey(List<string> diffs, int idx, StoreyData a, StoreyData b)
    {
        string p = $"Storey[{idx}]";
        Eq(diffs, $"{p}.Id", a.Id, b.Id);
        Eq(diffs, $"{p}.Name", a.Name, b.Name);
        EqF(diffs, $"{p}.BaseElevation", a.BaseElevation, b.BaseElevation);
        EqF(diffs, $"{p}.Height", a.Height, b.Height);

        Count(diffs, $"{p}.Instances", a.Instances.Count, b.Instances.Count);
        for (int j = 0; j < a.Instances.Count && j < b.Instances.Count; j++)
        {
            DiffInstance(diffs, $"{p}.Instance[{j}]", a.Instances[j], b.Instances[j]);
        }
    }

    private static void DiffInstance(List<string> diffs, string p, PrimitiveInstanceData a, PrimitiveInstanceData b)
    {
        Eq(diffs, $"{p}.Id", a.Id, b.Id);
        Eq(diffs, $"{p}.PrimitiveType", a.PrimitiveType, b.PrimitiveType);
        if (!a.LocalTransform.IsEqualApprox(b.LocalTransform))
            diffs.Add($"{p}.LocalTransform: {a.LocalTransform} != {b.LocalTransform}");
        DiffDict(diffs, $"{p}.Parameters", a.Parameters, b.Parameters);
        DiffDict(diffs, $"{p}.MaterialSlots", a.MaterialSlots, b.MaterialSlots);
        Count(diffs, $"{p}.Openings", a.Openings.Count, b.Openings.Count);
    }

    private static void DiffDict(List<string> diffs, string p, Dictionary a, Dictionary b)
    {
        if (a.Count != b.Count)
        {
            diffs.Add($"{p}.Count: {a.Count} != {b.Count}");
            return;
        }
        foreach (Variant key in a.Keys)
        {
            if (!b.ContainsKey(key))
            {
                diffs.Add($"{p}: missing key '{key}'");
                continue;
            }
            Variant va = a[key], vb = b[key];
            if (IsNumeric(va) && IsNumeric(vb))
            {
                // Floats widen/round through the .tres text format — compare with tolerance.
                if (System.Math.Abs(va.AsDouble() - vb.AsDouble()) > 1e-5)
                    diffs.Add($"{p}['{key}']: {va} != {vb}");
            }
            else if (va.ToString() != vb.ToString())
            {
                diffs.Add($"{p}['{key}']: '{va}' != '{vb}'");
            }
        }
    }

    private static bool IsNumeric(Variant v)
        => v.VariantType is Variant.Type.Float or Variant.Type.Int;

    private static void Eq(List<string> diffs, string label, string a, string b)
    {
        if (a != b) diffs.Add($"{label}: '{a}' != '{b}'");
    }

    private static void EqF(List<string> diffs, string label, float a, float b)
    {
        if (!Mathf.IsEqualApprox(a, b)) diffs.Add($"{label}: {a} != {b}");
    }

    private static void Count(List<string> diffs, string label, int a, int b)
    {
        if (a != b) diffs.Add($"{label}.Count: {a} != {b}");
    }
}
