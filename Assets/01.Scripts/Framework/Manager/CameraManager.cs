using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-730)]
public class CameraManager : ManagedBehaviour
{
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    protected override void OnInitialize()
    {
        if (_mainCamera == null || _cinemachineCamera == null || _impulseSource == null)
        {
            throw new System.InvalidOperationException("Assign the scene camera references explicitly.");
        }

    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<CameraShakeEvent>(OnCameraShake);
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<CameraShakeEvent>(OnCameraShake);
    }

    private void OnCameraShake(CameraShakeEvent evt)
    {
        if (_impulseSource == null)
        {
            Debug.LogError("[CameraManager] Cannot shake camera. Impulse source is missing.", this);
            return;
        }

        float amplitude = evt.Intensity switch
        {
            ShakeIntensity.Weak => 0.3f,
            ShakeIntensity.Medium => 0.6f,
            ShakeIntensity.Strong => 1f,
            _ => 0.3f
        };

        _impulseSource.GenerateImpulse(amplitude);
    }

    public void ShakeWeak()
    {
        EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Weak });
    }

    public void ShakeMedium()
    {
        EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Medium });
    }

    public void ShakeStrong()
    {
        EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Strong });
    }
}
