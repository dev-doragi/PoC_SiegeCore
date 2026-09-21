#if UNITY_EDITOR
using System.Collections.Generic;
using SiegeCore.Rat;
using UnityEditor;
using UnityEngine;

public static class RatLoadoutValidation
{
    private const string DataPath = "Assets/07.Data/RatPoc2/";

    [MenuItem("SiegeCore/Validate Rat Loadouts")]
    public static void Run()
    {
        RatDefinition basic = Load<RatDefinition>(DataPath + "Rat_Basic.asset");
        RatDefinition bomber = Load<RatDefinition>(DataPath + "Rat_Bomber.asset");
        RatDefinition tank = Load<RatDefinition>(DataPath + "Rat_Tank.asset");
        RatCatalog catalog = Load<RatCatalog>(DataPath + "RatCatalog.asset");

        Check(catalog.HasValidFallback, "Catalog fallback");
        Check(catalog.Entries != null && catalog.Entries.Length == 9, "Catalog entries");

        RatLoadoutRuntime runtime = new RatLoadoutRuntime();
        List<RatLoadoutEntry> entries = new List<RatLoadoutEntry>
        {
            new RatLoadoutEntry { Definition = basic, Copies = 2 },
            new RatLoadoutEntry { Definition = bomber, Copies = 1 },
            new RatLoadoutEntry { Definition = tank, Copies = 1 }
        };

        runtime.ReplaceEntries(entries, basic);
        HashSet<RatDefinition> drawnDefinitions = new HashSet<RatDefinition>();
        for (int index = 0; index < 4; index++)
        {
            Check(runtime.TryPeekNext(out RatDefinition definition), "Deck draw " + index);
            drawnDefinitions.Add(definition);
            runtime.ConsumeNext();
        }

        Check(drawnDefinitions.Contains(basic), "Deck contains Basic");
        Check(drawnDefinitions.Contains(bomber), "Deck contains Bomber");
        Check(drawnDefinitions.Contains(tank), "Deck contains Tank");
        Check(runtime.TryPeekNext(out RatDefinition recycled), "Deck recycles discard");
        Check(recycled != null, "Recycled card is valid");

        runtime.ReplaceEntries(new List<RatLoadoutEntry>(), basic);
        Check(runtime.UsingFallback, "Empty loadout fallback");
        Check(runtime.TryPeekNext(out RatDefinition fallback), "Fallback draw");
        Check(fallback == basic, "Fallback is Basic Rank1");

        Debug.Log("RAT_LOADOUT_VALIDATION_OK");
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.InvalidOperationException("Missing asset: " + path);
        return asset;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new System.InvalidOperationException("Rat loadout validation failed: " + label);
    }
}
#endif
