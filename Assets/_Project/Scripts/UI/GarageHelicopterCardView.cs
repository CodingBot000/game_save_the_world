using UnityEngine;
using UnityEngine.UI;

public class GarageHelicopterCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image frame;
    [SerializeField] private RawImage thumbnailImage;
    [SerializeField] private Text nameText;

    public Button Button => button;
    public Image Frame => frame;
    public RawImage ThumbnailImage => thumbnailImage;
    public Text NameText => nameText;

    public bool IsConfigured =>
        button != null &&
        frame != null &&
        thumbnailImage != null &&
        nameText != null;

#if UNITY_EDITOR
    public void SetReferences(Button newButton, Image newFrame, RawImage newThumbnailImage, Text newNameText)
    {
        button = newButton;
        frame = newFrame;
        thumbnailImage = newThumbnailImage;
        nameText = newNameText;
    }
#endif
}
