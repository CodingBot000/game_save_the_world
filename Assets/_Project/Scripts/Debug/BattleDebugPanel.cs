using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

[DefaultExecutionOrder(1100)]
public class BattleDebugPanel : MonoBehaviour
{
    private const float TextScale = 1.5f;
    private const float PanelWidth = 430f;
    private const float ToggleWidth = 112f;
    private const float ToggleHeight = 48f;
    private const float PanelRightMargin = 8f;
    private const float PanelTopOffset = 224f;
    private const float PanelBottomMargin = 12f;
    private const float RowHeight = 45f;
    private const float CommandButtonHeight = 36f;
    private const int CommandButtonFontSize = 12;

    private readonly List<DebugRow> rows = new();

    private BattleDebugTuningApplier applier;
    private Canvas canvas;
    private RectTransform panelRect;
    private RectTransform contentRect;
    private Text toggleLabel;
    private Font runtimeFont;
    private bool expanded;
    private float refreshTimer;

    private IEnumerator Start()
    {
        applier = GetComponent<BattleDebugTuningApplier>();
        if (applier == null)
        {
            applier = gameObject.AddComponent<BattleDebugTuningApplier>();
        }

        yield return null;
        applier?.ResolveTargets();
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildUi();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        refreshTimer = 0.1f;
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Refresh();
        }
    }

    private void BuildUi()
    {
        rows.Clear();

        GameObject canvasObject = new("BattleDebugPanelCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CreatePanel(canvasObject.transform);
        CreateToggle(canvasObject.transform);
        SetExpanded(false);
    }

    private void CreateToggle(Transform parent)
    {
        Button toggleButton = CreateButton("BattleDebugToggle", parent, "Debug", new Color(0.12f, 0.17f, 0.22f, 0.86f));
        RectTransform toggleRect = toggleButton.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(1f, 1f);
        toggleRect.sizeDelta = new Vector2(ToggleWidth, ToggleHeight);
        toggleRect.anchoredPosition = new Vector2(-PanelRightMargin, -PanelTopOffset);
        toggleButton.onClick.AddListener(() => SetExpanded(!expanded));
        toggleLabel = toggleButton.GetComponentInChildren<Text>();
    }

    private void CreatePanel(Transform parent)
    {
        GameObject panelObject = new("BattleDebugPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.offsetMin = new Vector2(-PanelWidth - PanelRightMargin, PanelBottomMargin);
        panelRect.offsetMax = new Vector2(-PanelRightMargin, -PanelTopOffset);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.03f, 0.045f, 0.06f, 0.74f);

        VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(12, 12, 14, 14);
        panelLayout.spacing = 8f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        Text title = CreateText("Title", panelObject.transform, "Battle Tuning", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 42f;

        CreateCommandBar(panelObject.transform);
        CreateScroll(panelObject.transform);
        PopulateRows();
    }

    private void CreateCommandBar(Transform parent)
    {
        GameObject bar = new("CommandBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        LayoutElement layoutElement = bar.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 39f;

        Button applyButton = CreateCommandButton("ApplyButton", bar.transform, "Apply", new Color(0.14f, 0.32f, 0.48f, 0.92f));
        applyButton.onClick.AddListener(() => applier?.ApplyAllOverrides(refillPlayerDefense: true));

        Button refillButton = CreateCommandButton("RefillPlayerButton", bar.transform, "Refill", new Color(0.13f, 0.38f, 0.28f, 0.92f));
        refillButton.onClick.AddListener(() => applier?.RefillPlayerForDebug());

        Button healButton = CreateCommandButton("HealBossButton", bar.transform, "Boss HP", new Color(0.42f, 0.22f, 0.16f, 0.92f));
        healButton.onClick.AddListener(() => applier?.FullHealBossForDebug());

        Button clearButton = CreateCommandButton("ClearOverridesButton", bar.transform, "Clear", new Color(0.24f, 0.24f, 0.28f, 0.92f));
        clearButton.onClick.AddListener(() =>
        {
            BattleDebugTuningState.ClearOverrides();
            applier?.ApplyBasePlayerStats(refillDefense: true);
        });
    }

    private void CreateScroll(Transform parent)
    {
        GameObject scrollObject = new("Scroll", typeof(RectTransform));
        scrollObject.transform.SetParent(parent, false);
        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = new("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.1f);
        viewportObject.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4f;
        contentLayout.padding = new RectOffset(0, 4, 0, 0);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
    }

    private void PopulateRows()
    {
        AddHeader("Debug Toggles");
        AddBoolRow("Undead", BattleTuningKey.Undead, () => GameplayDebugFlags.Undead);
        AddBoolRow("Hurtbox Visual", BattleTuningKey.ShowDamageHurtbox, () => applier != null && applier.PlayerCombat != null && applier.PlayerCombat.DebugShowDamageHurtbox);
        AddBoolRow("Move Bounds", BattleTuningKey.ShowMovementBoundsGuide, () => applier != null && applier.PlayerMovementBounds != null && applier.PlayerMovementBounds.DebugShowRuntimeGuide);

        AddHeader("Player Attack");
        AddFloatRow("Fire Cooldown", BattleTuningKey.PlayerFireCooldown, 0.05f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugFireCooldown : 0f);
        AddFloatRow("Bullet Speed", BattleTuningKey.PlayerProjectileSpeed, 5f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugProjectileSpeed : 0f);
        AddFloatRow("Bullet Damage", BattleTuningKey.PlayerProjectileDamage, 5f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugProjectileDamage : 0f);
        AddFloatRow("Invulnerable", BattleTuningKey.PlayerInvulnerabilityDuration, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugInvulnerabilityDuration : 0f);
        AddFloatRow("Hit Radius", BattleTuningKey.PlayerHitRadius, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.HitRadius : 0f);

        AddHeader("Player Missile");
        AddFloatRow("Launch Speed", BattleTuningKey.PlayerMissileLaunchSpeed, 2f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileLaunchSpeed : 0f);
        AddFloatRow("Cruise Speed", BattleTuningKey.PlayerMissileCruiseSpeed, 5f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileCruiseSpeed : 0f);
        AddFloatRow("Acceleration", BattleTuningKey.PlayerMissileAcceleration, 10f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileAcceleration : 0f);
        AddFloatRow("Turn Rate", BattleTuningKey.PlayerMissileTurnRate, 10f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileTurnRate : 0f);
        AddFloatRow("Lock Delay", BattleTuningKey.PlayerMissileLockOnDelay, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileLockOnDelay : 0f);
        AddFloatRow("Straight Time", BattleTuningKey.PlayerMissileStraightPhaseDuration, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileStraightPhaseDuration : 0f);
        AddFloatRow("Straight Dist", BattleTuningKey.PlayerMissileStraightPhaseDistance, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileStraightPhaseDistance : 0f);
        AddFloatRow("Turn Time", BattleTuningKey.PlayerMissileTurnPhaseDuration, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileTurnPhaseDuration : 0f);
        AddFloatRow("Boost Time", BattleTuningKey.PlayerMissileBoostPhaseDuration, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileBoostPhaseDuration : 0f);
        AddFloatRow("Lifetime", BattleTuningKey.PlayerMissileLifetime, 0.5f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileLifetime : 0f);
        AddFloatRow("Hit Radius", BattleTuningKey.PlayerMissileHitRadius, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugMissileHitRadius : 0f);

        AddHeader("Player Defense");
        AddFloatRow("Max Hull", BattleTuningKey.PlayerMaxHull, 50f, () => applier.PlayerCombat != null ? applier.PlayerCombat.MaxHull : 0f);
        AddFloatRow("Max Armor", BattleTuningKey.PlayerMaxArmor, 50f, () => applier.PlayerCombat != null ? applier.PlayerCombat.MaxArmor : 0f);
        AddFloatRow("Repair Rate", BattleTuningKey.PlayerRepairRate, 1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugArmorRepairRate : 0f);
        AddFloatRow("Repair Delay", BattleTuningKey.PlayerRepairDelay, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugArmorRepairDelay : 0f);
        AddFloatRow("Recover Thres.", BattleTuningKey.PlayerBrokenRecoverThreshold, 5f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugBrokenRecoverThreshold : 0f);
        AddFloatRow("Broken Mult.", BattleTuningKey.PlayerHullDamageMultiplierWhenBroken, 0.1f, () => applier.PlayerCombat != null ? applier.PlayerCombat.DebugHullDamageMultiplierWhenBroken : 0f);

        AddHeader("Player Movement");
        AddFloatRow("Strafe Speed", BattleTuningKey.PlayerStrafeSpeed, 1f, () => applier.PlayerOrbit != null ? applier.PlayerOrbit.DebugStrafeSpeed : 0f);
        AddFloatRow("Altitude Speed", BattleTuningKey.PlayerAltitudeSpeed, 1f, () => applier.PlayerOrbit != null ? applier.PlayerOrbit.DebugAltitudeSpeed : 0f);
        AddFloatRow("Forward Speed", BattleTuningKey.PlayerForwardSpeed, 1f, () => applier.PlayerOrbit != null ? applier.PlayerOrbit.DebugForwardSpeed : 0f);
        AddFloatRow("Bounds X", BattleTuningKey.MovementBoundsX, 0.25f, () => applier.PlayerMovementBounds != null ? applier.PlayerMovementBounds.DebugHalfExtents.x : 0f);
        AddFloatRow("Bounds Y", BattleTuningKey.MovementBoundsY, 0.25f, () => applier.PlayerMovementBounds != null ? applier.PlayerMovementBounds.DebugHalfExtents.y : 0f);
        AddFloatRow("Bounds Z", BattleTuningKey.MovementBoundsZ, 0.25f, () => applier.PlayerMovementBounds != null ? applier.PlayerMovementBounds.DebugHalfExtents.z : 0f);

        AddHeader("Boss");
        AddFloatRow("Max HP", BattleTuningKey.BossMaxHealth, 100f, () => applier.Boss != null ? applier.Boss.MaxHealth : 0f);
        AddFloatRow("Current HP", BattleTuningKey.BossCurrentHealth, 100f, () => applier.Boss != null ? applier.Boss.CurrentHealth : 0f);
        AddFloatRow("Hit Radius", BattleTuningKey.BossHitRadius, 0.1f, () => applier.Boss != null ? applier.Boss.HitRadius : 0f);
        AddFloatRow("Bob Amp", BattleTuningKey.BossIdleBobAmplitude, 0.05f, () => applier.Boss != null ? applier.Boss.DebugIdleBobAmplitude : 0f);
        AddFloatRow("Bob Speed", BattleTuningKey.BossIdleBobSpeed, 0.1f, () => applier.Boss != null ? applier.Boss.DebugIdleBobSpeed : 0f);

        AddHeader("Boss Attack");
        AddFloatRow("Base Interval", BattleTuningKey.BossBaseAttackInterval, 0.1f, () => applier.BossAttack != null ? applier.BossAttack.DebugBaseAttackInterval : 0f);
        AddFloatRow("Enraged Int.", BattleTuningKey.BossEnragedAttackInterval, 0.1f, () => applier.BossAttack != null ? applier.BossAttack.DebugEnragedAttackInterval : 0f);
        AddFloatRow("Bullet Speed", BattleTuningKey.BossProjectileSpeed, 2f, () => applier.BossAttack != null ? applier.BossAttack.BaseProjectileSpeed : 0f);
        AddFloatRow("Bullet Damage", BattleTuningKey.BossProjectileDamage, 2f, () => applier.BossAttack != null ? applier.BossAttack.BaseProjectileDamage : 0f);
        AddFloatRow("Projectile Scale x", BattleTuningKey.BossProjectileScaleMultiplier, 0.25f, () => applier.BossAttack != null ? applier.BossAttack.DebugProjectileScaleMultiplier : 0f);

        AddHeader("Pattern Timing");
        AddFloatRow("Startup Delay", BattleTuningKey.BossPatternStartupDelay, 0.1f, () => applier.BossPatterns != null ? applier.BossPatterns.DebugStartupDelay : 0f);
        AddFloatRow("Aimed Interval", BattleTuningKey.BossPatternAimedBurstShotInterval, 0.05f, () => applier.BossPatterns != null ? applier.BossPatterns.DebugAimedBurstShotInterval : 0f);
        AddFloatRow("Warn Line", BattleTuningKey.BossPatternWarningLineThickness, 0.01f, () => applier.BossPatterns != null ? applier.BossPatterns.DebugWarningLineThickness : 0f);
        AddFloatRow("Attack Size x", BattleTuningKey.BossPatternAttackSizeMultiplier, 0.25f, () => applier.BossPatterns != null ? applier.BossPatterns.DebugAttackSizeMultiplier : 0f);
        AddFloatRow("Min Telegraph", BattleTuningKey.BossPatternMinimumTelegraphThickness, 0.05f, () => applier.BossPatterns != null ? applier.BossPatterns.DebugMinimumTelegraphThickness : 0f);

        AddPatternRows();
    }

    private void AddPatternRows()
    {
        if (applier == null || applier.BossPatterns == null)
        {
            return;
        }

        IReadOnlyList<BossBulletPatternDefinition> patterns = applier.BossPatterns.DebugPatternSequence;
        for (int i = 0; i < patterns.Count; i++)
        {
            int index = i;
            BossBulletPatternDefinition pattern = patterns[i];
            string displayName = string.IsNullOrWhiteSpace(pattern.displayName) ? $"Pattern {i + 1}" : pattern.displayName;
            AddHeader(displayName);
            AddPatternBoolRow("Enabled", index, BossPatternTuningKey.Enabled, () => pattern.enabled);
            AddPatternFloatRow("Min HP Ratio", index, BossPatternTuningKey.MinHealthRatio, 0.05f, () => pattern.minHealthRatio);
            AddPatternFloatRow("Max HP Ratio", index, BossPatternTuningKey.MaxHealthRatio, 0.05f, () => pattern.maxHealthRatio);
            AddPatternFloatRow("Cooldown x", index, BossPatternTuningKey.CooldownMultiplier, 0.1f, () => pattern.cooldownMultiplier);
            AddPatternIntRow("Projectile", index, BossPatternTuningKey.ProjectileCount, 1, () => pattern.projectileCount);
            AddPatternIntRow("Secondary", index, BossPatternTuningKey.SecondaryProjectileCount, 1, () => pattern.secondaryProjectileCount);
            AddPatternIntRow("Burst", index, BossPatternTuningKey.BurstCount, 1, () => pattern.burstCount);
            AddPatternFloatRow("Burst Interval", index, BossPatternTuningKey.BurstInterval, 0.05f, () => pattern.burstInterval);
            AddPatternFloatRow("Spread Angle", index, BossPatternTuningKey.SpreadAngle, 5f, () => pattern.spreadAngle);
            AddPatternFloatRow("Speed x", index, BossPatternTuningKey.SpeedMultiplier, 0.1f, () => pattern.speedMultiplier);
            AddPatternFloatRow("Secondary Speed x", index, BossPatternTuningKey.SecondarySpeedMultiplier, 0.1f, () => pattern.secondarySpeedMultiplier);
            AddPatternFloatRow("Damage x", index, BossPatternTuningKey.DamageMultiplier, 0.1f, () => pattern.damageMultiplier);
            AddPatternFloatRow("Secondary Damage x", index, BossPatternTuningKey.SecondaryDamageMultiplier, 0.1f, () => pattern.secondaryDamageMultiplier);
            AddPatternFloatRow("Ring Step", index, BossPatternTuningKey.RingRotationStep, 5f, () => pattern.ringRotationStep);
            AddPatternFloatRow("Telegraph", index, BossPatternTuningKey.TelegraphDuration, 0.1f, () => pattern.telegraphDuration);
            AddPatternFloatRow("Flashing", index, BossPatternTuningKey.FlashingDuration, 0.1f, () => pattern.flashingDuration);
            AddPatternFloatRow("Warn Width", index, BossPatternTuningKey.WarningWidth, 0.1f, () => pattern.warningWidth);
            AddPatternFloatRow("Warn Height", index, BossPatternTuningKey.WarningHeight, 0.5f, () => pattern.warningHeight);
            AddPatternFloatRow("Warn Depth", index, BossPatternTuningKey.WarningDepth, 0.1f, () => pattern.warningDepth);
            AddPatternFloatRow("Overhead", index, BossPatternTuningKey.OverheadHeight, 0.5f, () => pattern.overheadHeight);
            AddPatternFloatRow("Split Dist", index, BossPatternTuningKey.SplitDistance, 0.5f, () => pattern.splitDistance);
            AddPatternFloatRow("Projectile Scale", index, BossPatternTuningKey.ProjectileScale, 0.25f, () => pattern.projectileScale);
            AddPatternFloatRow("Active Time", index, BossPatternTuningKey.ActiveDuration, 0.1f, () => pattern.activeDuration);
            AddPatternFloatRow("Hazard Radius", index, BossPatternTuningKey.HazardRadius, 1f, () => pattern.hazardRadius);
            AddPatternFloatRow("Hazard Thick", index, BossPatternTuningKey.HazardThickness, 0.25f, () => pattern.hazardThickness);
            AddPatternFloatRow("Interrupt Dmg", index, BossPatternTuningKey.InterruptDamageThreshold, 10f, () => pattern.interruptDamageThreshold);
            AddPatternFloatRow("Safe Radius", index, BossPatternTuningKey.SafeRadius, 0.25f, () => pattern.safeRadius);
            AddPatternFloatRow("Min Spacing", index, BossPatternTuningKey.MinimumSpacing, 0.25f, () => pattern.minimumSpacing);
            AddPatternFloatRow("Warm Time", index, BossPatternTuningKey.FixedDuration, 0.1f, () => pattern.fixedDuration);
            AddPatternFloatRow("Slow Time", index, BossPatternTuningKey.SlowDuration, 0.1f, () => pattern.slowDuration);
            AddPatternFloatRow("Fast Time", index, BossPatternTuningKey.FastDuration, 0.1f, () => pattern.fastDuration);
            AddPatternFloatRow("Track Time", index, BossPatternTuningKey.TrackingDuration, 0.1f, () => pattern.trackingDuration);
            AddPatternFloatRow("Warm Track x", index, BossPatternTuningKey.BeamWarmupTrackingSpeedMultiplier, 0.05f, () => pattern.beamWarmupTrackingSpeedMultiplier);
            AddPatternFloatRow("Beam Track x", index, BossPatternTuningKey.BeamActiveTrackingSpeedMultiplier, 0.05f, () => pattern.beamActiveTrackingSpeedMultiplier);
            AddPatternFloatRow("Aim Jitter x", index, BossPatternTuningKey.AimJitterPlayerScale, 0.25f, () => pattern.aimJitterPlayerScale);
            AddPatternFloatRow("Start Scale x", index, BossPatternTuningKey.ApproachStartScale, 0.05f, () => pattern.approachStartScale);
            AddPatternFloatRow("End Scale x", index, BossPatternTuningKey.ApproachEndScale, 0.05f, () => pattern.approachEndScale);
            AddPatternFloatRow("Start Speed x", index, BossPatternTuningKey.ApproachInitialSpeedMultiplier, 0.05f, () => pattern.approachInitialSpeedMultiplier);
            AddPatternFloatRow("Flight Time", index, BossPatternTuningKey.ApproachFlightDuration, 0.05f, () => pattern.approachFlightDuration);
        }
    }

    private void AddHeader(string label)
    {
        Text text = CreateText($"Header_{label}", contentRect, label, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
        text.color = new Color(0.82f, 0.9f, 1f, 0.98f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;
    }

    private void AddFloatRow(string label, BattleTuningKey key, float step, Func<float> fallback)
    {
        Text valueText = CreateValueRow(label, out Button leftButton, out Button rightButton);
        leftButton.onClick.AddListener(() => BattleDebugTuningState.SetFloat(key, GetFloatValue(key, fallback) - step));
        rightButton.onClick.AddListener(() => BattleDebugTuningState.SetFloat(key, GetFloatValue(key, fallback) + step));
        rows.Add(new DebugRow(valueText, () => FormatFloat(GetFloatValue(key, fallback))));
    }

    private void AddBoolRow(string label, BattleTuningKey key, Func<bool> fallback)
    {
        Text valueText = CreateValueRow(label, out Button leftButton, out Button rightButton);
        leftButton.GetComponentInChildren<Text>().text = "<";
        rightButton.GetComponentInChildren<Text>().text = ">";
        leftButton.onClick.AddListener(() => BattleDebugTuningState.SetBool(key, !GetBoolValue(key, fallback)));
        rightButton.onClick.AddListener(() => BattleDebugTuningState.SetBool(key, !GetBoolValue(key, fallback)));
        rows.Add(new DebugRow(valueText, () => GetBoolValue(key, fallback) ? "ON" : "OFF"));
    }

    private void AddPatternFloatRow(string label, int patternIndex, BossPatternTuningKey key, float step, Func<float> fallback)
    {
        Text valueText = CreateValueRow(label, out Button leftButton, out Button rightButton);
        leftButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternFloat(patternIndex, key, GetPatternFloatValue(patternIndex, key, fallback) - step));
        rightButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternFloat(patternIndex, key, GetPatternFloatValue(patternIndex, key, fallback) + step));
        rows.Add(new DebugRow(valueText, () => FormatFloat(GetPatternFloatValue(patternIndex, key, fallback))));
    }

    private void AddPatternIntRow(string label, int patternIndex, BossPatternTuningKey key, int step, Func<int> fallback)
    {
        Text valueText = CreateValueRow(label, out Button leftButton, out Button rightButton);
        leftButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternInt(patternIndex, key, GetPatternIntValue(patternIndex, key, fallback) - step));
        rightButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternInt(patternIndex, key, GetPatternIntValue(patternIndex, key, fallback) + step));
        rows.Add(new DebugRow(valueText, () => GetPatternIntValue(patternIndex, key, fallback).ToString()));
    }

    private void AddPatternBoolRow(string label, int patternIndex, BossPatternTuningKey key, Func<bool> fallback)
    {
        Text valueText = CreateValueRow(label, out Button leftButton, out Button rightButton);
        leftButton.GetComponentInChildren<Text>().text = "<";
        rightButton.GetComponentInChildren<Text>().text = ">";
        leftButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternBool(patternIndex, key, !GetPatternBoolValue(patternIndex, key, fallback)));
        rightButton.onClick.AddListener(() => BattleDebugTuningState.SetPatternBool(patternIndex, key, !GetPatternBoolValue(patternIndex, key, fallback)));
        rows.Add(new DebugRow(valueText, () => GetPatternBoolValue(patternIndex, key, fallback) ? "ON" : "OFF"));
    }

    private Text CreateValueRow(string label, out Button leftButton, out Button rightButton)
    {
        GameObject row = new($"Row_{label}", typeof(RectTransform));
        row.transform.SetParent(contentRect, false);
        LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = RowHeight;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        Text labelText = CreateText("Label", row.transform, label, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        labelText.color = new Color(0.9f, 0.94f, 0.98f, 0.96f);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 175f;
        labelLayout.flexibleWidth = 1f;

        leftButton = CreateButton("Decrease", row.transform, "<", new Color(0.12f, 0.16f, 0.2f, 0.94f));
        LayoutElement leftLayout = leftButton.gameObject.AddComponent<LayoutElement>();
        leftLayout.preferredWidth = 44f;

        Text valueText = CreateText("Value", row.transform, "0", 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        valueText.color = Color.white;
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 92f;

        rightButton = CreateButton("Increase", row.transform, ">", new Color(0.12f, 0.16f, 0.2f, 0.94f));
        LayoutElement rightLayout = rightButton.gameObject.AddComponent<LayoutElement>();
        rightLayout.preferredWidth = 44f;
        return valueText;
    }

    private Button CreateCommandButton(string name, Transform parent, string label, Color color)
    {
        Button button = CreateButton(name, parent, label, CommandButtonFontSize, color);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = CalculateCommandButtonWidth(label);
        layout.preferredHeight = CommandButtonHeight;
        layout.minWidth = layout.preferredWidth;
        layout.minHeight = CommandButtonHeight;
        return button;
    }

    private Button CreateButton(string name, Transform parent, string label, Color color)
    {
        return CreateButton(name, parent, label, 13, color);
    }

    private Button CreateButton(string name, Transform parent, string label, int fontSize, Color color)
    {
        GameObject buttonObject = new(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static float CalculateCommandButtonWidth(string label)
    {
        return Mathf.Max(48f * TextScale, label.Length * 7.5f * TextScale + 18f * TextScale);
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = runtimeFont != null ? runtimeFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = ScaleFontSize(fontSize);
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static int ScaleFontSize(int fontSize)
    {
        return Mathf.Max(1, Mathf.RoundToInt(fontSize * TextScale));
    }

    private void SetExpanded(bool value)
    {
        expanded = value;
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(expanded);
        }

        if (toggleLabel != null)
        {
            toggleLabel.text = expanded ? "Close" : "Debug";
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static string FormatFloat(float value)
    {
        return value >= 100f ? value.ToString("0") : value.ToString("0.##");
    }

    private static float GetFloatValue(BattleTuningKey key, Func<float> fallback)
    {
        return BattleDebugTuningState.TryGetFloat(key, out float value) ? value : Mathf.Max(0f, fallback());
    }

    private static bool GetBoolValue(BattleTuningKey key, Func<bool> fallback)
    {
        return BattleDebugTuningState.TryGetBool(key, out bool value) ? value : fallback();
    }

    private static float GetPatternFloatValue(int patternIndex, BossPatternTuningKey key, Func<float> fallback)
    {
        return BattleDebugTuningState.TryGetPatternFloat(patternIndex, key, out float value) ? value : Mathf.Max(0f, fallback());
    }

    private static int GetPatternIntValue(int patternIndex, BossPatternTuningKey key, Func<int> fallback)
    {
        return BattleDebugTuningState.TryGetPatternInt(patternIndex, key, out int value) ? value : Mathf.Max(0, fallback());
    }

    private static bool GetPatternBoolValue(int patternIndex, BossPatternTuningKey key, Func<bool> fallback)
    {
        return BattleDebugTuningState.TryGetPatternBool(patternIndex, key, out bool value) ? value : fallback();
    }

    private sealed class DebugRow
    {
        private readonly Text valueText;
        private readonly Func<string> readValue;

        public DebugRow(Text valueText, Func<string> readValue)
        {
            this.valueText = valueText;
            this.readValue = readValue;
        }

        public void Refresh()
        {
            if (valueText != null && readValue != null)
            {
                valueText.text = readValue();
            }
        }
    }
}
