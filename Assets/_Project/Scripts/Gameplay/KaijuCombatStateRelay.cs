using UnityEngine;

/// <summary>Bridges legacy Animator triggers to the same event/interrupt bookkeeping.</summary>
public sealed class KaijuCombatStateRelay : StateMachineBehaviour
{
    public KaijuBossAnimationDriver.ActionKind action;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var driver = animator.GetComponent<KaijuBossAnimationDriver>();
        if (driver != null) driver.AdoptCompatibilityState(action);
    }
}
