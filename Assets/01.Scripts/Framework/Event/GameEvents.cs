using UnityEngine;

public struct GameStateChangedEvent
{
    public GameState PreviousState;
    public GameState NewState;
}

public struct InGameStateChangedEvent
{
    public InGameState PreviousState;
    public InGameState NewState;
}

public struct PauseRequestedEvent
{
    public bool Pause;
}

public struct SceneLoadRequestedEvent
{
    public string SceneName;
}

public struct SceneLoadedEvent
{
    public string SceneName;
}

public struct InputDeviceChangedEvent
{
    public string DeviceName;
}

public struct CameraShakeEvent
{
    public ShakeIntensity Intensity;
}

public struct PlaySoundEvent
{
    public AudioClip Clip;
    public float Volume;
    public bool IsBgm;
}

public struct StopSoundEvent
{
    public bool StopBgm;
}

public struct TimeScaleChangedEvent
{
    public float NewTimeScale;
}

public struct HitStopRequestedEvent
{
    public float Duration;
    public float TimeScale;
}

public struct SlowMotionRequestedEvent
{
    public float Duration;
    public float TimeScale;
}

public struct MoveInputEvent
{
    public Vector2 Value;
}

public struct LookInputEvent
{
    public Vector2 Value;
}

/// <summary>Any energy pool can publish its owner and current capacity.</summary>
public struct EnergyChangedEvent
{
    public UnityEngine.Object Source;
    public float Current;
    public float Max;

    public float Normalized => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
}

public struct SubmitInputEvent { }
public struct CancelInputEvent { }
public struct PauseInputEvent { }

public struct PrimaryActionInputEvent
{
    public bool IsPressed;
}

public struct SecondaryActionInputEvent
{
    public bool IsPressed;
}
