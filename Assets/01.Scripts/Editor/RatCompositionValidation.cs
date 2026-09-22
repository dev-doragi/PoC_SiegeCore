using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SiegeCore.Cannon;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEditor;
using UnityEngine;

public static class RatCompositionValidation
{
    private const string ProbePath = "Assets/99.Test/RatComposition/Rat_CompositionProbe.prefab";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SiegeCore/Create Composition Validation Prefab")]
    public static void CreateProbe()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/99.Test")) AssetDatabase.CreateFolder("Assets", "99.Test");
        if (!AssetDatabase.IsValidFolder("Assets/99.Test/RatComposition")) AssetDatabase.CreateFolder("Assets/99.Test", "RatComposition");
        GameObject contents = PrefabUtility.LoadPrefabContents("Assets/02.Prefabs/RatPoc2/Rat_Bomber.prefab");
        try
        {
            UnityEngine.Object.DestroyImmediate(contents.GetComponent<RatGroundAI>());
            UnityEngine.Object.DestroyImmediate(contents.GetComponent<RatCollisionFusion>());
            contents.name = "Rat_CompositionProbe";
            PrefabUtility.SaveAsPrefabAsset(contents, ProbePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    public static void Run(RatFactory factory)
    {
        RatDefinition definition = factory.FallbackDefinition;
        Vector3 position = factory.Battlefield.NearestFloor(Vector3.zero, VehicleSide.Ally);
        CarryController carrier = UnityEngine.Object.FindFirstObjectByType<CarryController>();
        SiegeCore.Cannon.Cannon cannon = UnityEngine.Object.FindObjectsByType<SiegeCore.Cannon.Cannon>(FindObjectsSortMode.None)
            .First(item => item.IsInstalledFor(VehicleSide.Ally));
        RatAgent rat = factory.Spawn(definition, VehicleSide.Ally, position);
        Check(rat.IsSpawned && RatAgent.Active.Contains(rat) && rat.Health == definition.Ground.Health, "Only initialized rats are registered");
        Transform parent = rat.transform.parent;
        int history = rat.StateHistory.Count;
        Check(!rat.TryCatch(null, 0.1f) && !rat.TryThrow(Vector2.zero, position, null)
            && rat.transform.parent == parent && rat.StateHistory.Count == history, "Rejected carry requests leave state and ownership unchanged");

        Check(rat.TryAttachToCarrySlot(carrier.GetHoldPoint(0)), "Attach through RatAgent");
        List<ICarryable> held = (List<ICarryable>)Get(carrier, "_heldObjects");
        held.Add(rat.Carryable);
        typeof(CarryController).GetMethod("CaptureCarrySorting", Fields).Invoke(carrier, new object[] { rat.Carryable });
        history = rat.StateHistory.Count;
        Check(!rat.TryThrow(Vector2.zero, position, null) && rat.State == RatState.Carried
            && carrier.HeldCount == 1 && rat.StateHistory.Count == history, "Rejected throw retains carried object");
        rat.Release();
        Check(held.Count == 0 && !rat.IsSpawned && !RatAgent.Active.Contains(rat), "Pool return immediately releases carrier ownership");

        for (int cycle = 0; cycle < 2; cycle++)
        {
            rat = factory.Spawn(definition, VehicleSide.Enemy, position);
            Check(rat.State == RatState.Idle && rat.Motion.Height == 0f && !rat.Motion.IsFusionLocked
                && !rat.Motion.IsHitStopped && rat.AttackSide == VehicleSide.Enemy
                && rat.ProjectileSourceSlot == null, "Pool reset cycle " + cycle);
            Check(rat.TryLoadIntoCannon(cannon), "Idle load through RatAgent");
            int count = cannon.LoadedCount;
            rat.Release();
            Check(cannon.LoadedCount == count - 1, "Pool return immediately releases magazine ownership");
        }

        List<RatAgent> loaded = new List<RatAgent>();
        try
        {
            while (!cannon.IsFull && loaded.Count < 32)
            {
                RatAgent ammo = factory.Spawn(definition, VehicleSide.Ally, position);
                loaded.Add(ammo);
                Check(ammo.TryLoadIntoCannon(cannon), "Fill magazine");
            }
            Check(cannon.IsFull, "Magazine reaches configured capacity");
            rat = factory.Spawn(definition, VehicleSide.Enemy, position);
            parent = rat.transform.parent;
            history = rat.StateHistory.Count;
            Check(!rat.TryLoadIntoCannon(cannon) && rat.State == RatState.Idle
                && !rat.Motion.IsAirborne && rat.transform.parent == parent && rat.StateHistory.Count == history,
                "Full magazine rejects without temporary flight or reparenting");
            rat.Release();
        }
        finally { foreach (RatAgent ammo in loaded) ammo.Release(); }

        ValidateOptionalComposition(factory, cannon, position);
        Debug.Log("RAT_COMPOSITION_VALIDATION_OK");
    }

    private static void ValidateOptionalComposition(RatFactory factory, SiegeCore.Cannon.Cannon cannon, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath);
        Check(prefab != null && RatFactory.HasValidComposition(prefab.GetComponent<RatAgent>()), "Composed prefab passes validation");
        RatDefinition definition = prefab.GetComponent<RatAgent>().Definition;
        Dictionary<RatDefinition, PoolDefinition> lookup = (Dictionary<RatDefinition, PoolDefinition>)Get(factory, "_poolLookup");
        PoolDefinition previous = lookup[definition];
        PoolDefinition probePool = ScriptableObject.CreateInstance<PoolDefinition>();
        SerializedObject settings = new SerializedObject(probePool);
        settings.FindProperty("_prefab").objectReferenceValue = prefab;
        settings.ApplyModifiedPropertiesWithoutUndo();
        lookup[definition] = probePool;
        RatAgent probe = null;
        try
        {
            probe = factory.Spawn(definition, VehicleSide.Ally, position);
            Check(probe != null && probe.GroundBehaviour == null && probe.GetComponent<RatCollisionFusion>() == null,
                "Factory accepts optional ground behaviour and fusion");
            Check(probe.TryLoadIntoCannon(cannon), "Composed Rat loads without cannon changes");
            Check(probe.LaunchFromCannon(position, position + Vector3.right * 5f, VehicleSide.Ally,
                cannon.SourceSlot, CannonTrajectoryType.Straight, 3f, 0f), "Composed Rat fires existing projectile ability");
            probe.Release();
            probe = factory.Spawn(definition, VehicleSide.Enemy, position);
            Check(probe.State == RatState.Idle && probe.AttackSide == VehicleSide.Enemy, "Composed Rat survives pool reuse");
        }
        finally
        {
            if (probe != null) probe.Release();
            lookup[definition] = previous;
            // The scene pool retains this definition until scene teardown.
        }
    }

    private static object Get(object target, string name) { return target.GetType().GetField(name, Fields).GetValue(target); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("COMPOSITION_CHECK " + message);
    }
}
