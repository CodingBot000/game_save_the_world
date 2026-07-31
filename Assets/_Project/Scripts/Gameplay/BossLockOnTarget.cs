using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossLockOnTarget : MonoBehaviour
{
    [SerializeField] private string targetId = "boss.target";
    [SerializeField] private Transform anchorTransform;
    [SerializeField] private string anchorLookupName;
    [SerializeField] private bool requireAssignedAnchor;
    [SerializeField, Min(1f)] private float priority = 50f;
    [SerializeField] private bool isWeakPoint;
    [SerializeField] private bool isAttackable = true;
    [SerializeField] private bool isLargePart = true;
    [SerializeField] private bool isPreparingStrongAttack;
    [SerializeField, Min(1)] private int minimumPhase = 1;
    [SerializeField, Min(0)] private int maximumPhase;

    private BossTestState bossTestState;
    private float lastLockedTime = float.NegativeInfinity;
    private float lastRecentlyAttackedTime = float.NegativeInfinity;
    private int stageAssigned;
    private bool isVisible;
    private BossLockOnTargetProvider registeredProvider;

    public string TargetId => string.IsNullOrWhiteSpace(targetId) ? gameObject.name : targetId;
    public Transform AnchorTransform => requireAssignedAnchor
        ? anchorTransform
        : anchorTransform != null ? anchorTransform : transform;
    public Vector3 WorldPosition => AnchorTransform != null ? AnchorTransform.position : transform.position;
    public float Priority => priority;
    public bool IsWeakPoint => isWeakPoint;
    public bool IsWeakPointOpen => isWeakPoint && bossTestState != null && bossTestState.IsWeakPointOpen;
    public bool IsAttackable => isAttackable;
    public bool IsLargePart => isLargePart;
    public bool IsPreparingStrongAttack => isPreparingStrongAttack;
    public bool WasRecentlyAttacked => GetRecentAttackAge() < 2f;
    public bool IsVisible => isVisible;
    public float LastLockedTime => lastLockedTime;
    public int StageAssigned => stageAssigned;
    internal string AnchorLookupName => anchorLookupName;
    internal bool NeedsAnchorRebind =>
        requireAssignedAnchor && anchorTransform == null &&
        !string.IsNullOrWhiteSpace(anchorLookupName);
    public bool IsAvailableInCurrentPhase =>
        bossTestState == null ||
        (bossTestState.CurrentPhase >= minimumPhase &&
         (maximumPhase <= 0 || bossTestState.CurrentPhase <= maximumPhase));
    public bool IsSelectable =>
        isActiveAndEnabled &&
        gameObject.activeInHierarchy &&
        AnchorTransform != null &&
        AnchorTransform.gameObject.activeInHierarchy &&
        isAttackable &&
        IsAvailableInCurrentPhase &&
        (!isWeakPoint || IsWeakPointOpen);

    public event Action<BossLockOnTarget> AvailabilityChanged;

    internal void BindState(BossTestState state)
    {
        bossTestState = state;
    }

    internal void ConfigurePrototype(
        string id,
        float basePriority,
        bool weakPoint,
        bool largePart,
        Transform trackingAnchor,
        string trackingAnchorName)
    {
        targetId = id;
        anchorTransform = trackingAnchor;
        anchorLookupName = trackingAnchorName;
        requireAssignedAnchor = trackingAnchor != null;
        priority = Mathf.Max(1f, basePriority);
        isWeakPoint = weakPoint;
        isAttackable = true;
        isLargePart = largePart;
        isPreparingStrongAttack = false;
        minimumPhase = 1;
        maximumPhase = 0;
    }

    internal void RebindAnchor(Transform trackingAnchor)
    {
        if (trackingAnchor == null)
        {
            return;
        }

        anchorTransform = trackingAnchor;
        requireAssignedAnchor = true;
        AvailabilityChanged?.Invoke(this);
    }

    public void SetAttackableForDebug(bool attackable)
    {
        if (isAttackable == attackable)
        {
            return;
        }

        isAttackable = attackable;
        AvailabilityChanged?.Invoke(this);
    }

    public void SetPreparingStrongAttack(bool preparing)
    {
        if (isPreparingStrongAttack == preparing)
        {
            return;
        }

        isPreparingStrongAttack = preparing;
        AvailabilityChanged?.Invoke(this);
    }

    public void MarkRecentlyAttacked()
    {
        lastRecentlyAttackedTime = Time.time;
    }

    public void MarkLocked(int assignedStage)
    {
        lastLockedTime = Time.time;
        stageAssigned = Mathf.Max(0, assignedStage);
    }

    internal float EvaluateSelectionWeight(Camera worldCamera, float viewportMargin)
    {
        float weight = Mathf.Max(1f, priority);
        isVisible = EvaluateVisibility(worldCamera, viewportMargin, out float centerCloseness);

        if (IsWeakPointOpen)
        {
            weight += 1000f;
        }

        if (isPreparingStrongAttack)
        {
            weight += 600f;
        }

        float recentAttackAge = GetRecentAttackAge();
        if (recentAttackAge >= 0f && recentAttackAge < 2f)
        {
            weight += 300f * (1f - recentAttackAge / 2f);
        }

        if (isVisible)
        {
            weight += 200f + centerCloseness * 150f;
        }

        if (isLargePart)
        {
            float distance = worldCamera != null
                ? Vector3.Distance(worldCamera.transform.position, WorldPosition)
                : 0f;
            float proximityBonus = worldCamera != null
                ? 100f / (1f + distance * 0.05f)
                : 100f;
            weight += 50f + proximityBonus;
        }

        return weight;
    }

    private float GetRecentAttackAge()
    {
        return Time.time - lastRecentlyAttackedTime;
    }

    private bool EvaluateVisibility(
        Camera worldCamera,
        float viewportMargin,
        out float centerCloseness)
    {
        if (worldCamera == null)
        {
            centerCloseness = 1f;
            return true;
        }

        Vector3 viewport = worldCamera.WorldToViewportPoint(WorldPosition);
        float margin = Mathf.Max(0f, viewportMargin);
        bool inside = viewport.z > 0f &&
                      viewport.x >= -margin && viewport.x <= 1f + margin &&
                      viewport.y >= -margin && viewport.y <= 1f + margin;
        Vector2 centerDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        centerCloseness = inside
            ? 1f - Mathf.Clamp01(centerDelta.magnitude / 0.7072f)
            : 0f;
        return inside;
    }

    private void OnEnable()
    {
        RegisterWithProvider();
        AvailabilityChanged?.Invoke(this);
    }

    private void OnDisable()
    {
        AvailabilityChanged?.Invoke(this);
    }

    private void OnTransformParentChanged()
    {
        if (isActiveAndEnabled)
        {
            RegisterWithProvider();
        }
    }

    private void OnDestroy()
    {
        if (registeredProvider != null)
        {
            registeredProvider.UnregisterTarget(this);
            registeredProvider = null;
        }
    }

    private void RegisterWithProvider()
    {
        BossLockOnTargetProvider resolvedProvider =
            GetComponentInParent<BossLockOnTargetProvider>();
        if (registeredProvider == resolvedProvider)
        {
            return;
        }

        if (registeredProvider != null)
        {
            registeredProvider.UnregisterTarget(this);
        }

        registeredProvider = resolvedProvider;
        registeredProvider?.RegisterTarget(this);
    }

    private void OnValidate()
    {
        priority = Mathf.Max(1f, priority);
        minimumPhase = Mathf.Max(1, minimumPhase);
        maximumPhase = Mathf.Max(0, maximumPhase);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            targetId = gameObject.name;
        }
    }
}
