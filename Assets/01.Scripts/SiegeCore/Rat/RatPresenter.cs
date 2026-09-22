using DG.Tweening;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    public sealed class RatPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer _renderer;

        private RatFlightMotion _motion;
        private bool _motionVisualReady;
        [Header("Presentation")]
        [SerializeField] private Transform _visual;
        private Vector3 _visualRestPosition;
        private Vector3 _visualRestScale = Vector3.one;
        private SpriteRenderer _visualRenderer;
        private SpriteRenderer _shadowRenderer;
        private Transform _shadowTransform;

        private RatPresentationData _sprites;
        private RatPresentationConfig _config;
        private bool _presentationReady;

        internal void CreateShadow()
        {
            _visualRenderer = _visual.GetComponent<SpriteRenderer>();

            if (_visualRenderer == null)
            {
                Debug.LogError(
                    "[RatPresenter] Visual requires SpriteRenderer.",
                    this);
                return;
            }

            GameObject shadowObject = new GameObject("Shadow");

            _shadowTransform = shadowObject.transform;
            _shadowTransform.SetParent(transform, false);
            _shadowTransform.localPosition = new Vector3(
                _config.ShadowOffset.x,
                _config.ShadowOffset.y,
                0f);
            _shadowTransform.localScale = new Vector3(
                _config.ShadowScale.x,
                _config.ShadowScale.y,
                1f);

            _shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            _shadowRenderer.sprite = _visualRenderer.sprite;
            _shadowRenderer.sortingLayerID = _visualRenderer.sortingLayerID;
            _shadowRenderer.sortingOrder = _visualRenderer.sortingOrder - 1;

            Color shadowColor = Color.black;
            shadowColor.a = _config.ShadowAlpha;
            _shadowRenderer.color = shadowColor;
        }

        internal void UpdateHeightPresentation()
        {
            if (_visual == null)
            {
                return;
            }

            _visual.localPosition =
                _motion.IsCannonFlight
                    ? _visualRestPosition
                    : _visualRestPosition + Vector3.up * _motion.Height;

            if (_shadowTransform == null
                || _shadowRenderer == null)
            {
                return;
            }

            float shadowY = _config.ShadowOffset.y;

            if (_motion.IsCannonFlight)
            {
                shadowY -= _motion.Height;
            }

            _shadowTransform.localPosition = new Vector3(
                _config.ShadowOffset.x,
                shadowY,
                0f);

            float heightRatio = Mathf.Clamp01(_motion.Height / 2f);
            float scaleMultiplier = Mathf.Lerp(
                1f,
                _config.ShadowAirScale,
                heightRatio);

            _shadowTransform.localScale = new Vector3(
                _config.ShadowScale.x * scaleMultiplier,
                _config.ShadowScale.y * scaleMultiplier,
                1f);

            _shadowRenderer.enabled =
                !_motion.IsCarried && !_motion.IsLoaded;
        }

        public void RefreshShadowSprite()
        {
            if (_visualRenderer != null
                && _shadowRenderer != null)
            {
                _shadowRenderer.sprite = _visualRenderer.sprite;
            }
        }

        internal void ResetVisualHeight()
        {
            if (_visual != null)
            {
                _visual.localPosition =
                    _visualRestPosition;
            }
        }

        internal void ApplySquash(Vector2 impactNormal)
        {
            if (_visual == null)
            {
                return;
            }

            Vector3 scale = _visualRestScale;
            if (Mathf.Abs(impactNormal.x) >= Mathf.Abs(impactNormal.y))
            {
                scale.x *= _config.SquashRatio;
                scale.y /= Mathf.Max(0.1f, _config.SquashRatio);
            }
            else
            {
                scale.y *= _config.SquashRatio;
                scale.x /= Mathf.Max(0.1f, _config.SquashRatio);
            }

            _visual.localScale = scale;
        }

        internal void ResetVisualScale()
        {
            if (_visual != null)
            {
                _visual.localScale = _visualRestScale;
            }
        }

        internal Transform Visual { get { return _visual; } }
        internal Vector3 RestPosition { get { return _visualRestPosition; } }
        internal Vector3 RestScale { get { return _visualRestScale; } }

        internal bool InitializeMotionVisual()
        {
            if (_motionVisualReady) return true;
            if (!_presentationReady && !TryLoadPresentation()) return false;

            _motion = GetComponent<RatFlightMotion>();
            if (_visual == null || _visual == transform || _visual.GetComponent<SpriteRenderer>() == null)
            {
                Debug.LogError("[RatPresenter] Assign a separate Visual child with a SpriteRenderer.", this);
                return false;
            }
            _visualRestPosition = _visual.localPosition;
            _visualRestScale = _visual.localScale;
            CreateShadow();
            _motionVisualReady = true;
            return true;
        }

        private void LateUpdate()
        {
            if (_motionVisualReady) UpdateHeightPresentation();
        }

        private RatAgent _agent;
        private RatGroundBehaviour _groundAI;

        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private int _groundSortingOrder;

        private float _animationSeed;
        private Tween _deathTween;

        public bool IsConfigured
        {
            get
            {
                RatAgent agent = GetComponent<RatAgent>();
                return _renderer != null
                    && _visual != null
                    && _visual != transform
                    && _visual.IsChildOf(transform)
                    && _visual.GetComponent<SpriteRenderer>() != null
                    && agent != null
                    && agent.Definition != null
                    && agent.Definition.Presentation.HasRequiredSprites
                    && agent.Definition.PresentationConfig != null;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
            _groundAI = GetComponent<RatGroundBehaviour>();

            if (_renderer == null)
            {
                Debug.LogError("[RatPresenter] Renderer is not assigned.", this);
                enabled = false;
                return;
            }

            if (!TryLoadPresentation())
            {
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
            if (!_presentationReady && !TryLoadPresentation()) return;

            _agent.StateChanged += HandleStateChanged;

            ResetVisual();
            Refresh(_agent.State);
        }

        private bool TryLoadPresentation()
        {
            if (_agent == null)
            {
                _agent = GetComponent<RatAgent>();
            }

            if (_agent == null || _agent.Definition == null)
            {
                Debug.LogError("[RatPresenter] RatDefinition is not assigned.", this);
                return false;
            }

            _sprites = _agent.Definition.Presentation;
            _config = _agent.Definition.PresentationConfig;
            if (!_sprites.HasRequiredSprites)
            {
                Debug.LogError("[RatPresenter] RatDefinition needs Ground, Projectile, and Dead sprites.", this);
                return false;
            }

            if (_config == null)
            {
                Debug.LogError("[RatPresenter] RatDefinition needs a RatPresentationConfig.", this);
                return false;
            }

            _presentationReady = true;
            _renderer.sprite = _sprites.GroundSprite;
            return true;
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
                if (_groundAI != null && _groundAI.IsMoving)
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
                    _renderer.sprite = _sprites.GroundSprite;
                    break;

                case RatState.Carried:
                case RatState.Airborne:
                case RatState.Loaded:
                case RatState.CannonFlight:
                    _renderer.sprite = _sprites.ProjectileSprite;
                    break;

                case RatState.Dead:
                    PlayDeath();
                    return;
            }

            RefreshShadowSprite();
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
                ? _config.EnemyColor
                : _config.AllyColor;

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
                Time.time * _config.BounceSpeed + _animationSeed);

            float wiggle = Mathf.Sin(
                Time.time * _config.WiggleSpeed + _animationSeed * 0.5f);

            _renderer.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                wiggle * _config.WiggleAmount * 30f);

            ApplyScale(
                bounce * _config.StretchAmount,
                _config.BounceAmount * Mathf.Abs(bounce));
        }

        private void ApplyIdleAnimation()
        {
            float breath =
                Mathf.Sin(
                    Time.time * _config.IdleBreathSpeed + _animationSeed)
                * _config.IdleBreathAmount;

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

            _renderer.sprite = _sprites.DeadSprite;
            RefreshShadowSprite();

            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                    _renderer.transform.DOLocalMoveY(
                    _basePosition.y - _config.DeathSinkDistance,
                    _config.DeathDuration));

            sequence.Join(
                _renderer.DOFade(
                    0f,
                    _config.DeathDuration));

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
