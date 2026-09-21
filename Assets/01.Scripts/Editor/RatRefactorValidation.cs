using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SiegeCore.Cannon;
using SiegeCore.Player;
using SiegeCore.Projectile;
using SiegeCore.Rat;
using UnityEditor;
using UnityEngine;

public static class RatRefactorValidation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(RatFactory factory)
    {
        RatDefinition[] definitions = {
            Load<RatDefinition>("Rat_Basic"), Load<RatDefinition>("Rat_BB"), Load<RatDefinition>("Rat_BBB")
        };
        IRatMergeResolver resolver = Load<RatMergeResolver>("RatMergeResolver");
        for (int first = 0; first < 3; first++)
        {
            for (int second = 0; second < 3; second++)
            {
                bool resolved = resolver.TryResolve(definitions[first], definitions[second], out RatDefinition result);
                int sum = first + second + 2;
                Check(resolved == (sum <= 3) && (!resolved || result == definitions[sum - 1]),
                    "Resolver pair " + (first + 1) + "+" + (second + 1));
            }
        }
        Check(!resolver.TryResolve(null, definitions[0], out RatDefinition ignored), "Null merge rejected");
        RatDefinition unknown = ScriptableObject.CreateInstance<RatDefinition>();
        Check(!resolver.TryResolve(unknown, definitions[0], out ignored), "Unregistered merge rejected");
        Check(factory.Spawn(unknown, VehicleSide.Ally, Vector3.zero) == null, "Unregistered spawn rejected");
        UnityEngine.Object.DestroyImmediate(unknown);

        Vector3 safe = FindSafeFloor(factory);
        float[] impulses = { 1f, 0.95f, 0.8f };
        for (int index = 0; index < 3; index++)
        {
            RatDefinition definition = definitions[index];
            Check(definition.Type == RatType.Basic && (int)definition.Rank == index + 1
                && definition.Ground.Health == 10f * (index + 1)
                && definition.Ground.AttackDamage == 3f * (index + 1)
                && definition.Ground.AttackInterval == 1f && definition.Ground.MoveSpeed == 2f
                && definition.Ground.AttackRange == 1.1f && definition.Ground.DetectionRadius == 4f
                && definition.Projectile.Damage == 10f * (index + 1)
                && (int)definition.Projectile.Weight == index
                && definition.VerticalImpulseMultiplier == impulses[index], "Migrated stats rank " + (index + 1));
            RatAgent rat = factory.Spawn(definition, VehicleSide.Ally, safe);
            Rigidbody2D body = rat.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.right * 12f;
            float perRank = (float)Get(rat.Carryable, "_flightDecelerationPerRank");
            Call(rat.Carryable, "ApplyRankFlightDeceleration");
            Check(Mathf.Approximately(body.linearVelocity.x, 12f - perRank * index * Time.fixedDeltaTime),
                "Flight deceleration rank " + (index + 1));
            rat.Release();
        }

        RatDispenser supply = UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None)
            .First(item => item.Faction == VehicleSide.Ally);
        foreach (RatDefinition definition in definitions) factory.Spawn(definition, VehicleSide.Ally, safe);
        Check(supply.Population == 6, "Population costs remain 1/2/3");
        Clear();

        foreach (RatDefinition definition in definitions)
        {
            foreach (ProjectileEndReason reason in Enum.GetValues(typeof(ProjectileEndReason)))
            {
                RatAgent rat = factory.Spawn(definition, VehicleSide.Enemy, safe);
                Projectile projectile = rat.GetComponent<Projectile>();
                Set(projectile, "_isActive", true);
                projectile.Resolve(reason);
                projectile.Resolve(reason);
                int expected = definition == definitions[2] && reason == ProjectileEndReason.Cancelled ? 3 : 0;
                Check(RatAgent.Active.Count == expected
                    && RatAgent.Active.All(item => item.Definition == definitions[0] && item.Faction == VehicleSide.Enemy),
                    "Projectile resolution " + definition.Rank + " " + reason);
                Clear();
            }
        }
        for (int cycle = 0; cycle < 2; cycle++)
        {
            RatAgent ground = factory.SpawnGroundCombat(definitions[2], VehicleSide.Ally, safe);
            ground.TakeDamage(100f);
            ground.TakeDamage(100f);
            Check(RatAgent.Active.Count(item => !item.IsDead) == 3, "Ground split once, pool cycle " + cycle);
            Clear();
        }

        ValidateStacking(factory, definitions, safe);
        ValidateCollisionFailure(factory, definitions, safe);
        Debug.Log("RAT_REFACTOR_VALIDATION_OK");
    }

    private static void ValidateStacking(RatFactory factory, RatDefinition[] definitions, Vector3 safe)
    {
        RatStacking stacking = UnityEngine.Object.FindFirstObjectByType<RatStacking>();
        CarryController carry = stacking.GetComponent<CarryController>();
        Vector3 originalPosition = stacking.transform.position;
        stacking.transform.position = safe;
        float duration = (float)Get(stacking, "_stageDuration");
        Dictionary<RatDefinition, PoolDefinition> lookup = (Dictionary<RatDefinition, PoolDefinition>)Get(factory, "_poolLookup");
        PoolDefinition resultPool = lookup[definitions[1]];
        try
        {
            AddHeld(factory, carry, definitions[0]);
            AddHeld(factory, carry, definitions[0]);
            AddHeld(factory, carry, definitions[0]);
            Check(stacking.CanStack(), "Three rank1 rats can stack");
            stacking.Tick(true, duration);
            Check(carry.HeldCount == 2 && HeldRat(carry, 1).Definition == definitions[1], "First stack stage");
            stacking.Tick(true, duration);
            Check(carry.HeldCount == 1 && HeldRat(carry, 0).Definition == definitions[2]
                && !carry.InteractionLocked, "Second stack stage");
            carry.DropAll(); Clear();

            AddHeld(factory, carry, definitions[1]);
            AddHeld(factory, carry, definitions[0]);
            stacking.Tick(true, duration);
            Check(carry.HeldCount == 1 && HeldRat(carry, 0).Definition == definitions[2], "Reverse stack input order");
            carry.DropAll(); Clear();

            AddHeld(factory, carry, definitions[1]);
            AddHeld(factory, carry, definitions[1]);
            Check(!stacking.CanStack(), "Rank2 pair rejected");
            carry.DropAll(); Clear();

            AddHeld(factory, carry, definitions[1]);
            AddHeld(factory, carry, definitions[0]);
            AddHeld(factory, carry, definitions[0]);
            Check(!stacking.CanStack(), "Whole held stack limit preserved");
            carry.DropAll(); Clear();

            AddHeld(factory, carry, definitions[0]);
            AddHeld(factory, carry, definitions[0]);
            stacking.Tick(true, duration * 0.5f);
            stacking.Tick(false, 0f);
            Check(carry.HeldCount == 2 && !stacking.IsStacking && !carry.InteractionLocked, "Cancelled stack retains inputs");
            lookup.Remove(definitions[1]);
            stacking.Tick(true, duration);
            Check(carry.HeldCount == 2 && RatAgent.Active.Count == 2
                && !stacking.IsStacking && !carry.InteractionLocked, "Failed spawn retains held inputs");
        }
        finally
        {
            lookup[definitions[1]] = resultPool;
            stacking.Cancel();
            carry.DropAll();
            Clear();
            stacking.transform.position = originalPosition;
        }
    }

    private static void ValidateCollisionFailure(RatFactory factory, RatDefinition[] definitions, Vector3 safe)
    {
        Dictionary<RatDefinition, PoolDefinition> lookup = (Dictionary<RatDefinition, PoolDefinition>)Get(factory, "_poolLookup");
        PoolDefinition pool = lookup[definitions[1]];
        RatAgent first = factory.Spawn(definitions[0], VehicleSide.Ally, safe);
        RatAgent second = factory.Spawn(definitions[0], VehicleSide.Ally, safe);
        try
        {
            lookup.Remove(definitions[1]);
            Call(first.Carryable, "TryBeginCollisionFusion", second.Carryable);
            Check(RatAgent.Active.Count == 2 && first.isActiveAndEnabled && second.isActiveAndEnabled
                && !(bool)Get(first.Carryable, "_fusionLocked") && !(bool)Get(second.Carryable, "_fusionLocked"),
                "Failed collision spawn retains and unlocks inputs");
        }
        finally { lookup[definitions[1]] = pool; Clear(); }
    }

    private static void AddHeld(RatFactory factory, CarryController carry, RatDefinition definition)
    {
        RatAgent rat = factory.Spawn(definition, VehicleSide.Ally, carry.transform.position);
        Check((bool)Call(rat.Carryable, "TryAttachToCarrySlot", carry.GetHoldPoint(carry.HeldCount)), "Attach validation rat");
        ((List<ICarryable>)Get(carry, "_heldObjects")).Add(rat.Carryable);
    }

    private static RatAgent HeldRat(CarryController carry, int index)
    {
        return ((Component)carry.GetHeld(index)).GetComponent<RatAgent>();
    }

    private static Vector3 FindSafeFloor(RatFactory factory)
    {
        foreach (Vector3Int cell in factory.Battlefield.Ground.cellBounds.allPositionsWithin)
        {
            Vector3 center = factory.Battlefield.Ground.GetCellCenterWorld(cell);
            if (factory.Battlefield.Ground.HasTile(cell) && factory.Battlefield.IsSafe(center)) return center;
        }
        throw new InvalidOperationException("No safe floor for refactor validation.");
    }

    private static T Load<T>(string name) where T : UnityEngine.Object
    {
        return AssetDatabase.LoadAssetAtPath<T>("Assets/07.Data/RatPoc2/" + name + ".asset");
    }
    private static object Get(object target, string field) { return target.GetType().GetField(field, Fields).GetValue(target); }
    private static void Set(object target, string field, object value) { target.GetType().GetField(field, Fields).SetValue(target, value); }
    private static object Call(object target, string method, params object[] args)
    {
        return target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
    private static void Clear() { foreach (RatAgent rat in RatAgent.Active.ToArray()) rat.Release(); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("REFACTOR_CHECK " + message);
    }
}
