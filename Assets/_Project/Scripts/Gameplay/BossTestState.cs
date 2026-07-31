using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossTestState : MonoBehaviour
{
    [SerializeField, Min(1)] private int currentPhase = 1;
    [SerializeField] private bool isWeakPointOpen;

    public int CurrentPhase => currentPhase;
    public bool IsWeakPointOpen => isWeakPointOpen;

    public event Action<int> OnBossPhaseChanged;
    public event Action<bool> OnWeakPointStateChanged;

    private void Awake()
    {
        currentPhase = Mathf.Max(1, currentPhase);
    }

    public void SetPhase(int phase)
    {
        int resolvedPhase = Mathf.Max(1, phase);
        if (currentPhase == resolvedPhase)
        {
            return;
        }

        currentPhase = resolvedPhase;
        OnBossPhaseChanged?.Invoke(currentPhase);
    }

    public void AdvancePhase()
    {
        SetPhase(currentPhase + 1);
    }

    public void SetWeakPointOpen(bool open)
    {
        if (isWeakPointOpen == open)
        {
            return;
        }

        isWeakPointOpen = open;
        OnWeakPointStateChanged?.Invoke(isWeakPointOpen);
    }

    public void ToggleWeakPoint()
    {
        SetWeakPointOpen(!isWeakPointOpen);
    }

    private void OnValidate()
    {
        currentPhase = Mathf.Max(1, currentPhase);
    }
}
