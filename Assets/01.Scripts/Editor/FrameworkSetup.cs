using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class FrameworkSetup
{
    [MenuItem("Framework/Create Setup")]
    public static void CreateSetup()
    {
        Directory.CreateDirectory("Assets/02.Prefabs/Resources");
        Directory.CreateDirectory("Assets/07.Data");
        AssetDatabase.Refresh();
        string inputPath = "Assets/07.Data/FrameworkInput.inputactions";
        string prefabPath = "Assets/02.Prefabs/Resources/AppRoot.prefab";
        string scenePath = "Assets/00.Scenes/CodexFrameworkScene.unity";

        CreateInputAsset(inputPath);
        CreateAppPrefab(prefabPath, inputPath);
        CreateDemoScene(scenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("FRAMEWORK_SETUP_OK");
    }

    private static void CreateInputAsset(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return;
        }

        InputActionAsset input = ScriptableObject.CreateInstance<InputActionAsset>();
        InputActionMap player = input.AddActionMap("Player");
        player.AddAction("Move", InputActionType.Value).AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        player.AddAction("Look", InputActionType.Value, "<Pointer>/position");
        player.AddAction("PrimaryAction", InputActionType.Button, "<Mouse>/leftButton");
        player.AddAction("SecondaryAction", InputActionType.Button, "<Mouse>/rightButton");
        InputActionMap ui = input.AddActionMap("UI");
        ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
        ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/backspace");
        ui.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        ui.AddAction("BattlefieldView", InputActionType.Button, "<Keyboard>/tab");
        File.WriteAllText(inputPath, input.ToJson());
        UnityEngine.Object.DestroyImmediate(input);
        AssetDatabase.ImportAsset(inputPath);
    }

    private static void CreateAppPrefab(string prefabPath, string inputPath)
    {
        if (File.Exists(prefabPath))
        {
            return;
        }

        GameObject root = new GameObject("AppRoot");
        root.AddComponent<Bootstrapper>();
        Add<GameManager>(root);
        Add<TimeManager>(root);
        Add<SoundManager>(root);
        Add<SceneLoader>(root);
        InputReader input = Add<InputReader>(root);
        SerializedObject inputSettings = new SerializedObject(input);
        inputSettings.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
        inputSettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void CreateDemoScene(string scenePath)
    {
        if (File.Exists(scenePath))
        {
            return;
        }

        if (Application.isBatchMode)
        {
            EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity");
        }
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject root = new GameObject("SceneContext");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        SceneContext context = root.AddComponent<SceneContext>();
        GameFlowManager flow = Add<GameFlowManager>(root);
        UIManager ui = Add<UIManager>(root);
        PoolManager pool = Add<PoolManager>(root);
        SerializedObject serialized = new SerializedObject(context);
        SerializedProperty services = serialized.FindProperty("_services");
        services.arraySize = 3;
        services.GetArrayElementAtIndex(0).objectReferenceValue = flow;
        services.GetArrayElementAtIndex(1).objectReferenceValue = ui;
        services.GetArrayElementAtIndex(2).objectReferenceValue = pool;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject demoObject = new GameObject("FrameworkDemo");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(demoObject, scene);
        FrameworkDemo demo = demoObject.AddComponent<FrameworkDemo>();
        SerializedObject demoSettings = new SerializedObject(demo);
        demoSettings.FindProperty("_flow").objectReferenceValue = flow;
        demoSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static T Add<T>(GameObject root) where T : Component
    {
        GameObject child = new GameObject(typeof(T).Name);
        child.transform.SetParent(root.transform);
        return child.AddComponent<T>();
    }

    public static void Validate()
    {
        CreateSetup();
        GameObject root = new GameObject("StateValidation");
        try
        {
            GameManager game = root.AddComponent<GameManager>();
            GameFlowManager flow = root.AddComponent<GameFlowManager>();
            flow.Configure(game);
            game.Initialize();
            flow.Initialize();
            game.StartService();
            flow.StartService();
            Check(!game.RequestPause(), "Pause before playing");
            Check(game.BeginBoot() && game.CompleteLoading(GameState.Playing), "Boot transition");
            Check(flow.CurrentState == InGameState.Initializing, "Scene flow initialization");
            Check(!flow.TryChangeState(InGameState.Completed), "Invalid flow transition");
            Check(flow.TryChangeState(InGameState.Ready) && flow.TryChangeState(InGameState.Running), "Flow progression");
            Check(game.RequestPause() && flow.CurrentState == InGameState.Running, "Pause preserves flow");
            Check(!flow.TryChangeState(InGameState.Completed), "Paused progression rejected");
            Check(game.RequestResume() && flow.CurrentState == InGameState.Running, "Resume preserves flow");
            Check(game.EndGame() && !game.RequestResume(), "GameOver cannot resume");
            flow.Shutdown();
            game.Shutdown();
            Debug.Log("FRAMEWORK_VALIDATION_OK");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static void ValidatePlayMode()
    {
        CreateSetup();
        EditorSceneManager.OpenScene("Assets/00.Scenes/CodexFrameworkScene.unity");
        SessionState.SetInt("Framework.Validation", 1);
        EditorApplication.EnterPlaymode();
    }

    private static double _deadline;
    private static int _frame;
    private static Bootstrapper _app;
    private static GameFlowManager _flow;
    [InitializeOnLoadMethod]
    private static void ResumeValidation()
    {
        if (SessionState.GetInt("Framework.Validation", 0) == 0)
        {
            return;
        }
        _deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += TickValidation;
    }

    private static void TickValidation()
    {
        try
        {
            int phase = SessionState.GetInt("Framework.Validation", 0);
            if (phase == 3 && !EditorApplication.isPlaying)
            {
                SessionState.SetInt("Framework.Validation", 0);
                EditorApplication.update -= TickValidation;
                Debug.Log("FRAMEWORK_PLAYMODE_OK");
                EditorApplication.Exit(0);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                throw new Exception("Play validation timed out.");
            }
            if (!EditorApplication.isPlaying || Time.frameCount < 5)
            {
                return;
            }
            if (phase == 1)
            {
                ValidateRunningScene();
                return;
            }

            if (phase == 2 && Time.frameCount > _frame + 3)
            {
                ValidateReloadedScene();
            }
        }
        catch (Exception exception)
        {
            SessionState.SetInt("Framework.Validation", 0);
            EditorApplication.update -= TickValidation;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateRunningScene()
    {
        _app = Bootstrapper.Instance;
        Check(_app != null && _app.IsReady, "App boot");
        GameManager game = _app.Game;
        _flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        Check(game.CurrentState == GameState.Playing && _flow != null, "Scene boot");
        Check(_flow.CurrentState == InGameState.Running, "Demo progression");
        Check(game.RequestPause() && Time.timeScale == 0 && _flow.CurrentState == InGameState.Running, "Pause");
        Check(game.RequestResume() && Time.timeScale == 1, "Resume");
        _flow.enabled = false;
        _flow.enabled = true;
        Check(_flow.CurrentState == InGameState.Running, "Reactivation preserves progress");
        game.enabled = false;
        game.enabled = true;
        EventBus.Instance.Publish(new PauseRequestedEvent { Pause = true });
        Check(game.CurrentState == GameState.Paused, "Subscriptions restored");
        game.RequestResume();
        ValidatePoolOwnership();

        _frame = Time.frameCount;
        SessionState.SetInt("Framework.Validation", 2);
        Check(game.BeginLoading(), "Begin reload");
        EditorSceneManager.LoadSceneInPlayMode("Assets/00.Scenes/CodexFrameworkScene.unity",
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
    }

    private static void ValidatePoolOwnership()
    {
        PoolManager pool = UnityEngine.Object.FindFirstObjectByType<PoolManager>();
        GameObject prefab = new GameObject("DifferentFromPoolKey");
        prefab.SetActive(false);
        PoolDefinition definition = ScriptableObject.CreateInstance<PoolDefinition>();
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_prefab").objectReferenceValue = prefab;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        GameObject instance = pool.Spawn(definition, Vector3.one, Quaternion.identity);
        instance.GetComponent<PooledObject>().Return();
        Check(!instance.activeSelf, "Pool ownership return");
        Check(pool.Spawn(definition, Vector3.zero, Quaternion.identity) == instance, "Pool reuse");
        pool.ClearAllPools();
        UnityEngine.Object.Destroy(definition);
        UnityEngine.Object.Destroy(prefab);
    }

    private static void ValidateReloadedScene()
    {
        Check(Bootstrapper.Instance == _app && _app.IsReady, "App persists");
        GameFlowManager nextFlow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        Check(nextFlow != null && nextFlow != _flow && nextFlow.CurrentState == InGameState.Running, "Scene resets");
        Check(_app.Game.CurrentState == GameState.Playing, "Reload completed");
        SessionState.SetInt("Framework.Validation", 3);
        EditorApplication.ExitPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
