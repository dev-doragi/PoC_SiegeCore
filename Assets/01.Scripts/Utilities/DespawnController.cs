using UnityEngine;

/// <summary>풀에서 빌린 파티클이 끝나면 해당 풀로 반환합니다.</summary>
public class DespawnController : MonoBehaviour
{
    private ParticleSystem _particleSystem;

    public void Setup(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem;
    }

    private void Update()
    {
        if (_particleSystem == null)
        {
            return;
        }

        if (!_particleSystem.isPlaying)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        PooledObject handle = GetComponent<PooledObject>();
        if (handle != null)
        {
            handle.Return();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
