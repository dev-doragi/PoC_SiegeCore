#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SiegeCore.Rat;

public static class RatLoadoutMigration
{
    private const string ScenePath = "Assets/00.Scenes/PoC_SiegeCore.unity";
    private const string DataPath = "Assets/07.Data/RatPoc2/";
    private const string PoolPath = "Assets/07.Data/PoolDefinitions/";
    private const string PrefabPath = "Assets/02.Prefabs/RatPoc2/";

    [MenuItem("SiegeCore/Migrate Rat Catalog and Loadouts")]
    public static void Run()
    {
        RatDefinition[] definitions =
        {
            Load<RatDefinition>(DataPath + "Rat_Basic.asset"),
            Load<RatDefinition>(DataPath + "Rat_BB.asset"),
            Load<RatDefinition>(DataPath + "Rat_BBB.asset"),
            Load<RatDefinition>(DataPath + "Rat_Bomber.asset"),
            Load<RatDefinition>(DataPath + "Rat_Bomber_BB.asset"),
            Load<RatDefinition>(DataPath + "Rat_Bomber_BBB.asset"),
            Load<RatDefinition>(DataPath + "Rat_Tank.asset"),
            Load<RatDefinition>(DataPath + "Rat_Tank_BB.asset"),
            Load<RatDefinition>(DataPath + "Rat_Tank_BBB.asset")
        };

        PoolDefinition[] pools =
        {
            Load<PoolDefinition>(PoolPath + "RatBasicPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatBBPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatBBBPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatBomberPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatBomberBBPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatBomberBBBPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatTankPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatTankBBPool.asset"),
            Load<PoolDefinition>(PoolPath + "RatTankBBBPool.asset")
        };

        string[] prefabNames =
        {
            "Rat_Basic.prefab",
            "Rat_BB.prefab",
            "Rat_BBB.prefab",
            "Rat_Bomber.prefab",
            "Rat_Bomber_BB.prefab",
            "Rat_Bomber_BBB.prefab",
            "Rat_Tank.prefab",
            "Rat_Tank_BB.prefab",
            "Rat_Tank_BBB.prefab"
        };

        for (int index = 0; index < pools.Length; index++)
        {
            AssignPrefab(pools[index], PrefabPath + prefabNames[index]);
        }

        RatCatalog catalog = LoadOrCreate<RatCatalog>(DataPath + "RatCatalog.asset");
        SerializedObject catalogObject = new SerializedObject(catalog);
        SerializedProperty entries = catalogObject.FindProperty("_entries");
        entries.arraySize = definitions.Length;

        for (int index = 0; index < definitions.Length; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Definition").objectReferenceValue = definitions[index];
            entry.FindPropertyRelative("Pool").objectReferenceValue = pools[index];
        }

        catalogObject.FindProperty("_fallbackDefinition").objectReferenceValue = definitions[0];
        catalogObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        RatLoadoutDefinition basicLoadout =
            LoadOrCreate<RatLoadoutDefinition>(DataPath + "RatLoadout_Basic.asset");
        SerializedObject loadoutObject = new SerializedObject(basicLoadout);
        SerializedProperty loadoutEntries = loadoutObject.FindProperty("_entries");
        loadoutEntries.arraySize = 1;
        SerializedProperty basicEntry = loadoutEntries.GetArrayElementAtIndex(0);
        basicEntry.FindPropertyRelative("Definition").objectReferenceValue = definitions[0];
        basicEntry.FindPropertyRelative("Copies").intValue = 1;
        loadoutObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(basicLoadout);

        ConfigureScene(catalog, basicLoadout);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RAT_LOADOUT_MIGRATION_OK");
    }

    private static void ConfigureScene(
        RatCatalog catalog,
        RatLoadoutDefinition basicLoadout)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RatFactory factory in root.GetComponentsInChildren<RatFactory>(true))
                {
                    SerializedObject settings = new SerializedObject(factory);
                    SerializedProperty property = settings.FindProperty("_catalog");
                    property.objectReferenceValue = catalog;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }

                foreach (RatDispenser dispenser in root.GetComponentsInChildren<RatDispenser>(true))
                {
                    SerializedObject settings = new SerializedObject(dispenser);
                    SerializedProperty property = settings.FindProperty("_loadout");
                    property.objectReferenceValue = basicLoadout;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save Rat loadout migration scene.");
            }
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssignPrefab(PoolDefinition pool, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Missing Rat prefab: " + prefabPath);
        }

        SerializedObject settings = new SerializedObject(pool);
        SerializedProperty property = settings.FindProperty("_prefab");
        property.objectReferenceValue = prefab;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pool);
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif
