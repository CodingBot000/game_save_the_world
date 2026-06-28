using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(250)]
public class BattleAimPointTargetingPresenter : MonoBehaviour
{
    private const string RootName = "AimPointTargetingRoot";
    private const string MarkerNamePrefix = "AimPointTargetMarker_";

    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private BossController bossController;
    [SerializeField] private Sprite normalBaseSprite;
    [SerializeField] private Sprite normalInnerSprite;
    [SerializeField] private Sprite alertBaseSprite;
    [SerializeField] private Sprite alertInnerSprite;
    [SerializeField] private Vector2 markerSize = new(66f, 59.333f);
    [SerializeField] private Vector2 screenOffset = Vector2.zero;
    [SerializeField, Min(0.1f)] private float criticalRetargetInterval = 5f;
    [SerializeField, Range(0f, 1f)] private float normalCriticalChance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float targetTimingCriticalChance = 0.2f;
    [SerializeField, Min(0.1f)] private float innerBlinkFrequency = 5f;
    [SerializeField, Range(0f, 1f)] private float innerBlinkMinAlpha = 0.15f;

    private readonly List<Transform> aimPointBuffer = new();
    private readonly List<Transform> candidateBuffer = new();
    private readonly List<AimPointTargetMarkerView> markerViews = new();
    private RectTransform targetingRoot;
    private Transform selectedAimPoint;
    private Transform criticalWindowAimPoint;
    private float criticalRetargetRemaining;

    public Transform SelectedAimPoint => selectedAimPoint;
    public Transform CriticalWindowAimPoint => criticalWindowAimPoint;

    private void OnEnable()
    {
        criticalRetargetRemaining = 0f;
    }

    private void LateUpdate()
    {
        ResolveReferences();
        if (targetCanvas == null || bossController == null)
        {
            SetMarkersVisible(false);
            return;
        }

        EnsureRoot();
        RefreshAimPointBuffer();
        EnsureMarkers();
        ValidateSelectedAimPoint();
        UpdateCriticalWindow(Time.deltaTime);
        UpdateMarkerViews();
    }

    public void Configure(Canvas canvas, BossController boss)
    {
        if (canvas != null)
        {
            targetCanvas = canvas;
        }

        if (boss != null)
        {
            bossController = boss;
        }

        criticalRetargetRemaining = 0f;
        ResolveReferences();
        EnsureRoot();
        RefreshAimPointBuffer();
        EnsureMarkers();
        UpdateCriticalWindow(0f);
        UpdateMarkerViews();
    }

    public void ConfigureSprites(
        Sprite normalBase,
        Sprite normalInner,
        Sprite alertBase,
        Sprite alertInner)
    {
        normalBaseSprite = normalBase;
        normalInnerSprite = normalInner;
        alertBaseSprite = alertBase;
        alertInnerSprite = alertInner;
        for (int i = 0; i < markerViews.Count; i++)
        {
            ApplyMarkerState(markerViews[i]);
        }
    }

    public bool TryGetSelectedAimPoint(out Transform aimPoint)
    {
        if (selectedAimPoint != null)
        {
            aimPoint = selectedAimPoint;
            return true;
        }

        aimPoint = null;
        return false;
    }

    public float GetCriticalChanceForShot(Transform aimPoint, bool targetWasUserSelected)
    {
        if (targetWasUserSelected && aimPoint != null && aimPoint == criticalWindowAimPoint)
        {
            return Mathf.Clamp01(targetTimingCriticalChance);
        }

        return Mathf.Clamp01(normalCriticalChance);
    }

    public void SelectAimPoint(Transform aimPoint)
    {
        if (aimPoint == null || !ContainsAimPoint(aimPoint))
        {
            return;
        }

        selectedAimPoint = aimPoint;
        UpdateMarkerViews();
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindAnyObjectByType<Canvas>();
        }

        if (bossController == null)
        {
            bossController = FindAnyObjectByType<BossController>();
        }
    }

    private void EnsureRoot()
    {
        if (targetCanvas == null)
        {
            return;
        }

        if (targetingRoot != null)
        {
            return;
        }

        Transform existingRoot = targetCanvas.transform.Find(RootName);
        if (existingRoot != null)
        {
            targetingRoot = existingRoot as RectTransform;
        }

        if (targetingRoot == null)
        {
            GameObject rootObject = new(RootName);
            targetingRoot = rootObject.AddComponent<RectTransform>();
            targetingRoot.SetParent(targetCanvas.transform, false);
        }

        targetingRoot.anchorMin = Vector2.zero;
        targetingRoot.anchorMax = Vector2.one;
        targetingRoot.offsetMin = Vector2.zero;
        targetingRoot.offsetMax = Vector2.zero;
        targetingRoot.pivot = new Vector2(0.5f, 0.5f);
        targetingRoot.localScale = Vector3.one;
        targetingRoot.localRotation = Quaternion.identity;
        targetingRoot.SetAsLastSibling();
    }

    private void RefreshAimPointBuffer()
    {
        aimPointBuffer.Clear();
        if (bossController == null)
        {
            return;
        }

        int count = bossController.GetCombatAimPointCount();
        for (int i = 0; i < count; i++)
        {
            Transform aimPoint = bossController.GetCombatAimPoint(i);
            if (aimPoint != null && !aimPointBuffer.Contains(aimPoint))
            {
                aimPointBuffer.Add(aimPoint);
            }
        }
    }

    private void EnsureMarkers()
    {
        if (targetingRoot == null)
        {
            return;
        }

        if (MarkersMatchAimPoints())
        {
            return;
        }

        ClearMarkers();
        for (int i = 0; i < aimPointBuffer.Count; i++)
        {
            Transform aimPoint = aimPointBuffer[i];
            GameObject markerObject = new($"{MarkerNamePrefix}{aimPoint.name}");
            RectTransform markerRect = markerObject.AddComponent<RectTransform>();
            markerRect.SetParent(targetingRoot, false);
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = markerSize;
            markerRect.localScale = Vector3.one;
            markerRect.localRotation = Quaternion.identity;

            Image hitImage = markerObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            BattleAimPointTargetMarker marker = markerObject.AddComponent<BattleAimPointTargetMarker>();
            marker.Configure(this, aimPoint);

            Image baseImage = CreateMarkerImage("Base", markerRect);
            Image innerImage = CreateMarkerImage("Inner", markerRect);
            AimPointTargetMarkerView markerView = new(aimPoint, markerRect, baseImage, innerImage);
            markerViews.Add(markerView);
        }
    }

    private Image CreateMarkerImage(string name, RectTransform parent)
    {
        GameObject imageObject = new(name);
        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.SetParent(parent, false);
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = markerSize;
        imageRect.localScale = Vector3.one;
        imageRect.localRotation = Quaternion.identity;

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
        return image;
    }

    private bool MarkersMatchAimPoints()
    {
        if (markerViews.Count != aimPointBuffer.Count)
        {
            return false;
        }

        for (int i = 0; i < markerViews.Count; i++)
        {
            if (markerViews[i].AimPoint != aimPointBuffer[i])
            {
                return false;
            }
        }

        return true;
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < markerViews.Count; i++)
        {
            if (markerViews[i].RectTransform != null)
            {
                Destroy(markerViews[i].RectTransform.gameObject);
            }
        }

        markerViews.Clear();
    }

    private void ValidateSelectedAimPoint()
    {
        if (selectedAimPoint != null && !ContainsAimPoint(selectedAimPoint))
        {
            selectedAimPoint = null;
        }

        if (criticalWindowAimPoint != null && !ContainsAimPoint(criticalWindowAimPoint))
        {
            criticalWindowAimPoint = null;
            criticalRetargetRemaining = 0f;
        }
    }

    private void UpdateCriticalWindow(float deltaTime)
    {
        if (aimPointBuffer.Count == 0)
        {
            criticalWindowAimPoint = null;
            criticalRetargetRemaining = criticalRetargetInterval;
            return;
        }

        criticalRetargetRemaining -= deltaTime;
        if (criticalRetargetRemaining > 0f)
        {
            return;
        }

        RetargetCriticalWindow();
        criticalRetargetRemaining = Mathf.Max(0.1f, criticalRetargetInterval);
    }

    private void RetargetCriticalWindow()
    {
        candidateBuffer.Clear();
        for (int i = 0; i < aimPointBuffer.Count; i++)
        {
            Transform aimPoint = aimPointBuffer[i];
            if (aimPoint != null && aimPoint != selectedAimPoint)
            {
                candidateBuffer.Add(aimPoint);
            }
        }

        if (candidateBuffer.Count == 0)
        {
            criticalWindowAimPoint = null;
            return;
        }

        if (candidateBuffer.Count > 1 && criticalWindowAimPoint != null)
        {
            candidateBuffer.Remove(criticalWindowAimPoint);
        }

        criticalWindowAimPoint = candidateBuffer[Random.Range(0, candidateBuffer.Count)];
    }

    private void UpdateMarkerViews()
    {
        if (targetingRoot == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            worldCamera = FindAnyObjectByType<Camera>();
        }

        if (worldCamera == null)
        {
            SetMarkersVisible(false);
            return;
        }

        Camera canvasCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        for (int i = 0; i < markerViews.Count; i++)
        {
            AimPointTargetMarkerView marker = markerViews[i];
            if (marker.AimPoint == null || marker.RectTransform == null)
            {
                continue;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(marker.AimPoint.position);
            bool visible = screenPosition.z > 0.01f;
            marker.RectTransform.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetingRoot,
                    screenPosition,
                    canvasCamera,
                    out Vector2 localPosition))
            {
                marker.RectTransform.anchoredPosition = localPosition + screenOffset;
            }

            marker.RectTransform.sizeDelta = markerSize;
            ApplyMarkerState(marker);
        }
    }

    private void ApplyMarkerState(AimPointTargetMarkerView marker)
    {
        AimPointTargetVisualState state = ResolveVisualState(marker.AimPoint);
        Sprite baseSprite = state == AimPointTargetVisualState.Selected ? alertBaseSprite : normalBaseSprite;
        Sprite innerSprite = state == AimPointTargetVisualState.Selected ? alertInnerSprite : normalInnerSprite;
        marker.ApplyState(state, baseSprite, innerSprite, ResolveBlinkAlpha(marker.AimPoint));
    }

    private void SetMarkersVisible(bool visible)
    {
        for (int i = 0; i < markerViews.Count; i++)
        {
            if (markerViews[i].RectTransform != null)
            {
                markerViews[i].RectTransform.gameObject.SetActive(visible);
            }
        }
    }

    private AimPointTargetVisualState ResolveVisualState(Transform aimPoint)
    {
        if (aimPoint != null && aimPoint == selectedAimPoint)
        {
            return AimPointTargetVisualState.Selected;
        }

        if (aimPoint != null && aimPoint == criticalWindowAimPoint)
        {
            return AimPointTargetVisualState.CriticalWindow;
        }

        return AimPointTargetVisualState.Normal;
    }

    private float ResolveBlinkAlpha(Transform aimPoint)
    {
        AimPointTargetVisualState state = ResolveVisualState(aimPoint);
        if (state == AimPointTargetVisualState.Normal)
        {
            return 1f;
        }

        float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * innerBlinkFrequency) * 0.5f + 0.5f;
        return Mathf.Lerp(innerBlinkMinAlpha, 1f, phase);
    }

    private bool ContainsAimPoint(Transform aimPoint)
    {
        if (aimPoint == null)
        {
            return false;
        }

        for (int i = 0; i < aimPointBuffer.Count; i++)
        {
            if (aimPointBuffer[i] == aimPoint)
            {
                return true;
            }
        }

        if (bossController == null)
        {
            return false;
        }

        int count = bossController.GetCombatAimPointCount();
        for (int i = 0; i < count; i++)
        {
            if (bossController.GetCombatAimPoint(i) == aimPoint)
            {
                return true;
            }
        }

        return false;
    }

    private enum AimPointTargetVisualState
    {
        Normal,
        CriticalWindow,
        Selected,
    }

    private readonly struct AimPointTargetMarkerView
    {
        public readonly Transform AimPoint;
        public readonly RectTransform RectTransform;
        private readonly Image baseImage;
        private readonly Image innerImage;

        public AimPointTargetMarkerView(Transform aimPoint, RectTransform rectTransform, Image baseImage, Image innerImage)
        {
            AimPoint = aimPoint;
            RectTransform = rectTransform;
            this.baseImage = baseImage;
            this.innerImage = innerImage;
        }

        public void ApplyState(AimPointTargetVisualState state, Sprite baseSprite, Sprite innerSprite, float innerAlpha)
        {
            if (baseImage != null)
            {
                baseImage.sprite = baseSprite;
                baseImage.color = Color.white;
            }

            if (innerImage != null)
            {
                innerImage.sprite = innerSprite;
                Color color = Color.white;
                color.a = Mathf.Clamp01(innerAlpha);
                innerImage.color = color;
            }
        }
    }
}

public class BattleAimPointTargetMarker : MonoBehaviour, IPointerClickHandler
{
    private static readonly List<RaycastResult> SharedRaycastResults = new();

    private BattleAimPointTargetingPresenter presenter;
    private Transform aimPoint;

    public void Configure(BattleAimPointTargetingPresenter owner, Transform targetAimPoint)
    {
        presenter = owner;
        aimPoint = targetAimPoint;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (presenter != null && aimPoint != null)
        {
            presenter.SelectAimPoint(aimPoint);
        }
    }

    public static bool IsPointerOverAnyMarker()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !TryGetPointerPosition(out Vector2 pointerPosition))
        {
            return false;
        }

        PointerEventData eventData = new(eventSystem)
        {
            position = pointerPosition
        };

        SharedRaycastResults.Clear();
        eventSystem.RaycastAll(eventData, SharedRaycastResults);
        for (int i = 0; i < SharedRaycastResults.Count; i++)
        {
            GameObject hitObject = SharedRaycastResults[i].gameObject;
            if (hitObject != null && hitObject.GetComponentInParent<BattleAimPointTargetMarker>() != null)
            {
                SharedRaycastResults.Clear();
                return true;
            }
        }

        SharedRaycastResults.Clear();
        return false;
    }

    private static bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointerPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            pointerPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        pointerPosition = default;
        return false;
    }
}
