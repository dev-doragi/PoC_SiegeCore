using System;
using System.Collections.Generic;
using System.IO;
using SiegeCore.Player;
using SiegeCore.Rat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RatCompositionMigration
{
    private static readonly string[] MotionFields = {
        "_groundTilemap", "_snapSteering", "_snapFinishDuration", "_throwSpeed", "_upwardSpeed",
        "_throwCellDistance", "_heightGravity", "_bounceRestitution", "_groundSpeedRetention",
        "_minimumBounceSpeed", "_wallRestitution", "_impactStopDuration", "_squashRecoveryDuration",
        "_impactStopSpeedThreshold", "_flightDecelerationPerRank", "_popupVerticalSpeedMultiplier",
        "_minimumPopupVerticalSpeed", "_maximumPopupVerticalSpeed", "_popupPlayerBlend",
        "_maximumPopupReturnSpeed", "_defaultCannonFlightDuration", "_defaultCannonArcHeight"
    };
    private static readonly string[] PresentationFields = {
        "_visual"
    };
    private static readonly string[] FusionFields = { "_mergeResolver", "_fusionHeightTolerance" };

    [MenuItem("SiegeCore/Migrate Rat Composition")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before migration.");
        // Capture eligibility before modifying base prefabs: variants inherit the version field.
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/02.Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CarryableObject carryable = prefab.GetComponent<CarryableObject>();
            if (carryable != null && NeedsMigration(carryable)) paths.Add(path);
        }
        paths.Sort((first, second) => PrefabDepth(first).CompareTo(PrefabDepth(second)));

        List<CarryableObject> sceneObjects = new List<CarryableObject>();
        Scene scene = SceneManager.GetSceneByPath("Assets/00.Scenes/PoC_SiegeCore.unity");
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene("Assets/00.Scenes/PoC_SiegeCore.unity", OpenSceneMode.Additive);
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (CarryableObject item in sceneRoot.GetComponentsInChildren<CarryableObject>(true))
                if (NeedsMigration(item)) sceneObjects.Add(item);
        }

        string backupRoot = "Library/RatCompositionBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        foreach (string path in paths)
        {
            string backup = Path.Combine(backupRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(path, backup, false);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Migrate(prefab.GetComponent<CarryableObject>());
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        try
        {
            foreach (CarryableObject item in sceneObjects) Migrate(item);
            if (sceneObjects.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("RAT_COMPOSITION_MIGRATION_OK prefabs=" + paths.Count + " sceneObjects=" + sceneObjects.Count);
    }

    private static bool NeedsMigration(CarryableObject item)
    {
        return new SerializedObject(item).FindProperty("_compositionVersion").intValue < 1;
    }

    private static int PrefabDepth(string path)
    {
        int depth = 0;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        while (PrefabUtility.IsPartOfVariantPrefab(prefab))
        {
            prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            depth++;
        }
        return depth;
    }

    private static void Migrate(CarryableObject item)
    {
        SerializedObject source = new SerializedObject(item);
        RatFlightMotion motion = item.GetComponent<RatFlightMotion>();
        if (motion == null) motion = item.gameObject.AddComponent<RatFlightMotion>();
        RatPresenter presentation = item.GetComponent<RatPresenter>();
        if (presentation == null) throw new InvalidOperationException(item.name + " is missing RatPresenter.");
        RatCollisionFusion fusion = item.GetComponent<RatCollisionFusion>();
        if (fusion == null) fusion = item.gameObject.AddComponent<RatCollisionFusion>();
        Copy(source, motion, MotionFields);
        Copy(source, presentation, PresentationFields);
        Copy(source, fusion, FusionFields);
        source.FindProperty("_compositionVersion").intValue = 1;
        source.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
    }

    private static void Copy(SerializedObject source, UnityEngine.Object destination, string[] fields)
    {
        SerializedObject target = new SerializedObject(destination);
        foreach (string name in fields)
        {
            SerializedProperty value = source.FindProperty(name);
            if (value == null || target.FindProperty(name) == null)
                throw new InvalidOperationException("Cannot migrate " + name + " to " + destination.name);
            target.CopyFromSerializedProperty(value);
        }
        target.ApplyModifiedPropertiesWithoutUndo();
        // Verify actual serialized values, including object references and prefab overrides.
        target.Update();
        foreach (string name in fields)
        {
            if (!SerializedProperty.DataEquals(source.FindProperty(name), target.FindProperty(name)))
                throw new InvalidOperationException("Migration changed " + name + " on " + destination.name);
        }
        if (PrefabUtility.IsPartOfPrefabInstance(destination)) PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
    }
}
