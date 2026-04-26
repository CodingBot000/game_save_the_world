using UnityEngine;
using UnityEngine.UI;

public class GarageLoadoutSceneView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text selectedVehicleText;
    [SerializeField] private RawImage helicopterPreviewImage;
    [SerializeField] private Camera helicopterPreviewCamera;
    [SerializeField] private Transform helicopterPreviewAnchor;
    [SerializeField] private Light helicopterPreviewLight;
    [SerializeField] private GarageLoadoutItemView[] itemViews;

    public Canvas Canvas => canvas;
    public Image BackgroundImage => backgroundImage;
    public Button CloseButton => closeButton;
    public Text TitleText => titleText;
    public Text SelectedVehicleText => selectedVehicleText;
    public RawImage HelicopterPreviewImage => helicopterPreviewImage;
    public Camera HelicopterPreviewCamera => helicopterPreviewCamera;
    public Transform HelicopterPreviewAnchor => helicopterPreviewAnchor;
    public Light HelicopterPreviewLight => helicopterPreviewLight;
    public GarageLoadoutItemView[] ItemViews => itemViews;

    public bool IsConfigured =>
        canvas != null &&
        backgroundImage != null &&
        closeButton != null &&
        titleText != null &&
        selectedVehicleText != null &&
        helicopterPreviewImage != null &&
        helicopterPreviewCamera != null &&
        helicopterPreviewAnchor != null &&
        itemViews != null &&
        itemViews.Length > 0;

#if UNITY_EDITOR
    public void SetReferences(
        Canvas newCanvas,
        Image newBackgroundImage,
        Button newCloseButton,
        Text newTitleText,
        Text newSelectedVehicleText,
        RawImage newHelicopterPreviewImage,
        Camera newHelicopterPreviewCamera,
        Transform newHelicopterPreviewAnchor,
        Light newHelicopterPreviewLight,
        GarageLoadoutItemView[] newItemViews)
    {
        canvas = newCanvas;
        backgroundImage = newBackgroundImage;
        closeButton = newCloseButton;
        titleText = newTitleText;
        selectedVehicleText = newSelectedVehicleText;
        helicopterPreviewImage = newHelicopterPreviewImage;
        helicopterPreviewCamera = newHelicopterPreviewCamera;
        helicopterPreviewAnchor = newHelicopterPreviewAnchor;
        helicopterPreviewLight = newHelicopterPreviewLight;
        itemViews = newItemViews;
    }
#endif
}
