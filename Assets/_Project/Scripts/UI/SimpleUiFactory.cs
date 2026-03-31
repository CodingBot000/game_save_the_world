using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class SimpleUiFactory
{
    public static void StretchFull(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    public static void SetAnchoredLayout(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = FindOrCreateUiObject(name, parent);
        Image image = imageObject.GetComponent<Image>() ?? imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Color color,
        UnityAction onClick,
        int fontSize = 22)
    {
        GameObject buttonObject = FindOrCreateUiObject(name, parent);
        Image image = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        Text labelText = CreateText($"{name}Label", buttonObject.transform, font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        labelText.text = label;
        StretchFull(labelText.rectTransform);

        return button;
    }

    public static Text CreateText(
        string name,
        Transform parent,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
    {
        GameObject textObject = FindOrCreateUiObject(name, parent);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static GameObject FindOrCreateUiObject(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform)
            {
                return existing.gameObject;
            }

            Object.Destroy(existing.gameObject);
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
