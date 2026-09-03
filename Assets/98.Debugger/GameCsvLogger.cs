using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public class GameCsvLogger : MonoBehaviour
{
    [Header("Output")]
    [SerializeField] private bool _enableLogging = true;
    [SerializeField] private string _logDirectoryName = "Logs";
    [SerializeField] private string _filePrefix = "framework_log";

    private readonly ConcurrentQueue<string> _pendingLines = new ConcurrentQueue<string>();
    private readonly AutoResetEvent _flushSignal = new AutoResetEvent(false);

    private Thread _writerThread;
    private volatile bool _isRunning;
    private string _logFilePath;

    private void OnEnable()
    {
        if (!_enableLogging)
        {
            return;
        }

        StartWriter();
        SubscribeEvents();
        Log(GameLogEventType.ApplicationStarted);
    }

    private void OnDisable()
    {
        if (!_enableLogging)
        {
            return;
        }

        Log(GameLogEventType.ApplicationQuit);
        UnsubscribeEvents();
        StopWriter();
    }

    private void StartWriter()
    {
        string executableDirectory = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(executableDirectory))
        {
            Debug.LogError("[GameCsvLogger] Failed to resolve executable directory. Fallback to persistentDataPath.");
            executableDirectory = Application.persistentDataPath;
        }

        string basePath = Path.Combine(executableDirectory, _logDirectoryName);
        Directory.CreateDirectory(basePath);

        string fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _logFilePath = Path.Combine(basePath, fileName);

        _isRunning = true;
        _writerThread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "GameCsvLoggerWriter"
        };
        _writerThread.Start();

        EnqueueLine("TimestampUtc,EventType,ActorId,ActorName,TargetId,TargetName,Metadata");
        Debug.Log($"[GameCsvLogger] Logging started: {_logFilePath}");
    }

    private void StopWriter()
    {
        _isRunning = false;
        _flushSignal.Set();

        if (_writerThread != null)
        {
            _writerThread.Join(1000);
            _writerThread = null;
        }

        while (_pendingLines.TryDequeue(out _))
        {
        }

        Debug.Log("[GameCsvLogger] Logging stopped.");
    }

    private void WriterLoop()
    {
        try
        {
            using (var stream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                while (_isRunning || !_pendingLines.IsEmpty)
                {
                    bool wroteAny = false;
                    while (_pendingLines.TryDequeue(out string line))
                    {
                        writer.WriteLine(line);
                        wroteAny = true;
                    }

                    if (wroteAny)
                    {
                        writer.Flush();
                    }

                    if (_isRunning)
                    {
                        _flushSignal.WaitOne(50);
                    }
                }

                writer.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameCsvLogger] WriterLoop failed: {ex}");
        }
    }

    private void SubscribeEvents()
    {
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);
        EventBus.Instance.Subscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        EventBus.Instance.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        EventBus.Instance.Subscribe<PauseRequestedEvent>(OnPauseRequested);
        EventBus.Instance.Subscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);
        EventBus.Instance.Subscribe<CameraShakeEvent>(OnCameraShakeRequested);
        EventBus.Instance.Subscribe<PlaySoundEvent>(OnPlaySoundRequested);
        EventBus.Instance.Subscribe<StopSoundEvent>(OnStopSoundRequested);
        EventBus.Instance.Subscribe<MoveInputEvent>(OnMoveInput);
        EventBus.Instance.Subscribe<LookInputEvent>(OnLookInput);
        EventBus.Instance.Subscribe<SubmitInputEvent>(OnSubmitInput);
        EventBus.Instance.Subscribe<CancelInputEvent>(OnCancelInput);
        EventBus.Instance.Subscribe<PauseInputEvent>(OnPauseInput);
        EventBus.Instance.Subscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
        EventBus.Instance.Subscribe<SecondaryActionInputEvent>(OnSecondaryActionInput);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
        EventBus.Instance.Unsubscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        EventBus.Instance.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        EventBus.Instance.Unsubscribe<PauseRequestedEvent>(OnPauseRequested);
        EventBus.Instance.Unsubscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);
        EventBus.Instance.Unsubscribe<CameraShakeEvent>(OnCameraShakeRequested);
        EventBus.Instance.Unsubscribe<PlaySoundEvent>(OnPlaySoundRequested);
        EventBus.Instance.Unsubscribe<StopSoundEvent>(OnStopSoundRequested);
        EventBus.Instance.Unsubscribe<MoveInputEvent>(OnMoveInput);
        EventBus.Instance.Unsubscribe<LookInputEvent>(OnLookInput);
        EventBus.Instance.Unsubscribe<SubmitInputEvent>(OnSubmitInput);
        EventBus.Instance.Unsubscribe<CancelInputEvent>(OnCancelInput);
        EventBus.Instance.Unsubscribe<PauseInputEvent>(OnPauseInput);
        EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
        EventBus.Instance.Unsubscribe<SecondaryActionInputEvent>(OnSecondaryActionInput);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        Log(GameLogEventType.GameStateChanged, metadata: new Dictionary<string, object>
        {
            { "previous_state", evt.PreviousState.ToString() },
            { "new_state", evt.NewState.ToString() }
        });
    }

    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        Log(GameLogEventType.InGameStateChanged, metadata: new Dictionary<string, object>
        {
            { "previous_state", evt.PreviousState.ToString() },
            { "new_state", evt.NewState.ToString() }
        });
    }

    private void OnSceneLoadRequested(SceneLoadRequestedEvent evt)
    {
        Log(GameLogEventType.SceneLoadRequested, metadata: new Dictionary<string, object>
        {
            { "scene_name", evt.SceneName ?? string.Empty }
        });
    }

    private void OnSceneLoaded(SceneLoadedEvent evt)
    {
        Log(GameLogEventType.SceneLoaded, metadata: new Dictionary<string, object>
        {
            { "scene_name", evt.SceneName ?? string.Empty }
        });
    }

    private void OnPauseRequested(PauseRequestedEvent evt)
    {
        Log(evt.Pause ? GameLogEventType.PauseRequested : GameLogEventType.ResumeRequested);
    }

    private void OnTimeScaleChanged(TimeScaleChangedEvent evt)
    {
        Log(GameLogEventType.TimeScaleChanged, metadata: new Dictionary<string, object>
        {
            { "time_scale", evt.NewTimeScale }
        });
    }

    private void OnCameraShakeRequested(CameraShakeEvent evt)
    {
        Log(GameLogEventType.CameraShakeRequested, metadata: new Dictionary<string, object>
        {
            { "intensity", evt.Intensity.ToString() }
        });
    }

    private void OnPlaySoundRequested(PlaySoundEvent evt)
    {
        Log(GameLogEventType.SoundRequested, metadata: new Dictionary<string, object>
        {
            { "mode", evt.IsBgm ? "Bgm" : "Sfx" },
            { "volume", evt.Volume },
            { "clip_name", evt.Clip != null ? evt.Clip.name : string.Empty }
        });
    }

    private void OnStopSoundRequested(StopSoundEvent evt)
    {
        Log(GameLogEventType.SoundRequested, metadata: new Dictionary<string, object>
        {
            { "mode", evt.StopBgm ? "StopBgm" : "StopSfx" }
        });
    }

    private void OnMoveInput(MoveInputEvent evt) => LogInput("Move", evt.Value.ToString());
    private void OnLookInput(LookInputEvent evt) => LogInput("Look", evt.Value.ToString());
    private void OnSubmitInput(SubmitInputEvent evt) => LogInput("Submit", "Triggered");
    private void OnCancelInput(CancelInputEvent evt) => LogInput("Cancel", "Triggered");
    private void OnPauseInput(PauseInputEvent evt) => LogInput("Pause", "Triggered");
    private void OnPrimaryActionInput(PrimaryActionInputEvent evt) => LogInput("PrimaryAction", evt.IsPressed ? "Pressed" : "Released");
    private void OnSecondaryActionInput(SecondaryActionInputEvent evt) => LogInput("SecondaryAction", evt.IsPressed ? "Pressed" : "Released");

    private void LogInput(string actionName, string value)
    {
        Log(GameLogEventType.InputReceived, metadata: new Dictionary<string, object>
        {
            { "action", actionName },
            { "value", value }
        });
    }

    public void LogCustom(string message, Dictionary<string, object> metadata = null, GameObject actor = null, GameObject target = null)
    {
        var resolvedMetadata = metadata ?? new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(message))
        {
            resolvedMetadata["message"] = message;
        }

        Log(GameLogEventType.Custom, actor, target, resolvedMetadata);
    }

    public void Log(GameLogEventType eventType, GameObject actor = null, GameObject target = null, Dictionary<string, object> metadata = null)
    {
        EntitySnapshot actorSnapshot = BuildEntitySnapshot(actor);
        EntitySnapshot targetSnapshot = BuildEntitySnapshot(target);

        string line = string.Join(",",
            EscapeCsv(DateTime.UtcNow.ToString("O")),
            EscapeCsv(eventType.ToString()),
            EscapeCsv(actorSnapshot.EntityId),
            EscapeCsv(actorSnapshot.EntityName),
            EscapeCsv(targetSnapshot.EntityId),
            EscapeCsv(targetSnapshot.EntityName),
            EscapeCsv(SerializeMetadata(metadata)));

        EnqueueLine(line);
    }

    private void EnqueueLine(string line)
    {
        _pendingLines.Enqueue(line);
        _flushSignal.Set();
    }

    private static EntitySnapshot BuildEntitySnapshot(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return default;
        }

        LoggableEntity loggableEntity = gameObject.GetComponent<LoggableEntity>();
        if (loggableEntity != null)
        {
            return new EntitySnapshot(loggableEntity.EntityId, loggableEntity.DisplayName);
        }

        return new EntitySnapshot(gameObject.GetInstanceID().ToString(), gameObject.name);
    }

    private static string SerializeMetadata(Dictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        bool isFirst = true;

        foreach (KeyValuePair<string, object> pair in metadata)
        {
            if (!isFirst)
            {
                builder.Append(';');
            }

            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(pair.Value);
            isFirst = false;
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        string safe = value ?? string.Empty;
        if (safe.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }

        return safe;
    }

    private readonly struct EntitySnapshot
    {
        public readonly string EntityId;
        public readonly string EntityName;

        public EntitySnapshot(string entityId, string entityName)
        {
            EntityId = entityId ?? string.Empty;
            EntityName = entityName ?? string.Empty;
        }
    }
}
