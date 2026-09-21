using System;
using System.Collections.Generic;
using System.Linq;
using SiegeCore.Cannon;
using SiegeCore.Rat;
using SiegeCore.Projectile;
using SiegeCore.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SiegePocSetup
{
    private const string ScenePath = "Assets/00.Scenes/PoC_SiegeCore.unity";
    [MenuItem("SiegeCore/Configure Core Loop")]
    public static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        RatBattlefield battlefield = UnityEngine.Object.FindFirstObjectByType<RatBattlefield>();
        RatDispenser[] supplies = UnityEngine.Object.FindObjectsByType<RatDispenser>(FindObjectsSortMode.None);
        foreach (RatDispenser supply in supplies)
        {
            GroundGate gate = supply.Faction == VehicleSide.Ally ? battlefield.AllyGate : battlefield.EnemyGate;
            string prefix = supply.Faction.ToString();
            GameObject areaObject = GameObject.Find(prefix + "BaseArea");
            if (areaObject == null) areaObject = new GameObject(prefix + "BaseArea");
            BoxCollider2D area = areaObject.GetComponent<BoxCollider2D>();
            if (area == null) area = areaObject.AddComponent<BoxCollider2D>();
            Bounds bounds = FindRoomBounds(battlefield.Ground, gate.transform.position);
            areaObject.transform.position = bounds.center;
            area.size = bounds.size;
            area.isTrigger = true;
            SetReference(gate, "_baseArea", area);
            RatStructure[] facilities = new RatStructure[2];
            for (int index = 0; index < 2; index++)
            {
                string facilityName = prefix + "ProductionFacility" + (index + 1);
                GameObject facilityObject = GameObject.Find(facilityName);
                if (facilityObject == null) facilityObject = new GameObject(facilityName);
                float x = supply.Faction == VehicleSide.Ally ? -10.5f + index * 4f : 47.5f + index * 4f;
                Vector3 position = new Vector3(x, 3.5f, 0f);
                if (!battlefield.Ground.HasTile(battlefield.Ground.WorldToCell(position)))
                    throw new InvalidOperationException("Facility needs floor: " + position);
                facilityObject.transform.position = position;
                SpriteRenderer visual = facilityObject.GetComponent<SpriteRenderer>();
                if (visual == null) visual = facilityObject.AddComponent<SpriteRenderer>();
                SpriteRenderer entranceVisual = gate.Entrance.GetComponentInChildren<SpriteRenderer>();
                visual.sprite = entranceVisual.sprite;
                visual.color = supply.Faction == VehicleSide.Ally ? new Color(0.2f, 0.85f, 0.85f) : new Color(1f, 0.4f, 0.3f);
                visual.sortingLayerID = entranceVisual.sortingLayerID;
                visual.sortingOrder = entranceVisual.sortingOrder;
                facilityObject.transform.localScale = Vector3.one;
                BoxCollider2D hitbox = facilityObject.GetComponent<BoxCollider2D>();
                if (hitbox == null) hitbox = facilityObject.AddComponent<BoxCollider2D>();
                hitbox.isTrigger = true;
                hitbox.size = Vector2.one;
                RatStructure facility = facilityObject.GetComponent<RatStructure>();
                if (facility == null) facility = facilityObject.AddComponent<RatStructure>();
                SerializedObject settings = new SerializedObject(facility);
                settings.FindProperty("_faction").enumValueIndex = (int)supply.Faction;
                settings.FindProperty("_isEntrance").boolValue = false;
                settings.FindProperty("_maxHealth").floatValue = 30f;
                settings.FindProperty("_visual").objectReferenceValue = visual;
                settings.FindProperty("_barrier").objectReferenceValue = hitbox;
                settings.ApplyModifiedPropertiesWithoutUndo();
                facilities[index] = facility;
                Transform existingLabel = facilityObject.transform.Find("Label");
                TextMeshPro label;
                if (existingLabel == null)
                {
                    GameObject labelObject = new GameObject("Label", typeof(TextMeshPro));
                    labelObject.transform.SetParent(facilityObject.transform, false);
                    label = labelObject.GetComponent<TextMeshPro>();
                }
                else label = existingLabel.GetComponent<TextMeshPro>();
                label.text = "SUPPLY " + (index + 1);
                label.fontSize = 3;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(4, 1);
                label.transform.localPosition = new Vector3(0, 0.85f, 0);
            }
            SerializedObject production = new SerializedObject(supply);
            SerializedProperty refs = production.FindProperty("_productionFacilities");
            refs.arraySize = 2;
            for (int index = 0; index < 2; index++) refs.GetArrayElementAtIndex(index).objectReferenceValue = facilities[index];
            production.FindProperty("_minimumProductionRatio").floatValue = 0.25f;
            production.FindProperty("_populationCap").intValue = 30;
            production.ApplyModifiedPropertiesWithoutUndo();
        }
        RatDefinition bbb = AssetDatabase.LoadAssetAtPath<RatDefinition>("Assets/07.Data/RatPoc2/Rat_BBB.asset");
        bbb.Ground.Health = 30; bbb.Ground.AttackDamage = 9; bbb.Projectile.Damage = 30;
        EditorUtility.SetDirty(bbb);
        ConfigureOverlay(supplies);
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(item => item.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("POC_SETUP_OK");
    }

    private static Bounds FindRoomBounds(Tilemap floor, Vector3 start)
    {
        Vector3Int first = floor.WorldToCell(start);
        if (!floor.HasTile(first)) throw new InvalidOperationException("Gate is not on floor");
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { first };
        Queue<Vector3Int> pending = new Queue<Vector3Int>();
        pending.Enqueue(first);
        Bounds bounds = new Bounds(floor.GetCellCenterWorld(first), Vector3.zero);
        Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        while (pending.Count > 0)
        {
            Vector3Int cell = pending.Dequeue();
            bounds.Encapsulate(floor.GetCellCenterWorld(cell));
            foreach (Vector3Int direction in directions)
            {
                Vector3Int next = cell + direction;
                if (floor.HasTile(next) && visited.Add(next)) pending.Enqueue(next);
            }
        }
        bounds.Expand(new Vector3(1, 1, 0));
        Debug.Log("POC_BASE " + bounds);
        return bounds;
    }

    private static void ConfigureOverlay(RatDispenser[] supplies)
    {
        GameObject root = GameObject.Find("RatEconomyHUD");
        if (root == null) root = new GameObject("RatEconomyHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RatEconomyOverlay overlay = root.GetComponent<RatEconomyOverlay>();
        if (overlay == null) overlay = root.AddComponent<RatEconomyOverlay>();
        TMP_Text ally = CreateText(root.transform, "AllyEconomy", new Vector2(0, 1), new Vector2(24, -20), new Vector2(650, 90));
        TMP_Text enemy = CreateText(root.transform, "EnemyEconomy", new Vector2(1, 1), new Vector2(-24, -20), new Vector2(650, 90));
        enemy.alignment = TextAlignmentOptions.TopRight;
        TMP_Text result = CreateText(root.transform, "Result", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 160));
        result.alignment = TextAlignmentOptions.Center;
        result.fontSize = 64;
        result.text = "";
        SetReference(overlay, "_allyText", ally);
        SetReference(overlay, "_enemyText", enemy);
        foreach (RatDispenser supply in supplies)
            SetReference(overlay, supply.Faction == VehicleSide.Ally ? "_allySupply" : "_enemySupply", supply);
        foreach (SiegeHealth siege in UnityEngine.Object.FindObjectsByType<SiegeHealth>(FindObjectsSortMode.None))
            SetReference(overlay, siege.Side == VehicleSide.Ally ? "_allySiege" : "_enemySiege", siege);
        SetReference(UnityEngine.Object.FindFirstObjectByType<RatEncounter>(), "_status", result);
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject item = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TMP_Text text = item.GetComponent<TMP_Text>();
        text.rectTransform.anchorMin = anchor;
        text.rectTransform.anchorMax = anchor;
        text.rectTransform.pivot = anchor;
        text.rectTransform.anchoredPosition = offset;
        text.rectTransform.sizeDelta = size;
        text.fontSize = 28;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = name;
        return text;
    }

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject settings = new SerializedObject(target);
        settings.FindProperty(field).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
