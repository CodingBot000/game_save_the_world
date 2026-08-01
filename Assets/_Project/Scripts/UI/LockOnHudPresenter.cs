using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LockOnHudPresenter : MonoBehaviour
{
    private const string RuntimeRootName = "LockOnHudRuntime";
    private const string ChargeBackgroundName = "LockChargeBackground";
    private const string ChargeFillName = "LockChargeFill";
    private const float ChargingMarkerPulseHalfCycle = 0.28f;
    private const float ChargingMarkerMinimumAlpha = 0.32f;
    private const float ChargingMarkerScaleAmplitude = 0.42f;
    private static readonly Color[] StageColors =
    {
        new(0.35f, 0.90f, 1f, 1f),
        new(0.25f, 1f, 0.55f, 1f),
        new(1f, 0.90f, 0.25f, 1f),
        new(1f, 0.52f, 0.16f, 1f),
        new(1f, 0.18f, 0.20f, 1f),
    };

    [Header("Lock Marker Art")]
    [SerializeField] private Sprite lockMarkerBaseSprite;
    [SerializeField] private Sprite lockMarkerInnerSprite;
    [SerializeField] private Vector2 lockMarkerSize = new(96f, 94f);
    [SerializeField, Min(0f)] private float releaseMarkerHoldDuration = 1f;

    private PlayerLockOnController lockOnController;
    private Canvas battleCanvas;
    private Camera worldCamera;
    private Button lockButton;
    private Image lockButtonImage;
    private Text lockButtonLabel;
    private RectTransform runtimeRoot;
    private Image chargeFill;
    private readonly RectTransform[] lockMarkerRoots = new RectTransform[5];
    private readonly Image[] lockMarkerBaseImages = new Image[5];
    private readonly Image[] lockMarkerInnerImages = new Image[5];
    private readonly Text[] lockMarkerLabels = new Text[5];
    private readonly Transform[] releasedTargetAnchors = new Transform[5];
    private readonly Vector3[] releasedTargetPositions = new Vector3[5];
    private bool ownsRuntimeRoot;
    private bool ownsChargeBackground;
    private bool configured;
    private bool releaseMarkersActive;
    private int releasedMarkerCount;
    private int releasedSalvoId;
    private float releaseMarkersClearAt = -1f;
    private BossLockOnTarget lastChargingMarkerTarget;
    private int lastChargingMarkerIndex = -1;
    private float chargingMarkerPulseStartedAt = -1f;

    public int VisibleMarkerCount { get; private set; }
    public int VisibleTargetingImageCount { get; private set; }
    public bool ReleaseMarkersActive => releaseMarkersActive;
    public float ReleaseMarkerHoldDuration => releaseMarkerHoldDuration;
    public float ReleaseMarkerClearRemaining => releaseMarkersClearAt < 0f
        ? -1f
        : Mathf.Max(0f, releaseMarkersClearAt - Time.unscaledTime);
    public string ButtonLabelText => lockButtonLabel != null ? lockButtonLabel.text : string.Empty;
    public bool ButtonInteractable => lockButton != null && lockButton.interactable;
    public float ChargeFillAmount => chargeFill != null ? chargeFill.fillAmount : 0f;
    public int ActiveMarkerPulseCount { get; private set; }
    public int BlinkingMarkerCount => ActiveMarkerPulseCount;

    public void Configure(
        PlayerLockOnController controller,
        Canvas canvas,
        Button button,
        Image buttonImage,
        Text buttonLabel)
    {
        UnsubscribeController();
        ClearReleasedMarkers();
        lockOnController = controller;
        battleCanvas = canvas;
        worldCamera = Camera.main;
        lockButton = button;
        lockButtonImage = buttonImage;
        lockButtonLabel = buttonLabel;

        if (lockButton != null)
        {
            lockButton.onClick.RemoveAllListeners();
            LockOnButtonInputRelay relay =
                lockButton.GetComponent<LockOnButtonInputRelay>() ??
                lockButton.gameObject.AddComponent<LockOnButtonInputRelay>();
            relay.Configure(lockOnController);
        }

        EnsureChargeGauge();
        EnsureWorldMarkers();
        SubscribeController();
        configured = true;
        RefreshAll();
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshButtonAndGauge();
        RefreshMarkers();
    }

    public void RefreshForDebug()
    {
        if (configured)
        {
            RefreshAll();
        }
    }

    private void RefreshButtonAndGauge()
    {
        if (lockOnController == null || lockButton == null ||
            lockButtonImage == null || lockButtonLabel == null)
        {
            return;
        }

        bool charging = lockOnController.State == LockOnCombatState.Charging;
        bool ready = lockOnController.IsLockInputAvailable;
        bool reuseWait = lockOnController.State == LockOnCombatState.ReuseWait;
        lockButton.interactable = (charging && lockOnController.HasValidTargets) || ready;

        if (reuseWait)
        {
            lockButtonImage.color = new Color(0.28f, 0.30f, 0.34f, 0.96f);
            lockButtonLabel.text = $"LOCK\n{lockOnController.ReuseWaitRemaining:0.0}s";
        }
        else if (charging)
        {
            int stageIndex = Mathf.Clamp(lockOnController.SuccessfulLockCount - 1, 0, StageColors.Length - 1);
            lockButtonImage.color = lockOnController.SuccessfulLockCount > 0
                ? StageColors[stageIndex]
                : new Color(0.18f, 0.58f, 0.78f, 0.96f);
            lockButtonLabel.text = lockOnController.SuccessfulLockCount > 0
                ? $"RELEASE\n{lockOnController.SuccessfulLockCount} / 5"
                : "LOCKING\nHOLD";
        }
        else if (ready)
        {
            lockButtonImage.color = new Color(0.12f, 0.66f, 0.88f, 0.96f);
            lockButtonLabel.text = "LOCK ON\nHOLD";
        }
        else
        {
            lockButtonImage.color = new Color(0.25f, 0.27f, 0.31f, 0.94f);
            lockButtonLabel.text = "LOCK ON\nNO TARGET";
        }

        if (chargeFill != null)
        {
            chargeFill.fillAmount = charging ? lockOnController.NextStageProgress : 0f;
            int stageIndex = Mathf.Clamp(lockOnController.ChargeStage, 0, StageColors.Length - 1);
            chargeFill.color = StageColors[stageIndex];
        }
    }

    private void RefreshMarkers()
    {
        ExpireReleasedMarkersIfNeeded();
        RefreshChargingMarkerPulseState();
        VisibleMarkerCount = 0;
        VisibleTargetingImageCount = 0;
        ActiveMarkerPulseCount = 0;
        for (int i = 0; i < lockMarkerRoots.Length; i++)
        {
            RectTransform markerRoot = lockMarkerRoots[i];
            if (markerRoot == null)
            {
                continue;
            }

            bool hasSlot;
            bool selectable = true;
            bool isChargingMarker = false;
            Vector3 worldPosition = Vector3.zero;
            if (releaseMarkersActive)
            {
                hasSlot = i < releasedMarkerCount;
                if (hasSlot)
                {
                    Transform anchor = releasedTargetAnchors[i];
                    worldPosition = anchor != null ? anchor.position : releasedTargetPositions[i];
                }
            }
            else
            {
                bool charging = lockOnController != null &&
                                lockOnController.State == LockOnCombatState.Charging;
                BossLockOnTarget target = charging && i < lockOnController.LockedTargets.Count
                    ? lockOnController.LockedTargets[i]
                    : null;
                if (target == null && charging &&
                    i == lockOnController.LockedTargets.Count &&
                    lockOnController.CurrentChargingTarget != null)
                {
                    target = lockOnController.CurrentChargingTarget;
                    isChargingMarker = true;
                }

                hasSlot = target != null;
                selectable = target != null && target.IsSelectable;
                if (target != null)
                {
                    worldPosition = target.WorldPosition;
                }
            }

            Vector2 canvasPosition = Vector2.zero;
            bool show = hasSlot && selectable &&
                        TryGetCanvasPosition(worldPosition, out canvasPosition);
            markerRoot.gameObject.SetActive(show);
            if (!show)
            {
                continue;
            }

            Color stageColor = StageColors[Mathf.Clamp(i, 0, StageColors.Length - 1)];
            float pulse = isChargingMarker ? ResolveChargingMarkerPulse() : 0f;
            if (isChargingMarker)
            {
                stageColor.a *= Mathf.Lerp(
                    ChargingMarkerMinimumAlpha,
                    1f,
                    pulse);
                ActiveMarkerPulseCount++;
            }

            markerRoot.anchoredPosition = canvasPosition;
            if (lockMarkerBaseImages[i] != null)
            {
                lockMarkerBaseImages[i].color = stageColor;
                if (lockMarkerBaseImages[i].sprite != null)
                {
                    VisibleTargetingImageCount++;
                }
            }

            if (lockMarkerInnerImages[i] != null)
            {
                lockMarkerInnerImages[i].color = stageColor;
            }

            Text label = lockMarkerLabels[i];
            if (label != null)
            {
                label.text = (i + 1).ToString();
                label.color = stageColor;
            }

            float baseScale = 1f + i * 0.08f;
            float scale = baseScale *
                          (1f + pulse * ChargingMarkerScaleAmplitude);
            markerRoot.localScale = new Vector3(scale, scale, 1f);
            VisibleMarkerCount++;
        }
    }

    private void ExpireReleasedMarkersIfNeeded()
    {
        if (releaseMarkersActive && releaseMarkersClearAt >= 0f &&
            Time.unscaledTime >= releaseMarkersClearAt)
        {
            ClearReleasedMarkers();
        }
    }

    private void RefreshChargingMarkerPulseState()
    {
        BossLockOnTarget chargingTarget = !releaseMarkersActive &&
                                          lockOnController != null &&
                                          lockOnController.State == LockOnCombatState.Charging
            ? lockOnController.CurrentChargingTarget
            : null;
        int chargingIndex = chargingTarget != null
            ? lockOnController.LockedTargets.Count
            : -1;
        if (chargingTarget == lastChargingMarkerTarget &&
            chargingIndex == lastChargingMarkerIndex)
        {
            return;
        }

        lastChargingMarkerTarget = chargingTarget;
        lastChargingMarkerIndex = chargingIndex;
        chargingMarkerPulseStartedAt = chargingTarget != null
            ? Time.unscaledTime
            : -1f;
    }

    private float ResolveChargingMarkerPulse()
    {
        if (chargingMarkerPulseStartedAt < 0f || ChargingMarkerPulseHalfCycle <= 0f)
        {
            return 0f;
        }

        float elapsed = Mathf.Max(0f, Time.unscaledTime - chargingMarkerPulseStartedAt);
        return Mathf.PingPong(elapsed / ChargingMarkerPulseHalfCycle, 1f);
    }

    private void HandleLockStarted(LockOnInputSource inputSource)
    {
        ClearReleasedMarkers();
        ResetChargingMarkerPulseState();
    }

    private void HandleLockCanceled(LockOnCancelReason reason)
    {
        if (!releaseMarkersActive)
        {
            HideAllMarkers();
        }
    }

    private void HandleLockReleased(LockOnReleaseIntent intent)
    {
        ClearReleasedMarkers();
        if (intent == null)
        {
            return;
        }

        releasedMarkerCount = Mathf.Min(
            lockMarkerRoots.Length,
            intent.TargetSnapshots.Count);
        for (int i = 0; i < releasedMarkerCount; i++)
        {
            SalvoTargetSnapshot snapshot = intent.TargetSnapshots[i];
            releasedTargetAnchors[i] = snapshot?.Target;
            releasedTargetPositions[i] = snapshot != null
                ? snapshot.TargetWorldPosition
                : Vector3.zero;
        }

        releaseMarkersActive = releasedMarkerCount > 0;
        releasedSalvoId = lockOnController != null
            ? lockOnController.CurrentLockOnSalvoId
            : 0;
        releaseMarkersClearAt = -1f;
        RefreshMarkers();
    }

    private void HandleLockOnSalvoFinished(int salvoId, bool canceled)
    {
        if (!releaseMarkersActive || releasedSalvoId != salvoId)
        {
            return;
        }

        releaseMarkersClearAt = Time.unscaledTime + releaseMarkerHoldDuration;
    }

    private void ClearReleasedMarkers()
    {
        releaseMarkersActive = false;
        releasedMarkerCount = 0;
        releasedSalvoId = 0;
        releaseMarkersClearAt = -1f;
        for (int i = 0; i < releasedTargetAnchors.Length; i++)
        {
            releasedTargetAnchors[i] = null;
            releasedTargetPositions[i] = Vector3.zero;
        }
    }

    private void SubscribeController()
    {
        if (lockOnController == null)
        {
            return;
        }

        lockOnController.OnLockStart -= HandleLockStarted;
        lockOnController.OnLockStart += HandleLockStarted;
        lockOnController.OnLockCanceled -= HandleLockCanceled;
        lockOnController.OnLockCanceled += HandleLockCanceled;
        lockOnController.OnLockRelease -= HandleLockReleased;
        lockOnController.OnLockRelease += HandleLockReleased;
        lockOnController.OnLockOnSalvoFinished -= HandleLockOnSalvoFinished;
        lockOnController.OnLockOnSalvoFinished += HandleLockOnSalvoFinished;
    }

    private void UnsubscribeController()
    {
        if (lockOnController != null)
        {
            lockOnController.OnLockStart -= HandleLockStarted;
            lockOnController.OnLockCanceled -= HandleLockCanceled;
            lockOnController.OnLockRelease -= HandleLockReleased;
            lockOnController.OnLockOnSalvoFinished -= HandleLockOnSalvoFinished;
        }
    }

    private bool TryGetCanvasPosition(Vector3 worldPosition, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        if (battleCanvas == null || worldCamera == null)
        {
            return false;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            return false;
        }

        RectTransform canvasRect = battleCanvas.transform as RectTransform;
        Camera eventCamera = battleCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : battleCanvas.worldCamera;
        return canvasRect != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   canvasRect,
                   screenPosition,
                   eventCamera,
                   out canvasPosition);
    }

    private void EnsureChargeGauge()
    {
        if (lockButton == null)
        {
            return;
        }

        Transform existingBackground = lockButton.transform.Find(ChargeBackgroundName);
        Image background = existingBackground != null
            ? existingBackground.GetComponent<Image>()
            : null;
        if (background == null)
        {
            GameObject backgroundObject = new(ChargeBackgroundName, typeof(RectTransform));
            backgroundObject.transform.SetParent(lockButton.transform, false);
            background = backgroundObject.AddComponent<Image>();
            ownsChargeBackground = true;
        }

        background.color = new Color(0.02f, 0.04f, 0.08f, 0.90f);
        background.raycastTarget = false;
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0.08f, 0.08f);
        backgroundRect.anchorMax = new Vector2(0.92f, 0.20f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Transform existingFill = background.transform.Find(ChargeFillName);
        chargeFill = existingFill != null ? existingFill.GetComponent<Image>() : null;
        if (chargeFill == null)
        {
            GameObject fillObject = new(ChargeFillName, typeof(RectTransform));
            fillObject.transform.SetParent(background.transform, false);
            chargeFill = fillObject.AddComponent<Image>();
        }

        chargeFill.color = StageColors[0];
        chargeFill.raycastTarget = false;
        chargeFill.type = Image.Type.Filled;
        chargeFill.fillMethod = Image.FillMethod.Horizontal;
        chargeFill.fillOrigin = 0;
        chargeFill.fillAmount = 0f;
        RectTransform fillRect = chargeFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        background.transform.SetAsFirstSibling();
        if (lockButtonLabel != null)
        {
            lockButtonLabel.transform.SetAsLastSibling();
        }
    }

    private void EnsureWorldMarkers()
    {
        if (battleCanvas == null)
        {
            return;
        }

        Transform generatedHud = battleCanvas.transform.Find("GeneratedHUD");
        Transform parent = generatedHud ?? battleCanvas.transform;
        Transform existingRoot = parent.Find(RuntimeRootName);
        if (existingRoot == null)
        {
            GameObject rootObject = new(RuntimeRootName, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            runtimeRoot = rootObject.GetComponent<RectTransform>();
            ownsRuntimeRoot = true;
        }
        else
        {
            runtimeRoot = existingRoot as RectTransform;
        }

        if (runtimeRoot == null)
        {
            return;
        }

        runtimeRoot.anchorMin = Vector2.zero;
        runtimeRoot.anchorMax = Vector2.one;
        runtimeRoot.offsetMin = Vector2.zero;
        runtimeRoot.offsetMax = Vector2.zero;
        runtimeRoot.SetAsLastSibling();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < lockMarkerRoots.Length; i++)
        {
            string markerName = $"LockOnMarker_{i + 1}";
            Transform existingMarker = runtimeRoot.Find(markerName);
            RectTransform markerRoot = existingMarker as RectTransform;
            if (markerRoot == null)
            {
                GameObject markerObject = new(markerName, typeof(RectTransform));
                markerObject.transform.SetParent(runtimeRoot, false);
                markerRoot = markerObject.GetComponent<RectTransform>();
            }

            Text legacyText = markerRoot.GetComponent<Text>();
            if (legacyText != null)
            {
                legacyText.enabled = false;
            }

            markerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            markerRoot.pivot = new Vector2(0.5f, 0.5f);
            markerRoot.sizeDelta = lockMarkerSize;

            Image baseImage = EnsureMarkerImage(markerRoot, "TargetingBase", lockMarkerBaseSprite);
            Image innerImage = EnsureMarkerImage(markerRoot, "TargetingInner", lockMarkerInnerSprite);
            Text label = EnsureMarkerLabel(markerRoot, font);
            Outline outline = label.GetComponent<Outline>() ?? label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            markerRoot.gameObject.SetActive(false);
            lockMarkerRoots[i] = markerRoot;
            lockMarkerBaseImages[i] = baseImage;
            lockMarkerInnerImages[i] = innerImage;
            lockMarkerLabels[i] = label;
        }
    }

    private static Image EnsureMarkerImage(
        RectTransform markerRoot,
        string childName,
        Sprite sprite)
    {
        Transform existing = markerRoot.Find(childName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = new(childName, typeof(RectTransform));
            imageObject.transform.SetParent(markerRoot, false);
            image = imageObject.AddComponent<Image>();
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Text EnsureMarkerLabel(RectTransform markerRoot, Font font)
    {
        const string labelName = "MarkerLabel";
        Transform existing = markerRoot.Find(labelName);
        Text label = existing != null ? existing.GetComponent<Text>() : null;
        if (label == null)
        {
            GameObject labelObject = new(labelName, typeof(RectTransform));
            labelObject.transform.SetParent(markerRoot, false);
            label = labelObject.AddComponent<Text>();
        }

        label.font = font;
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(40f, 40f);
        label.transform.SetAsLastSibling();
        return label;
    }

    private void HideAllMarkers()
    {
        VisibleMarkerCount = 0;
        VisibleTargetingImageCount = 0;
        ActiveMarkerPulseCount = 0;
        for (int i = 0; i < lockMarkerRoots.Length; i++)
        {
            if (lockMarkerRoots[i] != null)
            {
                lockMarkerRoots[i].gameObject.SetActive(false);
            }
        }
    }

    private void ResetChargingMarkerPulseState()
    {
        lastChargingMarkerTarget = null;
        lastChargingMarkerIndex = -1;
        chargingMarkerPulseStartedAt = -1f;
    }

    private void OnDisable()
    {
        HideAllMarkers();
    }

    private void OnDestroy()
    {
        UnsubscribeController();
        if (ownsRuntimeRoot && runtimeRoot != null)
        {
            Destroy(runtimeRoot.gameObject);
        }

        if (ownsChargeBackground && chargeFill != null)
        {
            Destroy(chargeFill.transform.parent.gameObject);
        }
    }
}
