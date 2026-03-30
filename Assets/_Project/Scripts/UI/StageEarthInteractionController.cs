using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(Collider))]
public class StageEarthInteractionController : MonoBehaviour
{
    [SerializeField] private float autoSpinSpeed = 12f;
    [SerializeField] private float autoSpinResumeDelay = 1f;
    [SerializeField] private float dragRotationSpeed = 0.18f;
    [SerializeField] private float mouseWheelZoomFactor = 0.0025f;
    [SerializeField] private float pinchZoomFactor = 0.003f;
    [SerializeField] private float minScale = 1.5f;
    [SerializeField] private float maxScale = 3.8f;
    [SerializeField] private float tapMovementThreshold = 12f;
    [SerializeField] private float tapMaxDuration = 0.3f;
    [SerializeField] private float tiltAngleDegrees = 23.5f;

    private Camera stageCamera;
    private Collider globeCollider;
    private Quaternion userRotation = Quaternion.identity;
    private Vector3 scaleBasis = Vector3.one;
    private float currentScale = 1f;
    private float autoSpinAngle;
    private float lastInteractionTime = float.NegativeInfinity;

    private bool mousePointerActive;
    private bool mouseTapCandidate;
    private Vector2 mouseStartPosition;
    private Vector2 mouseLastPosition;
    private float mousePressTime;

    private bool touchPointerActive;
    private bool touchTapCandidate;
    private Vector2 touchStartPosition;
    private Vector2 touchLastPosition;
    private float touchPressTime;
    private bool pinchActive;
    private float previousPinchDistance;

    public event Action<Vector3> Selected;

    private void Awake()
    {
        globeCollider = GetComponent<Collider>();

        float initialScale = Mathf.Max(0.0001f, transform.localScale.x);
        scaleBasis = transform.localScale / initialScale;
        currentScale = initialScale;

        ResolveCamera();
    }

