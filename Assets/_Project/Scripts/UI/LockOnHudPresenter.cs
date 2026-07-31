using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LockOnHudPresenter : MonoBehaviour
{
    private const string RuntimeRootName = "LockOnHudRuntime";
    private const string ChargeBackgroundName = "LockChargeBackground";
    private const string ChargeFillName = "LockChargeFill";
    private const float MarkerPulseDuration = 0.28f;
    private static readonly Color[] StageColors =
    {
        new(0.35f, 0.90f, 1f, 1f),
        new(0.25f, 1f, 0.55f, 1f),
        new(1f, 0.90f, 0.25f, 1f),
        new(1f, 0.52f, 0.16f, 1f),
        new(1f, 0.18f, 0.20f, 1f),
    };

    private PlayerLockOnController lockOnController;
    private Canvas battleCanvas;
    private Camera worldCamera;
    private Button lockButton;
    private Image lockButtonImage;
    private Text lockButtonLabel;
    private GameObject legacySpecialButton;
    private RectTransform runtimeRoot;
    private Image chargeFill;
    private readonly Text[] lockMarkers = new Text[5];
    private readonly float[] markerPulseStartedAt = { -1f, -1f, -1f, -1f, -1f };
    private bool ownsRuntimeRoot;
    private bool ownsChargeBackground;
    private bool configured;

    public int VisibleMarkerCount { get; private set; }
    public string ButtonLabelText => lockButtonLabel != null ? lockButtonLabel.text : string.Empty;
    public bool ButtonInteractable => lockButton != null && lockButton.interactable;
    public bool LegacySpecialHidden => legacySpecialButton == null || !legacySpecialButton.activeSelf;
    public float ChargeFillAmount => chargeFill != null ? chargeFill.fillAmount : 0f;
    public int ActiveMarkerPulseCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < markerPulseStartedAt.Length; i++)
            {
                float elapsed = Time.unscaledTime - markerPulseStartedAt[i];
                if (markerPulseStartedAt[i] >= 0f && elapsed < MarkerPulseDuration)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void Configure(
        PlayerLockOnController controller,
        Canvas canvas,
        Button button,
        Image buttonImage,
        Text buttonLabel,
        GameObject specialButton)
    {
        UnsubscribeController();
        lockOnController = controller;
        battleCanvas = canvas;
        worldCamera = Camera.main;
        lockButton = button;
        lockButtonImage = buttonImage;
        lockButtonLabel = buttonLabel;
        legacySpecialButton = specialButton;

        if (lockButton != null)
        {
            lockButton.onClick.RemoveAllListeners();
            LockOnButtonInputRelay relay =
                lockButton.GetComponent<LockOnButtonInputRelay>() ??
                lockButton.gameObject.AddComponent<LockOnButtonInputRelay>();
            relay.Configure(lockOnController);
        }

        if (legacySpecialButton != null)
        {
            legacySpecialButton.SetActive(false);
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
        VisibleMarkerCount = 0;
        for (int i = 0; i < lockMarkers.Length; i++)
        {
            Text marker = lockMarkers[i];
            if (marker == null)
            {
                continue;
            }

            bool hasSlot = lockOnController != null &&
                           lockOnController.State == LockOnCombatState.Charging &&
                           i < lockOnController.LockedTargets.Count;
            BossLockOnTarget target = hasSlot
                ? lockOnController.LockedTargets[i]
                : null;
            Vector2 canvasPosition = Vector2.zero;
            bool show = target != null && target.IsSelectable &&
                        TryGetCanvasPosition(target.WorldPosition, out canvasPosition);
            marker.gameObject.SetActive(show);
            if (!show)
            {
                continue;
            }

            marker.rectTransform.anchoredPosition = canvasPosition;
            marker.text = $"◇{i + 1}";
            marker.color = StageColors[Mathf.Clamp(i, 0, StageColors.Length - 1)];
            float baseScale = 1f + i * 0.08f;
            float pulse = ResolveMarkerPulse(i);
            float scale = baseScale * (1f + pulse * 0.42f);
            marker.rectTransform.localScale = new Vector3(scale, scale, 1f);
            VisibleMarkerCount++;
        }
    }

    private float ResolveMarkerPulse(int markerIndex)
    {
        float startedAt = markerPulseStartedAt[markerIndex];
        if (startedAt < 0f)
        {
            return 0f;
        }

        float progress = (Time.unscaledTime - startedAt) / MarkerPulseDuration;
        if (progress >= 1f)
        {
            markerPulseStartedAt[markerIndex] = -1f;
            return 0f;
        }

        return Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
    }

    private void HandleLockStageUp(int successfulLockCount)
    {
        int markerIndex = successfulLockCount - 1;
        if (markerIndex >= 0 && markerIndex < markerPulseStartedAt.Length)
        {
            markerPulseStartedAt[markerIndex] = Time.unscaledTime;
        }
    }

    private void SubscribeController()
    {
        if (lockOnController == null)
        {
            return;
        }

        lockOnController.OnLockStageUp -= HandleLockStageUp;
        lockOnController.OnLockStageUp += HandleLockStageUp;
    }

    private void UnsubscribeController()
    {
        if (lockOnController != null)
        {
            lockOnController.OnLockStageUp -= HandleLockStageUp;
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
        for (int i = 0; i < lockMarkers.Length; i++)
        {
            string markerName = $"LockOnMarker_{i + 1}";
            Transform existingMarker = runtimeRoot.Find(markerName);
            Text marker = existingMarker != null
                ? existingMarker.GetComponent<Text>()
                : null;
            if (marker == null)
            {
                GameObject markerObject = new(markerName, typeof(RectTransform));
                markerObject.transform.SetParent(runtimeRoot, false);
                marker = markerObject.AddComponent<Text>();
            }

            marker.font = font;
            marker.fontSize = 34;
            marker.fontStyle = FontStyle.Bold;
            marker.alignment = TextAnchor.MiddleCenter;
            marker.raycastTarget = false;
            marker.horizontalOverflow = HorizontalWrapMode.Overflow;
            marker.verticalOverflow = VerticalWrapMode.Overflow;
            marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(90f, 54f);
            Outline outline = marker.GetComponent<Outline>() ?? marker.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            marker.gameObject.SetActive(false);
            lockMarkers[i] = marker;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < lockMarkers.Length; i++)
        {
            if (lockMarkers[i] != null)
            {
                lockMarkers[i].gameObject.SetActive(false);
            }
        }
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
