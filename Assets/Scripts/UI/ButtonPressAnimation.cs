using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Juicy" press animation for a shadowed button, driven by LeanTween.
///
/// The setup this is built for: a button face (this object, e.g. Roll_BTN) sits
/// slightly ABOVE its darker parent (e.g. Roll), and the parent peeks out at the
/// bottom as a drop shadow. On press we slide the face DOWN so it covers that
/// shadow — the button looks pushed in. On release it springs back up.
///
/// Attach this to the FACE object (the one with the Button, e.g. Roll_BTN).
/// No Inspector wiring is required: the RectTransform, the Button and the press
/// depth are all resolved automatically. Every field below is optional tuning.
///
/// Why LeanTween and not an Animator: for a two-state move like this, a tween is
/// lighter and needs no animator/controller/clips. An Animator would also fight
/// you here because it keeps writing the animated position every frame.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonPressAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Press depth")]
    [Tooltip("Auto-measure how far to sink from the shadow gap (recommended). " +
             "Uses anchoredPosition.y - sizeDelta.y/2, i.e. the bottom gap left for the shadow.")]
    [SerializeField] private bool autoDetectDepth = true;
    [Tooltip("Manual sink distance (UI units) when auto-detect is off or measures ~0.")]
    [SerializeField] private float pressDepth = 2.6f;

    [Header("Timing")]
    [SerializeField] private float pressDuration = 0.06f;
    [SerializeField] private float releaseDuration = 0.14f;
    [SerializeField] private LeanTweenType pressEase = LeanTweenType.easeOutQuad;
    [Tooltip("A little overshoot on the way back up reads as 'springy'.")]
    [SerializeField] private LeanTweenType releaseEase = LeanTweenType.easeOutBack;

    [Header("Optional squash")]
    [Tooltip("Also shrink slightly while pressed for extra feedback.")]
    [SerializeField] private bool useScale = false;
    [Range(0.8f, 1f)]
    [SerializeField] private float pressedScale = 0.97f;

    private RectTransform _rect;
    private Button _button;   // optional; if present, a non-interactable button won't animate
    private float _restY;     // resting anchoredPosition.y
    private Vector3 _restScale;
    private float _depth;
    private bool _pressed;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _button = GetComponent<Button>();
        _restY = _rect.anchoredPosition.y;
        _restScale = _rect.localScale;

        // Bottom shadow gap = anchoredPosition.y - sizeDelta.y/2 (with stretch anchors,
        // face shorter than parent and nudged up). Falls back to the manual value.
        float measured = _rect.anchoredPosition.y - _rect.sizeDelta.y * 0.5f;
        _depth = (autoDetectDepth && measured > 0.01f) ? measured : pressDepth;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _pressed = true;
        AnimateTo(_restY - _depth, pressDuration, pressEase, useScale ? pressedScale : 1f);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    // If the finger/cursor slides off while held down, spring back too.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_pressed) Release();
    }

    private void Release()
    {
        if (!_pressed) return;
        _pressed = false;
        AnimateTo(_restY, releaseDuration, releaseEase, 1f);
    }

    private void AnimateTo(float targetY, float time, LeanTweenType ease, float scaleMul)
    {
        // Cancel any in-flight tweens on this object so presses can't stack/drift.
        LeanTween.cancel(gameObject);

        LeanTween.value(gameObject, _rect.anchoredPosition.y, targetY, time)
            .setEase(ease)
            .setOnUpdate((float y) =>
            {
                Vector2 p = _rect.anchoredPosition;
                p.y = y;
                _rect.anchoredPosition = p;
            })
            .setIgnoreTimeScale(true); // works even if the game is paused (timeScale 0)

        if (useScale)
        {
            LeanTween.scale(gameObject, _restScale * scaleMul, time)
                .setEase(ease)
                .setIgnoreTimeScale(true);
        }
    }

    // If the object is disabled mid-press, don't leave it stuck down.
    private void OnDisable()
    {
        LeanTween.cancel(gameObject);
        if (_rect != null)
        {
            Vector2 p = _rect.anchoredPosition;
            p.y = _restY;
            _rect.anchoredPosition = p;
            _rect.localScale = _restScale;
        }
        _pressed = false;
    }
}
