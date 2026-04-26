using UnityEngine;
using UnityEngine.UI;

public class GarageLoadoutItemView : MonoBehaviour
{
    [SerializeField] private GarageLoadoutSlotType slotType;
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Color previewColor = Color.white;
    [SerializeField] private Button button;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image previewBodyImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text equippedText;

    public GarageLoadoutSlotType SlotType => slotType;
    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Color PreviewColor => previewColor;
    public Button Button => button;

    public bool IsConfigured =>
        button != null &&
        frameImage != null &&
        previewBodyImage != null &&
        nameText != null &&
        equippedText != null &&
        !string.IsNullOrWhiteSpace(itemId);

    private void OnValidate()
    {
        ApplyStaticContent();
    }

    public void ApplyStaticContent()
    {
        if (nameText != null)
        {
            nameText.text = displayName;
        }

        if (previewBodyImage != null)
        {
            previewBodyImage.color = previewColor;
        }
    }

    public void SetSelected(bool selected)
    {
        if (frameImage != null)
        {
            frameImage.color = selected ? new Color(0.95f, 0.68f, 0.25f, 1f) : new Color(0.18f, 0.24f, 0.28f, 1f);
        }

        if (equippedText != null)
        {
            equippedText.text = selected ? "EQUIPPED" : string.Empty;
            equippedText.color = selected ? new Color(1f, 0.82f, 0.36f, 1f) : new Color(0.65f, 0.7f, 0.72f, 1f);
        }
    }

#if UNITY_EDITOR
    public void SetData(GarageLoadoutSlotType newSlotType, string newItemId, string newDisplayName, Color newPreviewColor)
    {
        slotType = newSlotType;
        itemId = newItemId;
        displayName = newDisplayName;
        previewColor = newPreviewColor;
        ApplyStaticContent();
    }

    public void SetReferences(Button newButton, Image newFrameImage, Image newPreviewBodyImage, Text newNameText, Text newEquippedText)
    {
        button = newButton;
        frameImage = newFrameImage;
        previewBodyImage = newPreviewBodyImage;
        nameText = newNameText;
        equippedText = newEquippedText;
        ApplyStaticContent();
    }
#endif
}
