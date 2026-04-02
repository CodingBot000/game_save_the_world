using UnityEngine;
using UnityEngine.UI;

public class CharacterSceneView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text contentLabel;
    [SerializeField] private Renderer pilotRenderer;

    public Canvas Canvas => canvas;
    public Button CloseButton => closeButton;
    public Text TitleText => titleText;
    public Text DescriptionText => descriptionText;
    public Text ContentLabel => contentLabel;
    public Renderer PilotRenderer => ResolvePilotRenderer();

    public bool IsConfigured =>
        canvas != null &&
        closeButton != null &&
        titleText != null &&
        descriptionText != null &&
        contentLabel != null;

    private Renderer ResolvePilotRenderer()
    {
        if (pilotRenderer != null)
        {
            return pilotRenderer;
        }

        if (canvas == null)
        {
            return null;
        }

        Transform pilotTransform = canvas.transform.Find("CharacterRoot/Panel/ContentCard/Pilot");
        if (pilotTransform == null)
        {
            return null;
        }

        pilotRenderer = pilotTransform.GetComponent<Renderer>();
        return pilotRenderer;
    }

#if UNITY_EDITOR
    public void SetReferences(
        Canvas newCanvas,
        Button newCloseButton,
        Text newTitleText,
        Text newDescriptionText,
        Text newContentLabel,
        Renderer newPilotRenderer = null)
    {
        canvas = newCanvas;
        closeButton = newCloseButton;
        titleText = newTitleText;
        descriptionText = newDescriptionText;
        contentLabel = newContentLabel;
        pilotRenderer = newPilotRenderer;
    }
#endif
}
