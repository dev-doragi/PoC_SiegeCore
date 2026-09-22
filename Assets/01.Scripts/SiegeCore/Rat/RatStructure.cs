using System;
using System.Collections.Generic;
using SiegeCore.Cannon;
using SiegeCore.Combat;
using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatStructure : MonoBehaviour, IDamageable, IGroundCombatTarget
    {
        public static readonly HashSet<RatStructure> Active = new HashSet<RatStructure>();
        [SerializeField] private VehicleSide _faction;
        [SerializeField] private bool _isEntrance;
        [SerializeField, Min(1f)] private float _maxHealth = 30f;
        [SerializeField] private Collider2D _barrier;
        [SerializeField] private SpriteRenderer _visual;
        public VehicleSide Faction { get { return _faction; } }
        public VehicleSide Side { get { return _faction; } }
        public bool IsEntrance { get { return _isEntrance; } }
        public float Health { get; private set; }
        public float MaxHealth { get { return _maxHealth; } }
        public bool IsDestroyed { get { return Health <= 0f; } }
        public bool IsDead { get { return IsDestroyed; } }
        public Transform TargetTransform { get { return transform; } }
        public int AttackPriority { get { return 0; } }
        public event Action<RatStructure> Destroyed;

        private void Awake()
        {
            Health = _maxHealth;
        }

        private void OnEnable()
        {
            Active.Add(this);
            if (IsDestroyed) ApplyDestroyedState();
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public void TakeDamage(VehicleSide attacker, float damage)
        {
            if (attacker == _faction || IsDestroyed || damage <= 0f)
            {
                return;
            }

            Health = Mathf.Max(0f, Health - damage);
            if (!IsDestroyed)
            {
                return;
            }

            ApplyDestroyedState();
            LogDestruction();
            Destroyed?.Invoke(this);
        }

        public void TakeDamage(DamageData damageData)
        {
            TakeDamage(damageData.AttackerSide, damageData.Damage);
        }

        private void ApplyDestroyedState()
        {
            if (_barrier != null)
            {
                _barrier.enabled = false;
            }
            if (_visual != null)
            {
                _visual.color = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            }
        }

        private void LogDestruction()
        {
            if (_isEntrance)
            {
                Debug.Log($"[Entrance] {_faction} entrance opened.", this);
            }
            else
            {
                Debug.Log($"[Sabotage] {_faction} {name} destroyed.", this);
            }
        }
    }
}
