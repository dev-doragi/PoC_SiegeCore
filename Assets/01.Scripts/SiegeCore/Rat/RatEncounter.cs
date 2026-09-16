using SiegeCore.Cannon;
using SiegeCore.Projectile;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CannonController = SiegeCore.Cannon.Cannon;

namespace SiegeCore.Rat
{
    public sealed class RatEncounter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RatFactory _factory;
        [SerializeField] private Transform _enemyExit;

        [Tooltip("비워두면 설치된 Enemy Cannon들을 자동으로 찾습니다.")]
        [SerializeField]
        private CannonController[] _enemyCannons;

        [SerializeField] private TMP_Text _status;

        [Header("Intervals")]
        [SerializeField, Min(0.2f)]
        private float _enemyUnitInterval = 7f;

        [SerializeField, Min(0.2f)]
        private float _enemyShotInterval = 5f;

        [Header("Enemy Ground Units")]
        [SerializeField]
        private RatForm[] _enemyUnits =
        {
            RatForm.Basic
        };

        [Header("Enemy Cannon Ammo")]
        [SerializeField]
        private RatForm[] _enemyShots =
        {
            RatForm.Basic
        };

        private float _nextUnit;
        private float _nextShot;

        private int _unitIndex;
        private int _shotIndex;
        private int _cannonIndex;

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

        private void Start()
        {
            _nextUnit =
                Time.time + _enemyUnitInterval;

            _nextShot =
                Time.time + _enemyShotInterval;
        }

        private void Update()
        {
            if (_finished
                || Time.timeScale <= 0f
                || _factory == null
                || !_factory.IsReady)
            {
                return;
            }

            UpdateEnemyGroundUnits();
            UpdateEnemyCannons();
        }

        private void UpdateEnemyGroundUnits()
        {
            if (Time.time < _nextUnit
                || _enemyExit == null
                || _enemyUnits == null
                || _enemyUnits.Length == 0)
            {
                return;
            }

            _nextUnit =
                Time.time + _enemyUnitInterval;

            RatForm form =
                _enemyUnits[
                    _unitIndex % _enemyUnits.Length];

            _unitIndex++;

            _factory.SpawnGroundCombat(
                form,
                VehicleSide.Enemy,
                _enemyExit.position);
        }

        private void UpdateEnemyCannons()
        {
            if (Time.time < _nextShot
                || _enemyShots == null
                || _enemyShots.Length == 0)
            {
                return;
            }

            _nextShot =
                Time.time + _enemyShotInterval;

            RatForm form =
                _enemyShots[
                    _shotIndex % _enemyShots.Length];

            _shotIndex++;

            TryLoadEnemyCannon(form);
        }

        private void TryLoadEnemyCannon(
            RatForm form)
        {
            CannonController cannon =
                FindAvailableEnemyCannon();

            if (cannon == null)
            {
                return;
            }

            RatAgent ammunition =
                _factory.Spawn(
                    form,
                    VehicleSide.Enemy,
                    cannon.GetLoadingPosition(),
                    false,
                    true);

            if (ammunition == null)
            {
                return;
            }

            if (cannon.TryLoad(
                    ammunition.Carryable))
            {
                return;
            }

            ammunition.Release();
        }

        private CannonController FindAvailableEnemyCannon()
        {
            FindEnemyCannonsWhenEmpty();

            if (_enemyCannons == null
                || _enemyCannons.Length == 0)
            {
                return null;
            }

            for (int offset = 0;
                 offset < _enemyCannons.Length;
                 offset++)
            {
                int index =
                    (_cannonIndex + offset)
                    % _enemyCannons.Length;

                CannonController cannon =
                    _enemyCannons[index];

                if (cannon == null
                    || !cannon.IsInstalledFor(
                        VehicleSide.Enemy)
                    || cannon.IsFull)
                {
                    continue;
                }

                _cannonIndex =
                    (index + 1)
                    % _enemyCannons.Length;

                return cannon;
            }

            return null;
        }

        private void FindEnemyCannonsWhenEmpty()
        {
            if (_enemyCannons != null
                && _enemyCannons.Length > 0)
            {
                return;
            }

            CannonController[] allCannons =
                FindObjectsByType<CannonController>(
                    FindObjectsSortMode.None);

            List<CannonController> enemyCannons =
                new List<CannonController>();

            foreach (CannonController cannon in allCannons)
            {
                if (cannon != null
                    && cannon.IsInstalledFor(
                        VehicleSide.Enemy))
                {
                    enemyCannons.Add(cannon);
                }
            }

            _enemyCannons =
                enemyCannons.ToArray();
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