    private void Update()
    {
        ResolveCamera();
        HandleTouchInput();
        HandleMouseInput();
        UpdateAutoSpin();
        ApplyPose();
    }

    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scrollDelta = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > 0.001f)
        {
            AdjustScale(scrollDelta * mouseWheelZoomFactor);
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame && IsPointerOverEarth(pointerPosition))
        {
            mousePointerActive = true;
            mouseTapCandidate = true;
            mouseStartPosition = pointerPosition;
            mouseLastPosition = pointerPosition;
            mousePressTime = Time.unscaledTime;
        }

        if (mousePointerActive && mouse.leftButton.isPressed)
        {
            Vector2 delta = pointerPosition - mouseLastPosition;
            mouseLastPosition = pointerPosition;

            if (Vector2.Distance(pointerPosition, mouseStartPosition) > tapMovementThreshold)
            {
                mouseTapCandidate = false;
            }

            if (!mouseTapCandidate && delta.sqrMagnitude > 0.0001f)
            {
                RotateFromDelta(delta);
            }
        }

        if (mousePointerActive && mouse.leftButton.wasReleasedThisFrame)
        {
            RaycastHit mouseHit = default;
            bool shouldSelect = mouseTapCandidate && Time.unscaledTime - mousePressTime <= tapMaxDuration;
            if (shouldSelect)
            {
                shouldSelect = TryGetEarthHit(pointerPosition, out mouseHit);
            }

            mousePointerActive = false;
            mouseTapCandidate = false;

            if (shouldSelect)
            {
                Selected?.Invoke(mouseHit.point);
            }
        }
    }

    private void HandleTouchInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            pinchActive = false;
            return;
        }

        int activeTouchCount = 0;
        bool hasFirstTouch = false;
        bool hasSecondTouch = false;
        Vector2 firstTouchPosition = Vector2.zero;
        Vector2 secondTouchPosition = Vector2.zero;

        foreach (var touch in touchscreen.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            activeTouchCount++;
            if (!hasFirstTouch)
            {
                firstTouchPosition = touch.position.ReadValue();
                hasFirstTouch = true;
            }
            else if (!hasSecondTouch)
            {
                secondTouchPosition = touch.position.ReadValue();
                hasSecondTouch = true;
            }
        }

        if (activeTouchCount >= 2 && hasFirstTouch && hasSecondTouch)
        {
            float pinchDistance = Vector2.Distance(firstTouchPosition, secondTouchPosition);
            if (pinchActive)
            {
                AdjustScale((pinchDistance - previousPinchDistance) * pinchZoomFactor);
            }

            previousPinchDistance = pinchDistance;
            pinchActive = true;
            touchPointerActive = false;
            touchTapCandidate = false;
            return;
        }

        pinchActive = false;

        var primaryTouch = touchscreen.primaryTouch;
        if (primaryTouch == null)
        {
            touchPointerActive = false;
            touchTapCandidate = false;
            return;
        }

        Vector2 touchPosition = primaryTouch.position.ReadValue();

        if (primaryTouch.press.wasPressedThisFrame && IsPointerOverEarth(touchPosition))
        {
            touchPointerActive = true;
            touchTapCandidate = true;
            touchStartPosition = touchPosition;
            touchLastPosition = touchPosition;
            touchPressTime = Time.unscaledTime;
        }

        if (touchPointerActive && primaryTouch.press.isPressed)
        {
            Vector2 delta = touchPosition - touchLastPosition;
            touchLastPosition = touchPosition;

            if (Vector2.Distance(touchPosition, touchStartPosition) > tapMovementThreshold)
            {
                touchTapCandidate = false;
            }

            if (!touchTapCandidate && delta.sqrMagnitude > 0.0001f)
            {
                RotateFromDelta(delta);
            }
        }

        if (touchPointerActive && primaryTouch.press.wasReleasedThisFrame)
        {
            RaycastHit touchHit = default;
            bool shouldSelect = touchTapCandidate && Time.unscaledTime - touchPressTime <= tapMaxDuration;
            if (shouldSelect)
            {
                shouldSelect = TryGetEarthHit(touchPosition, out touchHit);
            }

            touchPointerActive = false;
            touchTapCandidate = false;

            if (shouldSelect)
            {
                Selected?.Invoke(touchHit.point);
            }
        }
    }

    private void RotateFromDelta(Vector2 delta)
    {
        if (stageCamera == null)
        {
            return;
        }

        Quaternion yaw = Quaternion.AngleAxis(-delta.x * dragRotationSpeed, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(delta.y * dragRotationSpeed, stageCamera.transform.right);
        userRotation = yaw * pitch * userRotation;
        lastInteractionTime = Time.unscaledTime;
    }

    private void AdjustScale(float delta)
    {
        if (Mathf.Abs(delta) < 0.0001f)
        {
            return;
        }

        currentScale = Mathf.Clamp(currentScale + delta, minScale, maxScale);
        lastInteractionTime = Time.unscaledTime;
    }

    private void UpdateAutoSpin()
    {
        bool userInteracting = mousePointerActive || touchPointerActive || pinchActive;
        if (userInteracting)
        {
            return;
        }

        if (Time.unscaledTime - lastInteractionTime < autoSpinResumeDelay)
        {
            return;
        }

        autoSpinAngle += autoSpinSpeed * Time.unscaledDeltaTime;
    }

    private void ApplyPose()
    {
        Quaternion tiltRotation = Quaternion.AngleAxis(tiltAngleDegrees, Vector3.forward);
        Quaternion spinRotation = Quaternion.AngleAxis(autoSpinAngle, Vector3.up);
        transform.rotation = userRotation * tiltRotation * spinRotation;
        transform.localScale = scaleBasis * currentScale;
    }

    private bool IsPointerOverEarth(Vector2 screenPosition)
    {
        return TryGetEarthHit(screenPosition, out _);
    }

    private bool TryGetEarthHit(Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (stageCamera == null || globeCollider == null)
        {
            return false;
        }

        Ray ray = stageCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out hit, 100f))
        {
            return false;
        }

        return hit.collider == globeCollider || hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private void ResolveCamera()
    {
        if (stageCamera == null)
        {
            stageCamera = Camera.main;
        }

        if (stageCamera == null)
        {
            stageCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        }
    }
}
