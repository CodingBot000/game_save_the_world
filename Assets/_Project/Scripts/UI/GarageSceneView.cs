using UnityEngine;
using UnityEngine.UI;

public class GarageSceneView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text selectedHelicopterText;
    [SerializeField] private RectTransform previewRect;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private ScrollRect helicopterScrollRect;
    [SerializeField] private RectTransform helicopterContent;
    [SerializeField] private GarageHelicopterCardView helicopterCardTemplate;

    public Canvas Canvas => canvas;
    public Button CloseButton => closeButton;
    public Text TitleText => titleText;
    public Text SelectedHelicopterText => selectedHelicopterText;
    public RectTransform PreviewRect => previewRect;
    public RawImage PreviewImage => previewImage;
    public ScrollRect HelicopterScrollRect => helicopterScrollRect;
    public RectTransform HelicopterContent => helicopterContent;
    public GarageHelicopterCardView HelicopterCardTemplate => helicopterCardTemplate;

    public bool IsConfigured =>
        canvas != null &&
        closeButton != null &&
        titleText != null &&
        selectedHelicopterText != null &&
        previewRect != null &&
        previewImage != null &&
        helicopterScrollRect != null &&
        helicopterContent != null &&
        helicopterCardTemplate != null &&
        helicopterCardTemplate.IsConfigured;

#if UNITY_EDITOR
    public void SetReferences(
        Canvas newCanvas,
        Button newCloseButton,
        Text newTitleText,
        Text newSelectedHelicopterText,
        RectTransform newPreviewRect,
        RawImage newPreviewImage,
        ScrollRect newHelicopterScrollRect,
        RectTransform newHelicopterContent,
        GarageHelicopterCardView newHelicopterCardTemplate)
    {
        canvas = newCanvas;
        closeButton = newCloseButton;
        titleText = newTitleText;
        selectedHelicopterText = newSelectedHelicopterText;
        previewRect = newPreviewRect;
        previewImage = newPreviewImage;
        helicopterScrollRect = newHelicopterScrollRect;
        helicopterContent = newHelicopterContent;
        helicopterCardTemplate = newHelicopterCardTemplate;
    }
#endif
}
