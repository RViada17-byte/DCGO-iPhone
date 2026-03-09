using UnityEngine;

[DisallowMultipleComponent]
public sealed class IPhoneSafeAreaRoot : MonoBehaviour
{
    RectTransform _rectTransform;
    Rect _lastSafeArea = Rect.zero;
    Vector2Int _lastScreenSize = Vector2Int.zero;

    void Awake()
    {
        _rectTransform = transform as RectTransform;
        ApplySafeArea(force: true);
    }

    void OnEnable()
    {
        ApplySafeArea(force: true);
    }

    void LateUpdate()
    {
        ApplySafeArea();
    }

    void ApplySafeArea(bool force = false)
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_rectTransform == null)
        {
            return;
        }

        if (!Application.isMobilePlatform)
        {
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            return;
        }

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        if (!force &&
            safeArea == _lastSafeArea &&
            _lastScreenSize.x == screenWidth &&
            _lastScreenSize.y == screenHeight)
        {
            return;
        }

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= screenWidth;
        anchorMin.y /= screenHeight;
        anchorMax.x /= screenWidth;
        anchorMax.y /= screenHeight;

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;

        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(screenWidth, screenHeight);
    }
}
