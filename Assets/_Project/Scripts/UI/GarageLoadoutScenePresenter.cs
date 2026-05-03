using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GarageLoadoutScenePresenter : MonoBehaviour
{
    [SerializeField] private GarageLoadoutSceneView view;
    [SerializeField] private float previewYawSpeed = 12f;

    private GarageLoadoutState loadoutState;
    private HelicopterSelectionState helicopterSelectionState;
    private RenderTexture previewTexture;
    private GameObject previewInstance;

    private void Awake()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
        BindState();
        RefreshVehiclePreview();
        RefreshSelectionVisuals();
    }

    private void OnEnable()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
        BindState();
        RefreshVehiclePreview();
        RefreshSelectionVisuals();
    }

    private void OnDisable()
    {
        if (loadoutState != null)
        {
            loadoutState.LoadoutChanged -= HandleLoadoutChanged;
        }

        if (helicopterSelectionState != null)
        {
            helicopterSelectionState.SelectionChanged -= RefreshVehiclePreview;
        }
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScene();
            return;
        }

        ApplyBackgroundCover();

        if (view != null && view.HelicopterPreviewAnchor != null)
        {
            view.HelicopterPreviewAnchor.Rotate(Vector3.up, previewYawSpeed * Time.unscaledDeltaTime, Space.World);
        }
    }

    private void ResolveView()
    {
        if (view == null)
        {
            view = GetComponent<GarageLoadoutSceneView>();
        }

        if (view == null)
        {
            view = GetComponentInChildren<GarageLoadoutSceneView>(true);
        }
    }

    private void ConfigureBackgroundImage()
    {
        if (view.BackgroundImage == null)
        {
            return;
        }

        view.BackgroundImage.raycastTarget = false;
        view.BackgroundImage.preserveAspect = true;
        if (view.BackgroundImage.sprite != null)
        {
            view.BackgroundImage.color = Color.white;
        }

        ApplyBackgroundCover();
    }

    private void ApplyBackgroundCover()
    {
        if (view == null || view.BackgroundImage == null || view.BackgroundImage.sprite == null)
        {
            return;
        }

        RectTransform backgroundRect = view.BackgroundImage.rectTransform;
        RectTransform parentRect = backgroundRect.parent as RectTransform;
        Vector2 containerSize = parentRect != null ? parentRect.rect.size : new Vector2(Screen.width, Screen.height);
        if (containerSize.x <= 0.01f || containerSize.y <= 0.01f)
        {
            containerSize = new Vector2(Screen.width, Screen.height);
        }

        Vector2 spriteSize = view.BackgroundImage.sprite.rect.size;
        if (containerSize.x <= 0.01f || containerSize.y <= 0.01f || spriteSize.x <= 0.01f || spriteSize.y <= 0.01f)
        {
            return;
        }

        float containerAspect = containerSize.x / containerSize.y;
        float spriteAspect = spriteSize.x / spriteSize.y;
        Vector2 coverSize = containerAspect > spriteAspect
            ? new Vector2(containerSize.x, containerSize.x / spriteAspect)
            : new Vector2(containerSize.y * spriteAspect, containerSize.y);

        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = coverSize;
    }

    private void BindView()
    {
        if (view == null || !view.IsConfigured)
        {
            return;
        }

        Canvas canvas = view.Canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        ConfigureBackgroundImage();

        view.CloseButton.onClick.RemoveAllListeners();
        view.CloseButton.onClick.AddListener(CloseScene);

        EnsurePreviewTexture();
        BindItemButtons();
    }

    private void BindState()
    {
        GarageLoadoutState nextLoadoutState = GarageLoadoutState.EnsureInitialized();
        if (loadoutState != nextLoadoutState)
        {
            if (loadoutState != null)
            {
                loadoutState.LoadoutChanged -= HandleLoadoutChanged;
            }

            loadoutState = nextLoadoutState;
            loadoutState.LoadoutChanged += HandleLoadoutChanged;
        }

        HelicopterSelectionState nextHelicopterState = HelicopterSelectionState.EnsureInitialized();
        if (helicopterSelectionState != nextHelicopterState)
        {
            if (helicopterSelectionState != null)
            {
                helicopterSelectionState.SelectionChanged -= RefreshVehiclePreview;
            }

            helicopterSelectionState = nextHelicopterState;
            helicopterSelectionState.SelectionChanged += RefreshVehiclePreview;
        }
    }

    private void EnsurePreviewTexture()
    {
        if (view.HelicopterPreviewCamera == null || view.HelicopterPreviewImage == null)
        {
            return;
        }

        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
            {
                name = "GarageHelicopterPreviewTexture",
                antiAliasing = 4
            };
            previewTexture.Create();
        }

        view.HelicopterPreviewCamera.targetTexture = previewTexture;
        view.HelicopterPreviewImage.texture = previewTexture;
    }

    private void BindItemButtons()
    {
        GarageLoadoutItemView[] itemViews = view.ItemViews;
        for (int i = 0; i < itemViews.Length; i++)
        {
            GarageLoadoutItemView itemView = itemViews[i];
            if (itemView == null || !itemView.IsConfigured)
            {
                continue;
            }

            itemView.ApplyStaticContent();
            itemView.Button.onClick.RemoveAllListeners();
            itemView.Button.onClick.AddListener(() => SelectItem(itemView));
        }
    }

    private void SelectItem(GarageLoadoutItemView itemView)
    {
        if (loadoutState == null || itemView == null)
        {
            return;
        }

        loadoutState.SetSelection(itemView.SlotType, itemView.ItemId);
    }

    private void HandleLoadoutChanged()
    {
        RefreshSelectionVisuals();
        ApplyLoadoutPreviewTint();
    }

    private void RefreshSelectionVisuals()
    {
        if (view == null || view.ItemViews == null || loadoutState == null)
        {
            return;
        }

        for (int i = 0; i < view.ItemViews.Length; i++)
        {
            GarageLoadoutItemView itemView = view.ItemViews[i];
            if (itemView == null)
            {
                continue;
            }

            itemView.SetSelected(loadoutState.GetSelection(itemView.SlotType) == itemView.ItemId);
        }
    }

    private void RefreshVehiclePreview()
    {
        if (view == null || view.HelicopterPreviewAnchor == null)
        {
            return;
        }

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }

        VehicleDefinition selectedVehicle = helicopterSelectionState != null
            ? helicopterSelectionState.EnsureSelectedHelicopter()
            : HelicopterSelectionState.EnsureInitialized().EnsureSelectedHelicopter();

        if (view.SelectedVehicleText != null)
        {
            view.SelectedVehicleText.text = selectedVehicle != null ? selectedVehicle.DisplayName : "No helicopter selected";
        }

        if (selectedVehicle != null && selectedVehicle.Prefab != null)
        {
            previewInstance = Instantiate(selectedVehicle.Prefab, view.HelicopterPreviewAnchor);
        }
        else
        {
            previewInstance = CreateFallbackHelicopter(view.HelicopterPreviewAnchor);
        }

        previewInstance.name = "GaragePreviewHelicopter";
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localRotation = Quaternion.Euler(0f, 210f, 0f);
        previewInstance.transform.localScale = Vector3.one;

        FitPreviewInstance();
        ApplyLoadoutPreviewTint();
    }

    private GameObject CreateFallbackHelicopter(Transform parent)
    {
        GameObject fallback = new GameObject("FallbackHelicopter");
        fallback.transform.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(fallback.transform, false);
        body.transform.localScale = new Vector3(0.8f, 0.8f, 2.2f);

        GameObject rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rotor.name = "Rotor";
        rotor.transform.SetParent(fallback.transform, false);
        rotor.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        rotor.transform.localScale = new Vector3(3.4f, 0.06f, 0.16f);

        return fallback;
    }

    private void FitPreviewInstance()
    {
        Bounds bounds;
        if (previewInstance == null || !TryCalculateBounds(previewInstance, out bounds))
        {
            return;
        }

        float largestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largestAxis <= 0.001f)
        {
            return;
        }

        float scale = 3.4f / largestAxis;
        previewInstance.transform.localScale = Vector3.one * scale;

        if (TryCalculateBounds(previewInstance, out Bounds scaledBounds))
        {
            previewInstance.transform.position += view.HelicopterPreviewAnchor.position - scaledBounds.center;
        }
    }

    private bool TryCalculateBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    private void ApplyLoadoutPreviewTint()
    {
        if (previewInstance == null || view == null || loadoutState == null)
        {
            return;
        }

        Color tint = ResolveSelectedColor(GarageLoadoutSlotType.Armor, new Color(0.82f, 0.88f, 0.9f, 1f));
        Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            targetRenderer.SetPropertyBlock(block);
        }
    }

    private Color ResolveSelectedColor(GarageLoadoutSlotType slotType, Color fallback)
    {
        if (view == null || view.ItemViews == null || loadoutState == null)
        {
            return fallback;
        }

        string selectedItemId = loadoutState.GetSelection(slotType);
        for (int i = 0; i < view.ItemViews.Length; i++)
        {
            GarageLoadoutItemView itemView = view.ItemViews[i];
            if (itemView != null && itemView.SlotType == slotType && itemView.ItemId == selectedItemId)
            {
                return itemView.PreviewColor;
            }
        }

        return fallback;
    }

    private void EnsureStandaloneRuntimeSupport()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }
    }

    private void CloseScene()
    {
        if (SceneManager.sceneCount <= 1)
        {
            SceneManager.LoadScene(GameFlowController.MainMenuSceneName);
            return;
        }

        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
