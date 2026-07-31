using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class LockOnButtonInputRelay : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    private PlayerLockOnController lockOnController;
    private int activePointerId = int.MinValue;

    public bool HasActivePointer => activePointerId != int.MinValue;

    public void Configure(PlayerLockOnController controller)
    {
        if (lockOnController != null)
        {
            lockOnController.OnLockCanceled -= HandleLockCanceled;
        }

        lockOnController = controller;
        activePointerId = int.MinValue;
        if (lockOnController != null)
        {
            lockOnController.OnLockCanceled += HandleLockCanceled;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left ||
            activePointerId != int.MinValue ||
            lockOnController == null)
        {
            return;
        }

        if (lockOnController.TryBeginCharging(LockOnInputSource.MobileHud))
        {
            activePointerId = eventData.pointerId;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!MatchesActivePointer(eventData))
        {
            return;
        }

        activePointerId = int.MinValue;
        lockOnController?.TryReleaseCharging(LockOnInputSource.MobileHud);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!MatchesActivePointer(eventData))
        {
            return;
        }

        activePointerId = int.MinValue;
        lockOnController?.HandlePointerExit();
    }

    private bool MatchesActivePointer(PointerEventData eventData)
    {
        return eventData != null &&
               activePointerId != int.MinValue &&
               eventData.pointerId == activePointerId;
    }

    private void HandleLockCanceled(LockOnCancelReason reason)
    {
        activePointerId = int.MinValue;
    }

    private void OnDisable()
    {
        if (activePointerId == int.MinValue)
        {
            return;
        }

        activePointerId = int.MinValue;
        lockOnController?.HandlePointerExit();
    }

    private void OnDestroy()
    {
        if (lockOnController != null)
        {
            lockOnController.OnLockCanceled -= HandleLockCanceled;
        }
    }
}
