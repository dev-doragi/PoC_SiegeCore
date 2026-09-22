using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time, explicit migration. Existing assets keep their GUIDs and prefab inheritance.
[InitializeOnLoad]
public static class RatRefactorMigration
{
    private const string ScenePath = "Assets/00.Scenes/PoC_SiegeCore.unity";
    private const string DataPath = "Assets/07.Data/RatPoc2/";
    private const string RequestPath = "Library/RatRefactorMigration.request";

    static RatRefactorMigration()
    {
        EditorApplication.delayCall += RunRequestedMigration;
    }

    private static void RunRequestedMigration()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += RunRequestedMigration;
            return;
        }
        File.Delete(RequestPath);
        try
        {
            Run();
            File.WriteAllText("Library/RatRefactorMigration.result", "OK");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/RatRefactorMigration.result", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("SiegeCore/Migrate Rat Definitions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the Rat migration outside Play mode.");

        RatDefinition basic = Load<RatDefinition>(DataPath + "Rat_Basic.asset");
        RatDefinition rank2 = Load<RatDefinition>(DataPath + "Rat_BB.asset");
        RatDefinition rank3 = Load<RatDefinition>(DataPath + "Rat_BBB.asset");
        MigrateDefinition(basic);
        MigrateDefinition(rank2);
        MigrateDefinition(rank3);

        BasicProjectileAbility projectile = GetOrCreate<BasicProjectileAbility>(DataPath + "BasicProjectileAbility.asset");
        BasicGroundDeathAbility ground = GetOrCreate<BasicGroundDeathAbility>(DataPath + "BasicGroundDeathAbility.asset");
        SetReference(projectile, "_spawnDefinition", basic);
        SetReference(ground, "_spawnDefinition", basic);
        rank3.ProjectileAbility = projectile;
        rank3.GroundDeathAbility = ground;
        EditorUtility.SetDirty(rank3);

        RatMergeResolver resolver = GetOrCreate<RatMergeResolver>(DataPath + "RatMergeResolver.asset");
        SerializedObject rulesObject = new SerializedObject(resolver);
        SerializedProperty rules = rulesObject.FindProperty("_rules");
        if (rules.arraySize == 0)
        {
            rules.arraySize = 2;
            SetRule(rules.GetArrayElementAtIndex(0), basic, basic, rank2);
            SetRule(rules.GetArrayElementAtIndex(1), basic, rank2, rank3);
            rulesObject.ApplyModifiedPropertiesWithoutUndo();
        }

        string[] prefabPaths = { "Rat_Basic", "Rat_BB", "Rat_BBB" };
        foreach (string name in prefabPaths)
        {
            string path = "Assets/02.Prefabs/RatPoc2/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CarryableObject carryable = root.GetComponent<CarryableObject>();
                SerializedObject settings = new SerializedObject(carryable.GetComponent<RatCollisionFusion>());
                SerializedProperty property = settings.FindProperty("_mergeResolver");
                if (property.objectReferenceValue != resolver)
                {
                    property.objectReferenceValue = resolver;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        RatLoadoutDefinition basicLoadout =
            AssetDatabase.LoadAssetAtPath<RatLoadoutDefinition>(DataPath + "RatLoadout_Basic.asset");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RatFactory factory in root.GetComponentsInChildren<RatFactory>(true))
                {
                    // RatFactory now receives its complete Definition-to-Pool catalog
                    // from RatLoadoutMigration instead of storing a scene array.
                }
                foreach (RatDispenser dispenser in root.GetComponentsInChildren<RatDispenser>(true))
                    SetReference(dispenser, "_loadout", basicLoadout);
                foreach (RatStacking stacking in root.GetComponentsInChildren<RatStacking>(true))
                    SetReference(stacking, "_mergeResolver", resolver);
                foreach (CarryableObject carryable in root.GetComponentsInChildren<CarryableObject>(true))
                {
                    if (carryable.GetComponent<RatAgent>() != null)
                        SetReference(carryable.GetComponent<RatCollisionFusion>(), "_mergeResolver", resolver);
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save migrated Rat scene.");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("RAT_REFACTOR_MIGRATION_OK");
    }

    private static void MigrateDefinition(RatDefinition definition)
    {
        string text = File.ReadAllText(AssetDatabase.GetAssetPath(definition));
        if (!Regex.IsMatch(text, @"(?m)^  Form:")) return;
        int rank = (int)ReadNumber(text, "Form", 1f);
        definition.Type = RatType.Basic;
        definition.Rank = (RatRank)rank;
        definition.Ground = new GroundStats
        {
            Health = ReadNumber(text, "Health", 10f),
            AttackDamage = ReadNumber(text, "AttackDamage", 3f),
            AttackInterval = ReadNumber(text, "AttackInterval", 1f),
            MoveSpeed = ReadNumber(text, "MoveSpeed", 2f),
            AttackRange = ReadNumber(text, "AttackRange", 0.9f),
            DetectionRadius = ReadNumber(text, "DetectionRadius", 4f)
        };
        definition.Projectile = new ProjectileStats
        {
            Damage = ReadNumber(text, "ProjectileDamage", 10f),
            Weight = rank == 3 ? RatWeightRank.Heavy : rank == 2 ? RatWeightRank.Medium : RatWeightRank.Light
        };
        EditorUtility.SetDirty(definition);
    }

    private static float ReadNumber(string text, string field, float fallback)
    {
        Match match = Regex.Match(text, @"(?m)^  " + field + @": ([^\r\n]+)");
        return match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject settings = new SerializedObject(target);
        SerializedProperty property = settings.FindProperty(field);
        if (property.objectReferenceValue == value) return;
        property.objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }

    private static void SetRule(SerializedProperty rule, RatDefinition first, RatDefinition second, RatDefinition result)
    {
        rule.FindPropertyRelative("First").objectReferenceValue = first;
        rule.FindPropertyRelative("Second").objectReferenceValue = second;
        rule.FindPropertyRelative("Result").objectReferenceValue = result;
    }
}
