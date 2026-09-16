using SiegeCore.Cannon;
using SiegeCore.Projectile;
using TMPro;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatEncounter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _status;

        private bool _finished;

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<SiegeDestroyedEvent>(
                OnSiegeDestroyed);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<SiegeDestroyedEvent>(
                    OnSiegeDestroyed);
            }
        }


        private void OnSiegeDestroyed(
            SiegeDestroyedEvent result)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;

            if (_status != null)
            {
                _status.text =
                    result.WinningSide
                    == VehicleSide.Ally
                        ? "VICTORY"
                        : "DEFEAT";
            }

            GameManager.Instance.EndGame();
        }
    }
}
