using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossLockOnTargetProvider : MonoBehaviour
{
    private const string RuntimeTargetRootName = "LockOnTargetRoot";
    private const int DefaultPrototypeTargetCount = 6;
    private const float MissingAnchorRebindInterval = 0.25f;

    [SerializeField, Min(0f)] private float viewportMargin = 0.08f;

    private readonly List<BossLockOnTarget> targets = new();
    private readonly List<BossLockOnTarget> validTargets = new();
    private readonly List<float> selectionWeights = new();
    private readonly List<BossLockOnTarget> selectionGroup = new();
    private readonly HashSet<BossLockOnTarget> selectedTargets = new();
    private BossController bossController;
    private BossTestState bossTestState;
    private Camera worldCamera;
    private bool subscribed;
    private float nextMissingAnchorRebindTime;

    public IReadOnlyList<BossLockOnTarget> Targets => targets;
    public int ValidTargetCount
    {
        get
        {
            CollectValidTargets(validTargets);
            return validTargets.Count;
        }
    }

    public bool HasValidTargets => ValidTargetCount > 0;

    public event Action TargetsChanged;

    public void Configure(
        BossController boss,
        BossTestState testState,
        Camera camera)
    {
        Unsubscribe();
        bossController = boss;
        bossTestState = testState;
        worldCamera = camera != null ? camera : Camera.main;
        EnsurePrototypeTargets();
        RefreshTargets();
        Subscribe();
        NotifyTargetsChanged();
    }

    public int CollectValidTargets(List<BossLockOnTarget> output)
    {
        if (output == null)
        {
            return 0;
        }

        output.Clear();
        RefreshTargetsIfNeeded();
        for (int i = 0; i < targets.Count; i++)
        {
            BossLockOnTarget target = targets[i];
            if (target != null && target.IsSelectable)
            {
                output.Add(target);
            }
        }

        return output.Count;
    }

    public int BuildTargetSequence(
        int requestedCount,
        int randomSeed,
        List<BossLockOnTarget> output,
        bool recordLockAssignments = true)
    {
        if (output == null)
        {
            return 0;
        }

        output.Clear();
        if (requestedCount <= 0 || CollectValidTargets(validTargets) == 0)
        {
            return 0;
        }

        Camera resolvedCamera = worldCamera != null ? worldCamera : Camera.main;
        int selectionCount = Mathf.Min(requestedCount, validTargets.Count);
        selectedTargets.Clear();
        RefreshSelectionEvaluations(resolvedCamera);
        AppendPriorityGroup(
            target => target.IsWeakPointOpen,
            selectionCount,
            randomSeed ^ 0x2F19,
            resolvedCamera,
            output);
        AppendPriorityGroup(
            target => target.IsPreparingStrongAttack,
            selectionCount,
            randomSeed ^ 0x61A7,
            resolvedCamera,
            output);
        AppendPriorityGroup(
            target => target.WasRecentlyAttacked,
            selectionCount,
            randomSeed ^ 0x73D5,
            resolvedCamera,
            output);
        AppendPriorityGroup(
            target => target.IsVisible,
            selectionCount,
            randomSeed ^ 0x1CA9,
            resolvedCamera,
            output);
        AppendPriorityGroup(
            target => target.IsLargePart,
            selectionCount,
            randomSeed ^ 0x5E83,
            resolvedCamera,
            output);
        AppendPriorityGroup(
            target => true,
            selectionCount,
            randomSeed ^ 0x4B3D,
            resolvedCamera,
            output);

        if (recordLockAssignments)
        {
            for (int i = 0; i < output.Count; i++)
            {
                output[i].MarkLocked(i + 1);
            }
        }

        return output.Count;
    }

    public BossLockOnTarget MarkNearestTargetRecentlyAttacked(Vector3 worldPosition)
    {
        CollectValidTargets(validTargets);
        BossLockOnTarget nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < validTargets.Count; i++)
        {
            BossLockOnTarget target = validTargets[i];
            float sqrDistance = (target.WorldPosition - worldPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearest = target;
            nearestSqrDistance = sqrDistance;
        }

        nearest?.MarkRecentlyAttacked();
        return nearest;
    }

    private void RefreshSelectionEvaluations(Camera resolvedCamera)
    {
        for (int i = 0; i < validTargets.Count; i++)
        {
            validTargets[i].EvaluateSelectionWeight(resolvedCamera, viewportMargin);
        }
    }

    private void AppendPriorityGroup(
        Predicate<BossLockOnTarget> predicate,
        int totalSelectionCount,
        int randomSeed,
        Camera resolvedCamera,
        List<BossLockOnTarget> output)
    {
        int remainingCount = totalSelectionCount - output.Count;
        if (remainingCount <= 0)
        {
            return;
        }

        selectionGroup.Clear();
        selectionWeights.Clear();
        for (int i = 0; i < validTargets.Count; i++)
        {
            BossLockOnTarget target = validTargets[i];
            if (selectedTargets.Contains(target) || !predicate(target))
            {
                continue;
            }

            selectionGroup.Add(target);
            selectionWeights.Add(target.EvaluateSelectionWeight(resolvedCamera, viewportMargin));
        }

        int groupSelectionCount = Mathf.Min(remainingCount, selectionGroup.Count);
        int[] sequence = LockOnTargetSelection.BuildWeightedRepeatedSequence(
            selectionWeights,
            groupSelectionCount,
            randomSeed);
        for (int i = 0; i < sequence.Length; i++)
        {
            BossLockOnTarget selected = selectionGroup[sequence[i]];
            if (selectedTargets.Add(selected))
            {
                output.Add(selected);
            }
        }
    }

    public SalvoTargetSnapshot CreateSalvoSnapshot(BossLockOnTarget target)
    {
        if (target == null || !target.IsSelectable)
        {
            return null;
        }

        bool openWeakPoint = target.IsWeakPointOpen;
        return new SalvoTargetSnapshot(
            target.AnchorTransform,
            target.TargetId,
            openWeakPoint,
            openWeakPoint ? 2f : 1f);
    }

    public void SetAllTargetsAttackableForDebug(bool attackable)
    {
        RefreshTargetsIfNeeded();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetAttackableForDebug(attackable);
            }
        }

        NotifyTargetsChanged();
    }

    internal void RegisterTarget(BossLockOnTarget target)
    {
        if (target == null)
        {
            return;
        }

        target.BindState(bossTestState);
        if (targets.Contains(target))
        {
            target.AvailabilityChanged -= HandleTargetAvailabilityChanged;
            target.AvailabilityChanged += HandleTargetAvailabilityChanged;
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            BossLockOnTarget existing = targets[i];
            if (existing != null &&
                string.Equals(existing.TargetId, target.TargetId, StringComparison.Ordinal))
            {
                Debug.LogError($"Duplicate lock-on targetId ignored: {target.TargetId}", target);
                return;
            }
        }

        targets.Add(target);
        target.AvailabilityChanged -= HandleTargetAvailabilityChanged;
        target.AvailabilityChanged += HandleTargetAvailabilityChanged;
        NotifyTargetsChanged();
    }

    internal void UnregisterTarget(BossLockOnTarget target)
    {
        if (target == null)
        {
            return;
        }

        target.AvailabilityChanged -= HandleTargetAvailabilityChanged;
        if (targets.Remove(target))
        {
            NotifyTargetsChanged();
        }
    }

    public void RefreshTargets()
    {
        UnsubscribeTargets();
        targets.Clear();
        HashSet<string> targetIds = new(StringComparer.Ordinal);
        BossLockOnTarget[] foundTargets = GetComponentsInChildren<BossLockOnTarget>(true);
        for (int i = 0; i < foundTargets.Length; i++)
        {
            BossLockOnTarget target = foundTargets[i];
            if (target == null)
            {
                continue;
            }

            target.BindState(bossTestState);
            if (!targetIds.Add(target.TargetId))
            {
                Debug.LogError($"Duplicate lock-on targetId ignored: {target.TargetId}", target);
                continue;
            }

            targets.Add(target);
        }

        SubscribeTargets();
    }

    private void EnsurePrototypeTargets()
    {
        if (GetComponentInChildren<BossLockOnTarget>(true) != null)
        {
            return;
        }

        Transform owner = bossController != null ? bossController.transform : transform;
        Transform targetRoot = owner.Find(RuntimeTargetRootName);
        if (targetRoot == null)
        {
            GameObject rootObject = new(RuntimeTargetRootName);
            rootObject.transform.SetParent(owner, false);
            targetRoot = rootObject.transform;
        }

        ResolvePrototypeBounds(owner, out Vector3 center, out Vector3 extents);
        Vector3 right = owner.right;
        Vector3 up = owner.up;
        Vector3 forward = owner.forward;
        Vector3[] positions =
        {
            center,
            center + up * (extents.y * 0.38f),
            center - right * (extents.x * 0.30f) + up * (extents.y * 0.10f),
            center + right * (extents.x * 0.30f) + up * (extents.y * 0.10f),
            center - up * (extents.y * 0.38f),
            center - forward * (extents.z * 0.25f) - up * (extents.y * 0.15f),
        };
        string[] names = { "Core", "HeadWeakPoint", "LeftUpper", "RightUpper", "LowerBody", "TailBase" };
        string[] ids = { "boss.core", "boss.head_weak", "boss.left_upper", "boss.right_upper", "boss.lower", "boss.tail_base" };
        string[] boneNames = { "Spine 02", "Head", "Clavicle L", "Clavicle R", "Pelvis", "Tail001" };
        float[] priorities = { 90f, 120f, 75f, 75f, 60f, 55f };

        for (int i = 0; i < DefaultPrototypeTargetCount; i++)
        {
            Transform trackingBone = FindDeepChild(owner, boneNames[i]);
            GameObject targetObject = new($"LockOnTarget_{names[i]}");
            targetObject.SetActive(false);
            targetObject.transform.SetParent(targetRoot, false);
            targetObject.transform.position = trackingBone != null
                ? trackingBone.position
                : positions[i];
            BossLockOnTarget target = targetObject.AddComponent<BossLockOnTarget>();
            target.ConfigurePrototype(
                ids[i],
                priorities[i],
                weakPoint: i == 1,
                largePart: i != 1,
                trackingAnchor: trackingBone,
                trackingAnchorName: boneNames[i]);
            targetObject.SetActive(true);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ResolvePrototypeBounds(
        Transform owner,
        out Vector3 center,
        out Vector3 extents)
    {
        Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.name == "BossHurtbox")
            {
                center = collider.bounds.center;
                extents = collider.bounds.extents;
                extents.x = Mathf.Max(1f, extents.x);
                extents.y = Mathf.Max(1f, extents.y);
                extents.z = Mathf.Max(1f, extents.z);
                return;
            }
        }

        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        center = hasBounds ? combined.center : owner.position + owner.up * 4f;
        extents = hasBounds ? combined.extents : new Vector3(3f, 5f, 2f);
        extents.x = Mathf.Max(1f, extents.x);
        extents.y = Mathf.Max(1f, extents.y);
        extents.z = Mathf.Max(1f, extents.z);
    }

    private void RefreshTargetsIfNeeded()
    {
        if (targets.Count == 0 || HasMissingTarget())
        {
            EnsurePrototypeTargets();
            RefreshTargets();
        }

        RebindMissingAnchorsIfNeeded();
    }

    private void RebindMissingAnchorsIfNeeded()
    {
        bool hasMissingAnchor = false;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && targets[i].NeedsAnchorRebind)
            {
                hasMissingAnchor = true;
                break;
            }
        }

        if (!hasMissingAnchor)
        {
            return;
        }

        if (Time.unscaledTime < nextMissingAnchorRebindTime)
        {
            return;
        }

        nextMissingAnchorRebindTime = Time.unscaledTime + MissingAnchorRebindInterval;
        Transform owner = bossController != null ? bossController.transform : transform;
        for (int i = 0; i < targets.Count; i++)
        {
            BossLockOnTarget target = targets[i];
            if (target == null || !target.NeedsAnchorRebind)
            {
                continue;
            }

            Transform reboundAnchor = FindDeepChild(owner, target.AnchorLookupName);
            if (reboundAnchor == null)
            {
                continue;
            }

            target.RebindAnchor(reboundAnchor);
        }
    }

    private bool HasMissingTarget()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private void Subscribe()
    {
        if (subscribed || bossTestState == null)
        {
            return;
        }

        bossTestState.OnBossPhaseChanged += HandlePhaseChanged;
        bossTestState.OnWeakPointStateChanged += HandleWeakPointChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || bossTestState == null)
        {
            subscribed = false;
            return;
        }

        bossTestState.OnBossPhaseChanged -= HandlePhaseChanged;
        bossTestState.OnWeakPointStateChanged -= HandleWeakPointChanged;
        subscribed = false;
    }

    private void SubscribeTargets()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].AvailabilityChanged -= HandleTargetAvailabilityChanged;
            targets[i].AvailabilityChanged += HandleTargetAvailabilityChanged;
        }
    }

    private void UnsubscribeTargets()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].AvailabilityChanged -= HandleTargetAvailabilityChanged;
            }
        }
    }

    private void HandlePhaseChanged(int phase)
    {
        NotifyTargetsChanged();
    }

    private void HandleWeakPointChanged(bool open)
    {
        NotifyTargetsChanged();
    }

    private void HandleTargetAvailabilityChanged(BossLockOnTarget target)
    {
        NotifyTargetsChanged();
    }

    private void NotifyTargetsChanged()
    {
        TargetsChanged?.Invoke();
    }

    private void OnEnable()
    {
        Subscribe();
        SubscribeTargets();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeTargets();
    }
}
