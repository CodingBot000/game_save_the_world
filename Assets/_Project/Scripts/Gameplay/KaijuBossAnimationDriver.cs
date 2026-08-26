using System.Collections;
using UnityEngine;

/// <summary>Owns pose/timing only. Existing pattern controllers still own damage and projectiles.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class KaijuBossAnimationDriver : MonoBehaviour
{
    public enum ActionKind { None, Firing, BeamLeftToRight, BeamRightToLeft, Tail, JumpTurn, Death }

    // Initial art timings, in seconds at 30 fps. The editor builder uses these same constants.
    public const float FireCueTime = 9f / 30f;
    public const float BeamCueTime = 20f / 30f;
    public const float BeamEndTime = 68f / 30f;
    public const float TailCueTime = 48f / 30f;
    public const float TurnStartTime = 5f / 30f;
    public const float TurnEndTime = 32f / 30f;

    [SerializeField] private BossController boss;
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform tailImpact;
    [SerializeField, Range(45f, 90f)] private float minTurnAngle = 45f;
    [SerializeField, Range(45f, 90f)] private float maxTurnAngle = 90f;
    [SerializeField, Min(0f)] private float turnCooldown = 0.3f;
    [SerializeField, Min(0f)] private float aimDamping = 0.12f;

    private Animator animator;
    private bool patternActive;
    private bool paused;
    private bool released;
    private bool sustainFiring;
    private bool turning;
    private bool dead;
    private int sequence;
    private float activeBeamDuration;
    private float nextTurnTime;
    private Quaternion turnFrom;
    private Quaternion turnTo;

    public ActionKind CurrentAction { get; private set; }
    public bool IsBusy => CurrentAction != ActionKind.None;
    public bool IsDead => dead;
    public bool IsTurning => turning;
    public bool IsPaused => paused;
    public float TargetAngle { get; private set; }
    public int ReleasedCueCount { get; private set; }
    public int Sequence => sequence;
    public Transform Mouth => mouth;
    public Transform TailImpact => tailImpact;
    public Animator Animator => animator != null ? animator : GetComponent<Animator>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (boss == null) boss = GetComponentInParent<BossController>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.fireEvents = true;
        if (boss != null) boss.Died += HandleDeath;
    }

    private void OnDestroy()
    {
        if (boss != null) boss.Died -= HandleDeath;
    }

    public void Configure(BossController owner, Transform mouthSocket, Transform tailSocket)
    {
        // Called by the editor builder before play, not a second runtime subscription.
        boss = owner;
        mouth = mouthSocket;
        tailImpact = tailSocket;
    }

    public void TrackTarget(Vector3 worldTarget)
    {
        if (dead || paused || boss == null || !boss.IsAlive) return;
        Vector3 direction = Vector3.ProjectOnPlane(worldTarget - boss.transform.position, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (direction.sqrMagnitude < 0.001f) return;
        TargetAngle = Vector3.SignedAngle(forward, direction, Vector3.up);
        Animator.SetFloat("TargetAngle", Mathf.Clamp(TargetAngle, -45f, 45f), aimDamping, Time.deltaTime);
        if (!patternActive && !IsBusy && Mathf.Abs(TargetAngle) > 45f && Time.time >= nextTurnTime)
            BeginTurn(Mathf.Sign(TargetAngle) * Mathf.Min(Mathf.Abs(TargetAngle),
                Random.Range(Mathf.Min(minTurnAngle, maxTurnAngle), Mathf.Max(minTurnAngle, maxTurnAngle))));
    }

    public void BeginPattern() => patternActive = true;
    public void EndPattern() => patternActive = false;

    public int BeginFiring(float windup = FireCueTime, bool sustain = false)
    {
        if (!CanBegin()) return -1;
        bool interruptFullBody = CurrentAction != ActionKind.None && CurrentAction != ActionKind.Firing;
        Begin(ActionKind.Firing);
        if (interruptFullBody) Animator.Play("Base Layer.BasicIdle", 0, 0f);
        sustainFiring = sustain;
        Animator.SetFloat("FiringSpeed", FireCueTime / Mathf.Max(0.05f, windup));
        Animator.SetLayerWeight(1, 1f);
        Animator.Play("UpperBody.Firing", 1, 0f);
        return sequence;
    }

    public void AdoptCompatibilityState(ActionKind kind)
    {
        // Also support callers that still set Attack1/Attack2 directly on the Animator.
        // Explicit BeginFiring/BeginBeam calls have already set their action and timing.
        if (CurrentAction == kind || !CanBegin()) return;
        Begin(kind);
        if (kind == ActionKind.Firing)
        {
            Animator.SetFloat("FiringSpeed", 1f);
            Animator.Play("Base Layer.BasicIdle", 0, 0f);
            Animator.SetLayerWeight(1, 1f);
        }
        else
        {
            activeBeamDuration = BeamEndTime - BeamCueTime;
            Animator.SetFloat("ActionSpeed", 1f);
            Animator.SetLayerWeight(1, 0f);
        }
    }

    public int BeginBeam(bool leftToRight, float windup, float duration)
    {
        if (!CanBegin()) return -1;
        Begin(leftToRight ? ActionKind.BeamLeftToRight : ActionKind.BeamRightToLeft);
        activeBeamDuration = Mathf.Max(0.05f, duration);
        Animator.SetFloat("ActionSpeed", BeamCueTime / Mathf.Max(0.05f, windup));
        PlayFullBody(leftToRight ? "BeamLeftToR" : "BeamRightToL");
        return sequence;
    }

    public int BeginTail()
    {
        if (!CanBegin()) return -1;
        Begin(ActionKind.Tail);
        Animator.SetFloat("ActionSpeed", 1f);
        PlayFullBody("Tail");
        return sequence;
    }

    public void BeginTurn(float signedDegrees)
    {
        if (!CanBegin() || IsBusy || boss == null) return;
        Begin(ActionKind.JumpTurn);
        turnFrom = boss.transform.rotation;
        turnTo = Quaternion.AngleAxis(Mathf.Clamp(signedDegrees, -90f, 90f), Vector3.up) * turnFrom;
        Animator.SetFloat("ActionSpeed", 1f);
        PlayFullBody("JumpTurnR");
    }

    private bool CanBegin() => isActiveAndEnabled && !dead && !paused && (boss == null || boss.IsAlive);

    private void Begin(ActionKind kind)
    {
        sequence++;
        released = false;
        sustainFiring = false;
        turning = false;
        CurrentAction = kind;
        Animator.ResetTrigger("Attack1");
        Animator.ResetTrigger("Attack2");
    }

    private void PlayFullBody(string state)
    {
        Animator.Play("UpperBody.AimIdle", 1, 0f);
        Animator.SetLayerWeight(1, 0f);
        Animator.Play("Base Layer." + state, 0, 0f);
    }

    public bool WasReleased(int ticket) => ticket >= 0 && ticket == sequence && released && !dead && !paused;

    public IEnumerator WaitForRelease(int ticket)
    {
        // No timed fallback: missing events must never silently launch a damaging attack.
        float timeout = 0f;
        while (ticket >= 0 && ticket == sequence && IsBusy && !released && !dead)
        {
            if (!paused) timeout += Time.deltaTime;
            if (timeout > 15f)
            {
                Debug.LogError($"Kaiju animation cue missing: {CurrentAction}. Attack cancelled.", this);
                CancelAction();
                yield break;
            }
            yield return null;
        }
    }

    public IEnumerator WaitForRecovery()
    {
        float timeout = 0f;
        while (IsBusy && !dead)
        {
            if (!paused) timeout += Time.deltaTime;
            if (timeout > 15f) { CancelAction(); yield break; }
            yield return null;
        }
    }

    public void ReleaseSustainedFiring()
    {
        if (CurrentAction != ActionKind.Firing || !sustainFiring || dead) return;
        sustainFiring = false;
        Animator.SetFloat("FiringSpeed", 1f);
    }

    public void SetCinematicPaused(bool value)
    {
        if (dead) return;
        paused = value;
        Animator.speed = value ? 0f : 1f;
    }

    public void CancelAction()
    {
        if (dead) return;
        sequence++;
        released = false;
        patternActive = false;
        ReturnToIdle();
    }

    private void ReturnToIdle()
    {
        turning = false;
        sustainFiring = false;
        CurrentAction = ActionKind.None;
        Animator.SetFloat("ActionSpeed", 1f);
        Animator.SetFloat("FiringSpeed", 1f);
        Animator.Play("Base Layer.BasicIdle", 0, 0f);
        Animator.Play("UpperBody.AimIdle", 1, 0f);
        Animator.SetLayerWeight(1, 1f);
    }

    private void ReleaseCue()
    {
        if (released || dead || paused) return; // Three blend-tree clips may emit the same event.
        released = true;
        ReleasedCueCount++;
    }

    // Animation Events live on this same GameObject as the Animator.
    public void OnFireProjectile()
    {
        if (CurrentAction != ActionKind.Firing || released) return;
        ReleaseCue();
        Animator.SetFloat("FiringSpeed", sustainFiring ? 0f : 1f);
    }
    public void OnFiringEnd() { if (CurrentAction == ActionKind.Firing && !sustainFiring && !dead) ReturnToIdle(); }
    public void OnBeamStart()
    {
        if (!IsBeam() || released) return;
        ReleaseCue();
        Animator.SetFloat("ActionSpeed", (BeamEndTime - BeamCueTime) / activeBeamDuration);
    }
    public void OnBeamEnd() { if (IsBeam()) Animator.SetFloat("ActionSpeed", 1f); }
    public void OnBeamRecovered() { if (IsBeam() && !dead) ReturnToIdle(); }
    public void OnTailImpact() { if (CurrentAction == ActionKind.Tail) ReleaseCue(); }
    public void OnTailEnd() { if (CurrentAction == ActionKind.Tail && !dead) ReturnToIdle(); }
    private bool IsBeam() => CurrentAction == ActionKind.BeamLeftToRight || CurrentAction == ActionKind.BeamRightToLeft;
    public void OnJumpTurnStart() { if (CurrentAction == ActionKind.JumpTurn && !dead) turning = true; }
    public void OnJumpTurnEnd()
    {
        if (CurrentAction != ActionKind.JumpTurn || dead) return;
        boss.transform.rotation = turnTo;
        turning = false;
    }
    public void OnJumpTurnRecovered()
    {
        if (CurrentAction != ActionKind.JumpTurn || dead) return;
        nextTurnTime = Time.time + turnCooldown;
        ReturnToIdle();
    }

    private void LateUpdate()
    {
        if (!turning || dead || paused || boss == null) return;
        float clipTime = Animator.GetCurrentAnimatorStateInfo(0).normalizedTime * (37f / 30f);
        float t = Mathf.InverseLerp(TurnStartTime, TurnEndTime, clipTime);
        boss.transform.rotation = Quaternion.Slerp(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, t));
    }

    private void HandleDeath()
    {
        dead = true;
        paused = false;
        turning = false;
        patternActive = false;
        released = false;
        sequence++;
        CurrentAction = ActionKind.Death;
        Animator.speed = 1f;
        Animator.SetFloat("ActionSpeed", 1f);
        PlayFullBody("Death"); // No exit transition: retain the last death pose.
    }
}
