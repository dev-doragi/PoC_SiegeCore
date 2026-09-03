using UnityEngine;
using UnityEngine.UI;

public class LinkEnergyOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _fillImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("Optional energy owner filter. Leave empty to receive all energy changes.")]
    [SerializeField] private UnityEngine.Object _energySource;

    [Header("Fade")]
    [SerializeField] private float _visibleHoldTime = 0.8f;
    [SerializeField] private float _fadeDuration = 0.35f;

    private float _holdTimer;
    private float _targetAlpha = 1f;
    private bool _isFull = true;
    private bool _isActive;

    private void Awake()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        SetFill(1f);
        SetAlpha(1f);
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<EnergyChangedEvent>(OnEnergyChanged);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<EnergyChangedEvent>(OnEnergyChanged);
    }

    private void Update()
    {
        if (_isFull && !_isActive)
        {
            _holdTimer += Time.unscaledDeltaTime;

            if (_holdTimer >= _visibleHoldTime)
            {
                _targetAlpha = 0f;
            }
        }

        if (_canvasGroup == null) return;

        float speed = _fadeDuration <= 0f ? 999f : 1f / _fadeDuration;
        _canvasGroup.alpha = Mathf.MoveTowards(
            _canvasGroup.alpha,
            _targetAlpha,
            speed * Time.unscaledDeltaTime
        );
    }

    private void OnEnergyChanged(EnergyChangedEvent evt)
    {
        if (_energySource != null && evt.Source != _energySource) return;

        float normalized = Mathf.Clamp01(evt.Normalized);

        SetFill(normalized);

        _isFull = normalized >= 0.999f;

        if (!_isFull)
        {
            _isActive = true;
            ShowImmediately();
            return;
        }

        _isActive = false;
        _holdTimer = 0f;
        _targetAlpha = 1f;
    }

    private void ShowImmediately()
    {
        _holdTimer = 0f;
        _targetAlpha = 1f;
        SetAlpha(1f);
    }

    private void SetFill(float normalized)
    {
        if (_fillImage == null) return;

        _fillImage.fillAmount = normalized;
    }

    private void SetAlpha(float alpha)
    {
        if (_canvasGroup == null) return;

        _canvasGroup.alpha = alpha;
    }
}
