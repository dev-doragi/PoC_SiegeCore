using DG.Tweening;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    [RequireComponent(typeof(RatGroundAI))]
    public sealed class RatPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer _renderer;

        [Header("Sprites")]
        [SerializeField] private Sprite _groundSprite;
        [SerializeField] private Sprite _projectileSprite;
        [SerializeField] private Sprite _deadSprite;

        [Header("Faction")]
        [SerializeField] private Color _allyColor = Color.white;
        [SerializeField] private Color _enemyColor = new Color(1f, 0.55f, 0.55f, 1f);

        [Header("Moving")]
        [SerializeField] private float _wiggleAmount = 0.09f;
        [SerializeField] private float _wiggleSpeed = 20f;
        [SerializeField] private float _bounceAmount = 0.08f;
        [SerializeField] private float _bounceSpeed = 16f;
        [SerializeField] private float _stretchAmount = 0.08f;

        [Header("Idle")]
        [SerializeField] private float _idleBreathAmount = 0.025f;
        [SerializeField] private float _idleBreathSpeed = 3.2f;

        [Header("Death")]
        [SerializeField] private float _deathSinkDistance = 0.3f;
        [SerializeField] private float _deathDuration = 0.6f;

        private RatAgent _agent;
        private RatGroundAI _groundAI;

        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private int _groundSortingOrder;

        private float _animationSeed;
        private Tween _deathTween;

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _groundAI = GetComponent<RatGroundAI>();

            if (_renderer == null)
            {
                Debug.LogError("[RatPresentation] Renderer is not assigned.", this);
                enabled = false;
                return;
            }

            _baseScale = _renderer.transform.localScale;
            _basePosition = _renderer.transform.localPosition;
            _groundSortingOrder = _renderer.sortingOrder;
            _animationSeed = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            _agent.StateChanged += HandleStateChanged;

            ResetVisual();
            Refresh(_agent.State);
        }

        private void OnDisable()
        {
            _agent.StateChanged -= HandleStateChanged;

            if (_deathTween != null)
            {
                _deathTween.Kill();
                _deathTween = null;
            }
        }

        private void Update()
        {
            if (_agent.State == RatState.Dead)
            {
                return;
            }

            ApplyFactionColor();

            if (_agent.State == RatState.Idle
                || _agent.State == RatState.GroundCombat)
            {
                if (_groundAI.IsMoving)
                {
                    ApplyFacing();
                    ApplyMovingAnimation();
                }
                else
                {
                    ApplyIdleAnimation();
                }

                return;
            }

            if (_agent.State == RatState.Groggy)
            {
                ApplyIdleAnimation();
            }
        }

        private void HandleStateChanged(
            RatAgent agent,
            RatState previousState,
            RatState nextState)
        {
            Refresh(nextState);
        }

        private void Refresh(RatState state)
        {
            if (state != RatState.Dead
                && _deathTween != null)
            {
                _deathTween.Kill();
                _deathTween = null;
            }

            if (state != RatState.Dead)
            {
                Color color = _renderer.color;
                color.a = 1f;
                _renderer.color = color;
                _renderer.transform.localPosition = _basePosition;
            }

            ApplyFactionColor();
            ApplyProjectileSorting(state);

            switch (state)
            {
                case RatState.Idle:
                case RatState.Groggy:
                case RatState.GroundCombat:
                    _renderer.sprite = _groundSprite;
                    break;

                case RatState.Carried:
                case RatState.Airborne:
                case RatState.Loaded:
                case RatState.CannonFlight:
                    _renderer.sprite = _projectileSprite;
                    break;

                case RatState.Dead:
                    PlayDeath();
                    return;
            }

            _agent.Carryable.RefreshShadowSprite();
            ResetMotionVisual();
        }

        private void ApplyProjectileSorting(RatState state)
        {
            if (state != RatState.CannonFlight)
            {
                _renderer.sortingOrder = _groundSortingOrder;
                return;
            }

            int group = GetSortingGroup(_agent.ProjectileSourceSlot);

            if (_agent.AttackSide == Cannon.VehicleSide.Enemy)
            {
                group = 2 - group;
            }

            _renderer.sortingOrder = group * 100;
        }

        private static int GetSortingGroup(Cannon.CannonSlot sourceSlot)
        {
            if (sourceSlot == null)
            {
                return 1;
            }

            if (sourceSlot.SlotType == Cannon.CannonSlotType.Left)
            {
                return 0;
            }

            if (sourceSlot.SlotType == Cannon.CannonSlotType.Right)
            {
                return 2;
            }

            return 1;
        }

        private void ApplyFactionColor()
        {
            Color color = _agent.Faction == Cannon.VehicleSide.Enemy
                ? _enemyColor
                : _allyColor;

            color.a = _renderer.color.a;
            _renderer.color = color;
        }

        private void ApplyFacing()
        {
            if (Mathf.Abs(_groundAI.MoveDirection.x) <= 0.01f)
            {
                return;
            }

            float facing = _groundAI.MoveDirection.x >= 0f ? 1f : -1f;

            Vector3 scale = _renderer.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;

            _renderer.transform.localScale = scale;
        }

        private void ApplyMovingAnimation()
        {
            float bounce = Mathf.Sin(
                Time.time * _bounceSpeed + _animationSeed);

            float wiggle = Mathf.Sin(
                Time.time * _wiggleSpeed + _animationSeed * 0.5f);

            _renderer.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                wiggle * _wiggleAmount * 30f);

            ApplyScale(
                bounce * _stretchAmount,
                _bounceAmount * Mathf.Abs(bounce));
        }

        private void ApplyIdleAnimation()
        {
            float breath =
                Mathf.Sin(
                    Time.time * _idleBreathSpeed + _animationSeed)
                * _idleBreathAmount;

            _renderer.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                breath * 8f);

            ApplyScale(breath * 0.5f, 0f);
        }

        private void ApplyScale(float stretch, float squash)
        {
            float facing = Mathf.Sign(_renderer.transform.localScale.x);

            if (Mathf.Approximately(facing, 0f))
            {
                facing = 1f;
            }

            float width = Mathf.Max(
                0.85f,
                1f - stretch + squash);

            float height = Mathf.Max(
                0.85f,
                1f + stretch - squash);

            _renderer.transform.localScale = new Vector3(
                Mathf.Abs(_baseScale.x) * width * facing,
                _baseScale.y * height,
                _baseScale.z);
        }

        private void PlayDeath()
        {
            if (_deathTween != null)
            {
                _deathTween.Kill();
            }

            _renderer.sprite = _deadSprite;
            _agent.Carryable.RefreshShadowSprite();

            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                _renderer.transform.DOLocalMoveY(
                    _basePosition.y - _deathSinkDistance,
                    _deathDuration));

            sequence.Join(
                _renderer.DOFade(
                    0f,
                    _deathDuration));

            sequence.OnComplete(_agent.Release);

            _deathTween = sequence;
        }

        private void ResetMotionVisual()
        {
            float facing = Mathf.Sign(_renderer.transform.localScale.x);

            if (Mathf.Approximately(facing, 0f))
            {
                facing = 1f;
            }

            _renderer.transform.localRotation = Quaternion.identity;

            _renderer.transform.localScale = new Vector3(
                Mathf.Abs(_baseScale.x) * facing,
                _baseScale.y,
                _baseScale.z);
        }

        private void ResetVisual()
        {
            Color color = _renderer.color;
            color.a = 1f;

            _renderer.color = color;
            _renderer.transform.localPosition = _basePosition;

            ResetMotionVisual();
        }
    }
}
