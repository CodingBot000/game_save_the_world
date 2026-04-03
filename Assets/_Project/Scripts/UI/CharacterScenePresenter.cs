using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterScenePresenter : MonoBehaviour
{
    private const string PilotImageResourcePath = "Player/player";

    [SerializeField] private CharacterSceneView view;

    private Sprite runtimePilotSprite;

    private void Awake()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
    }

    private void OnEnable()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScene();
        }
    }

    private void OnDestroy()
    {
        if (runtimePilotSprite == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimePilotSprite);
        }
        else
        {
            DestroyImmediate(runtimePilotSprite);
        }
    }

    private void EnsureStandaloneRuntimeSupport()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // CharacterScene 바로 실행시 에러 방지를 위한 방어코드.
        EnsureEventSystem();
        EnsureFallbackDisplayCamera();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void EnsureFallbackDisplayCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy &&
                candidate.targetTexture == null)
            {
                return;
            }
        }

        GameObject cameraObject = new GameObject("CharacterStandaloneCamera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);

        Camera fallbackCamera = cameraObject.GetComponent<Camera>();
        fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
        fallbackCamera.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        fallbackCamera.cullingMask = 0;
        fallbackCamera.depth = -100f;
        fallbackCamera.nearClipPlane = 0.1f;
        fallbackCamera.farClipPlane = 10f;
    }

    private void ResolveView()
    {
        if (view == null)
        {
            view = GetComponent<CharacterSceneView>();
        }

        if (view == null)
        {
            view = GetComponentInChildren<CharacterSceneView>(true);
        }
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
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        view.CloseButton.onClick.RemoveAllListeners();
        view.CloseButton.onClick.AddListener(CloseScene);
        ApplyPilotImage();
    }

    private void ApplyPilotImage()
    {
        if (view.Canvas == null)
        {
            return;
        }

        Texture2D pilotTexture = Resources.Load<Texture2D>(PilotImageResourcePath);
        if (pilotTexture == null)
        {
            return;
        }

        Transform pilotTransform = view.Canvas.transform.Find("CharacterRoot/Panel/ContentCard/Pilot");
        if (pilotTransform == null)
        {
            return;
        }

        MeshRenderer meshRenderer = pilotTransform.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        BoxCollider boxCollider = pilotTransform.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (pilotTransform.GetComponent<CanvasRenderer>() == null)
        {
            pilotTransform.gameObject.AddComponent<CanvasRenderer>();
        }

        if (runtimePilotSprite == null || runtimePilotSprite.texture != pilotTexture)
        {
            if (runtimePilotSprite != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimePilotSprite);
                }
                else
                {
                    DestroyImmediate(runtimePilotSprite);
                }
            }

            runtimePilotSprite = Sprite.Create(
                pilotTexture,
                new Rect(0f, 0f, pilotTexture.width, pilotTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        Image pilotImage = pilotTransform.GetComponent<Image>() ?? pilotTransform.gameObject.AddComponent<Image>();
        pilotImage.sprite = runtimePilotSprite;
        pilotImage.color = Color.white;
        pilotImage.preserveAspect = true;
        pilotImage.raycastTarget = false;
    }

    private void CloseScene()
    {
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
