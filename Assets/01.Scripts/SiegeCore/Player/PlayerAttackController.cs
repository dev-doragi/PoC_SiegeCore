using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Rat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SiegeCore.Player
{
    [RequireComponent(typeof(PlayerAimController))]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private Vector2 _attackSize = new Vector2(1.5f, 0.8f);
        [SerializeField, Min(0f)] private float _attackDistance = 0.9f;
        [SerializeField, Min(0f)] private float _cooldown = 0.25f;
        [SerializeField] private LayerMask _targetLayers = ~0;

        [Header("Knockback")]
        [SerializeField] private float _knockbackVertical = 0.15f;
        [SerializeField] private float _throwHeight = 1.5f;

        [Header("Debug Visual")]
        [SerializeField] private GameObject _attackVisual;
        [SerializeField, Min(0f)] private float _visualDuration = 0.1f;

        private PlayerAimController _aim;

        private float _nextAttackTime;
        private float _visualEndTime;

        private readonly HashSet<RatAgent> _hitRats =
            new HashSet<RatAgent>();

        private void Awake()
        {
            _aim = GetComponent<PlayerAimController>();

            if (_attackVisual != null)
            {
                _attackVisual.SetActive(false);
            }
        }

        private void Update()
        {
            // PoC 임시 입력.
            if (Keyboard.current != null
                && Keyboard.current.fKey.wasPressedThisFrame)
            {
                TryAttack();
            }

            if (_attackVisual != null
                && _attackVisual.activeSelf
                && Time.time >= _visualEndTime)
            {
                _attackVisual.SetActive(false);
            }
        }

        public void TryAttack()
        {
            if (Time.timeScale <= 0f
                || Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + _cooldown;

            int facing = _aim.FacingSign;

            Vector2 center =
                (Vector2)transform.position
                + Vector2.right * facing * _attackDistance;

            Collider2D[] hits = Physics2D.OverlapBoxAll(
                center,
                _attackSize,
                0f,
                _targetLayers);

            _hitRats.Clear();

            foreach (Collider2D hit in hits)
            {
                RatAgent rat =
                    hit.GetComponentInParent<RatAgent>();

                if (rat == null
                    || rat.Faction != VehicleSide.Enemy
                    || !_hitRats.Add(rat))
                {
                    continue;
                }

                Vector2 knockbackDirection = new Vector2(
                    facing,
                    _knockbackVertical).normalized;

                rat.KnockbackToGroggy(
                    knockbackDirection,
                    _throwHeight);
            }

            PlayAttackVisual(facing);
        }

        private void PlayAttackVisual(int facing)
        {
            if (_attackVisual == null)
            {
                return;
            }

            Vector3 localPosition =
                _attackVisual.transform.localPosition;

            localPosition.x =
                Mathf.Abs(localPosition.x) * facing;

            _attackVisual.transform.localPosition =
                localPosition;

            Vector3 scale =
                _attackVisual.transform.localScale;

            scale.x =
                Mathf.Abs(scale.x) * facing;

            _attackVisual.transform.localScale = scale;

            _attackVisual.SetActive(true);

            _visualEndTime =
                Time.time + _visualDuration;
        }

        private void OnDrawGizmosSelected()
        {
            PlayerAimController aim =
                GetComponent<PlayerAimController>();

            int facing =
                aim != null ? aim.FacingSign : 1;

            Vector2 center =
                (Vector2)transform.position
                + Vector2.right * facing * _attackDistance;

            Gizmos.DrawWireCube(
                center,
                _attackSize);
        }
    }
}