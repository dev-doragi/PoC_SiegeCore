using System;
using System.Linq;
using System.Reflection;
using SiegeCore.Cannon;
using SiegeCore.Rat;
using SiegeCore.Projectile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SiegePocValidation
{
    private static RatDefinition BasicDefinition => AssetDatabase.LoadAssetAtPath<RatDefinition>("Assets/07.Data/RatPoc2/Rat_Basic.asset");
    private static RatDefinition Rank2Definition => AssetDatabase.LoadAssetAtPath<RatDefinition>("Assets/07.Data/RatPoc2/Rat_BB.asset");
    private static RatDefinition Rank3Definition => AssetDatabase.LoadAssetAtPath<RatDefinition>("Assets/07.Data/RatPoc2/Rat_BBB.asset");
    private static double _started;
    private static int _phase;
    private static float _siegeBefore;
    static SiegePocValidation()
    {
        if (SessionState.GetBool("SiegePocValidation", false)) EditorApplication.update += Tick;
    }
    [MenuItem("SiegeCore/Validate Core Rules")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/00.Scenes/PoC_SiegeCore.unity");
        SessionState.SetBool("SiegePocValidation", true);
        _started = 0;
        _phase = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (_started == 0) _started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - _started < 2) return;
        try
        {
            if (_phase == 0)
            {
                Validate();
                BeginGroundScenario();
                _phase = 1;
                _started = EditorApplication.timeSinceStartup;
                return;
            }
            RatStructure[] facilities = RatStructure.Active.Where(item => !item.IsEntrance && item.Faction == VehicleSide.Enemy).ToArray();
            if (facilities.All(item => item.IsDestroyed))
            {
                Check(UnityEngine.Object.FindFirstObjectByType<RatBattlefield>().EnemyGate.Entrance.IsDestroyed,
                    "Live Ground AI broke Entrance before sabotage");
                SiegeHealth siege = UnityEngine.Object.FindObjectsByType<SiegeHealth>(FindObjectsSortMode.None).First(item => item.Side == VehicleSide.Enemy);
                Check(siege.CurrentHealth == _siegeBefore, "Live sabotage does not damage Siege");
                RatDispenser supply = UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None).First(item => item.Faction == VehicleSide.Enemy);
                Check(supply.ProductionRatio == 0.25f, "Live sabotage reduces enemy production to floor");
                FinishEncounter();
                System.IO.File.WriteAllText("Library/RatRefactorValidation.result", "SIEGE_POC_VALIDATION_OK");
                Debug.Log("SIEGE_POC_VALIDATION_OK");
                Finish(0);
            }
            else if (EditorApplication.timeSinceStartup - _started > 45)
            {
                throw new InvalidOperationException("Ground scenario timed out: " + string.Join(", ", RatAgent.Active.Select(item => item.State + " at " + item.transform.position)));
            }
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText("Library/RatRefactorValidation.result", exception.ToString());
            Debug.LogException(exception);
            Finish(1);
        }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool("SiegePocValidation", false);
        EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("POC_CHECK " + message);
    }
    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
    private static object Call(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
    private static void ClearRats()
    {
        foreach (RatAgent rat in RatAgent.Active.ToArray()) rat.Release();
    }
    private static RatAgent Spawn(RatFactory factory, RatDefinition definition, bool combat = false)
    {
        RatAgent rat = factory.Spawn(definition, VehicleSide.Ally, new Vector3(-5.5f, 0.5f), combat);
        Check(rat != null, "Spawn " + definition.name);
        return rat;
    }
    private static int Basics()
    {
        return RatAgent.Active.Count(rat => !rat.IsDead && rat.Definition == BasicDefinition);
    }
    private static void Validate()
    {
        Check(GameManager.Instance.CurrentState == GameState.Playing, "Bootstrap enters Playing");
        RatFactory factory = UnityEngine.Object.FindFirstObjectByType<RatFactory>();
        Check(factory.IsReady, "Scene pools ready");
        ClearRats();
        RatRefactorValidation.Run(factory);
        RatCompositionValidation.Run(factory);
        ValidateActions(factory);
        foreach (ProjectileEndReason reason in Enum.GetValues(typeof(ProjectileEndReason)))
        {
            RatAgent rat = Spawn(factory, Rank3Definition);
            Projectile projectile = rat.GetComponent<Projectile>();
            Set(projectile, "_isActive", true);
            if (reason == ProjectileEndReason.SiegeHit)
            {
                SiegeHealth siege = UnityEngine.Object.FindObjectsByType<SiegeHealth>(FindObjectsSortMode.None)
                    .First(item => item.Side == VehicleSide.Enemy);
                float before = siege.CurrentHealth;
                Call(projectile, "TryHitSiege", siege);
                Check(Mathf.Approximately(before - siege.CurrentHealth, 30), "BBB Siege damage 30");
            }
            else projectile.Resolve(reason);
            Check(Basics() == (reason == ProjectileEndReason.Cancelled ? 3 : 0), "BBB resolution " + reason);
            projectile.Resolve(reason);
            Check(Basics() == (reason == ProjectileEndReason.Cancelled ? 3 : 0), "No repeated resolution " + reason);
            ClearRats();
        }
        RatAgent ground = Spawn(factory, Rank3Definition, true);
        ground.GetComponent<RatGroundAI>().BeginBaseInfiltration();
        Check(ground.Health == 30 && ground.Definition.Ground.AttackDamage == 9, "BBB ground stats");
        ground.LaunchFromBat(Vector2.right, 4, 8, 1, 3, 1, false, null);
        ground.TakeDamage(100);
        Check(Basics() == 3, "Airborne ground BBB splits into three");
        Check(RatAgent.Active.Where(rat => !rat.IsDead).All(rat => rat.GetComponent<RatGroundAI>().IsInfiltrated), "Split retains infiltration");
        ground.TakeDamage(100);
        Check(Basics() == 3, "No repeated ground split");
        ClearRats();
        RatAgent idle = Spawn(factory, Rank3Definition);
        idle.TakeDamage(100);
        Check(Basics() == 0, "Idle death does not split");
        ClearRats();
        RatDispenser supply = UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None)
            .First(item => item.Faction == VehicleSide.Ally);
        RatStructure[] facilities = RatStructure.Active.Where(item => !item.IsEntrance && item.Faction == VehicleSide.Ally).ToArray();
        Check(facilities.Length == 2 && supply.ProductionRatio == 1, "Two facilities and full production");
        Set(supply, "_progress", 0.4f);
        int events = 0;
        facilities[0].Destroyed += item => events++;
        facilities[0].TakeDamage(VehicleSide.Enemy, 100);
        facilities[0].TakeDamage(VehicleSide.Enemy, 100);
        Check(supply.ProductionRatio == 0.625f && supply.Progress == 0.4f, "First facility reduces rate and preserves progress");
        facilities[0].gameObject.SetActive(false);
        facilities[0].gameObject.SetActive(true);
        Check(facilities[0].IsDestroyed && events == 1, "Facility destruction is permanent and published once");
        facilities[1].TakeDamage(VehicleSide.Enemy, 100);
        Check(supply.ProductionRatio == 0.25f, "Production floor 25 percent");
        for (int index = 0; index < 10; index++) Spawn(factory, Rank3Definition);
        Check(supply.Population == 30, "Population counts B equivalents");
        supply.SpawnRat();
        Check(supply.Population == 30, "Production stops at cap");
        Call(supply, "Update");
        Check(supply.Progress == 0, "Cap clears accrued production");
        RatAgent.Active.First().Release();
        Call(supply, "Update");
        Check(supply.Population == 27 && supply.Progress < 0.1f, "Production resumes with a fresh interval");
        ClearRats();
        RatAgent infiltrator = factory.SpawnGroundCombat(BasicDefinition, VehicleSide.Ally, new Vector3(44.5f, 0.5f));
        RatGroundAI ai = infiltrator.GetComponent<RatGroundAI>();
        ai.BeginBaseInfiltration();
        Call(ai, "ResolveBattlefield");
        RatStructure target = (RatStructure)Call(ai, "FindProductionFacility");
        Check(target != null && target.Faction == VehicleSide.Enemy, "Infiltrator finds reachable enemy facility");
        RatAgent victim = factory.SpawnIdle(BasicDefinition, VehicleSide.Enemy, new Vector3(45.5f, 0.5f));
        Check((RatAgent)Call(ai, "FindNearestEnemy") == victim, "Infiltrator targets idle base Rat");
        victim.BeginAirborne(RatState.Idle, 0);
        Check(Call(ai, "FindNearestEnemy") == null, "Infiltrator excludes airborne Rat");
        ClearRats();
    }

    private static void BeginGroundScenario()
    {
        foreach (RatDispenser supply in UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None)) supply.enabled = false;
        foreach (EnemyRatDirector director in UnityEngine.Object.FindObjectsByType<EnemyRatDirector>(FindObjectsSortMode.None)) director.enabled = false;
        foreach (SiegeCore.Cannon.Cannon cannon in UnityEngine.Object.FindObjectsByType<SiegeCore.Cannon.Cannon>(FindObjectsSortMode.None)) cannon.gameObject.SetActive(false);
        RatFactory factory = UnityEngine.Object.FindFirstObjectByType<RatFactory>();
        RatAgent rat = factory.SpawnGroundCombat(Rank3Definition, VehicleSide.Ally,
            factory.Battlefield.EnemyGate.Entrance.transform.position + Vector3.left * 2);
        rat.GetComponent<RatGroundAI>().BeginArenaCombat();
        _siegeBefore = UnityEngine.Object.FindObjectsByType<SiegeHealth>(FindObjectsSortMode.None).First(item => item.Side == VehicleSide.Enemy).CurrentHealth;
        // Keep subscriptions active while suppressing new rats during the isolated scenario.
        foreach (RatDispenser supply in UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None))
        {
            Set(supply, "_spawnInterval", 100000f);
            supply.enabled = true;
        }
        Time.timeScale = 5;
    }

    private static void FinishEncounter()
    {
        SiegeHealth enemySiege = UnityEngine.Object.FindObjectsByType<SiegeHealth>(FindObjectsSortMode.None).First(item => item.Side == VehicleSide.Enemy);
        enemySiege.TakeProjectileDamage(VehicleSide.Ally, 100000, enemySiege.transform.position);
        Check(GameManager.Instance.CurrentState == GameState.GameOver && Time.timeScale == 0, "Siege destruction ends and freezes match");
        RatDispenser supply = UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None).First(item => item.Faction == VehicleSide.Ally);
        int population = supply.Population;
        supply.SpawnRat();
        Check(supply.Population == population, "No production after match end");
    }

    private static void ValidateActions(RatFactory factory)
    {
        Vector3 safe = Vector3.zero;
        bool found = false;
        foreach (Vector3Int cell in factory.Battlefield.Ground.cellBounds.allPositionsWithin)
        {
            Vector3 center = factory.Battlefield.Ground.GetCellCenterWorld(cell);
            if (!factory.Battlefield.Ground.HasTile(cell) || !factory.Battlefield.IsSafe(center)) continue;
            safe = center;
            found = true;
            break;
        }
        Check(found, "Scene has safe fusion floor");
        RatAgent first = factory.Spawn(BasicDefinition, VehicleSide.Ally, safe);
        RatAgent second = factory.Spawn(BasicDefinition, VehicleSide.Ally, safe);
        first.LaunchFromBat(Vector2.right, 12, 0, 0, 3, 123, true, null);
        // Advance to the moving phase; initial bat impact intentionally holds velocity at zero.
        first.ContinueAfterCollisionFusion(safe, 0, 0, Vector2.right * 12, 123, 3, null);
        Check((bool)Call(first.GetComponent<RatCollisionFusion>(), "TryBeginCollisionFusion", second.GetComponent<RatCollisionFusion>()), "Full charge B+B fusion");
        RatAgent merged = RatAgent.Active.Single(rat => !rat.IsDead);
        Check(merged.Definition == Rank2Definition
            && Mathf.Approximately(merged.GetComponent<Rigidbody2D>().linearVelocity.x, 12), "BB preserves launch speed");
        RatAgent third = factory.Spawn(BasicDefinition, VehicleSide.Ally, merged.transform.position);
        Check((bool)Call(merged.GetComponent<RatCollisionFusion>(), "TryBeginCollisionFusion", third.GetComponent<RatCollisionFusion>()), "Chained BB+B fusion");
        RatAgent bbb = RatAgent.Active.Single(rat => !rat.IsDead);
        Check(bbb.Definition == Rank3Definition
            && Mathf.Approximately(bbb.GetComponent<Rigidbody2D>().linearVelocity.x, 12), "BBB preserves launch speed");
        ClearRats();
        RatAgent deployed = factory.SpawnIdle(BasicDefinition, VehicleSide.Ally, safe);
        Check(factory.Battlefield.AllyGate.TryEnterArena(deployed)
            && deployed.State == RatState.GroundCombat, "Exit deploys same Rat to Arena");
        ClearRats();
        RatAgent captured = factory.SpawnIdle(BasicDefinition, VehicleSide.Enemy, safe);
        SiegeCore.Cannon.Cannon cannon = UnityEngine.Object.FindObjectsByType<SiegeCore.Cannon.Cannon>(FindObjectsSortMode.None)
            .First(item => item.IsInstalledFor(VehicleSide.Ally));
        Check(captured.TryLoadIntoCannon(cannon) && captured.State == RatState.Loaded, "Enemy Rat can load into ally Cannon");
        Check(captured.Faction == VehicleSide.Enemy, "Loading preserves original faction");
        captured.LaunchFromCannon(safe, safe + Vector3.right * 10, VehicleSide.Ally,
            cannon.SourceSlot, CannonTrajectoryType.Straight, 3, 0);
        Check(captured.AttackSide == VehicleSide.Ally && captured.State == RatState.CannonFlight,
            "Captured Rat attacks as firing Cannon faction");
        ClearRats();
    }
}
