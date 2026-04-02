#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterInventoryPrototypeBuilder
{
    private const string CharacterScenePath = "Assets/Scenes/CharacterScene.unity";

    [MenuItem("Tools/Titan Destroyer/Build Character Equipment Prototype")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(CharacterScenePath, OpenSceneMode.Single);
        Transform contentCard = FindRequired(scene, "CharacterCanvas/CharacterRoot/Panel/ContentCard");
        if (contentCard == null)
        {
            Debug.LogError("Character ContentCard was not found.");
            return;
        }

        DestroyChildIfExists(contentCard, "EquipmentSlotsPanel");
        DestroyChildIfExists(contentCard, "InventoryGridPanel");

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform equipmentPanel = CreateUiObject(contentCard, "EquipmentSlotsPanel").GetComponent<RectTransform>();
        SetAnchoredLayout(
            equipmentPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(116f, 548f),
            new Vector2(-165f, 74f));

        VerticalLayoutGroup verticalLayout = equipmentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = TextAnchor.UpperCenter;
        verticalLayout.childControlWidth = false;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandWidth = false;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.spacing = 12f;
        verticalLayout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter equipmentFitter = equipmentPanel.gameObject.AddComponent<ContentSizeFitter>();
        equipmentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        equipmentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateEquipmentSlot(equipmentPanel, font, "Helmet");
        CreateEquipmentSlot(equipmentPanel, font, "Top");
        CreateEquipmentSlot(equipmentPanel, font, "Bottom");
        CreateEquipmentSlot(equipmentPanel, font, "Boots");
        CreateEquipmentSlot(equipmentPanel, font, "Pistol");

        RectTransform inventoryPanel = CreateUiObject(contentCard, "InventoryGridPanel").GetComponent<RectTransform>();
        AddImage(inventoryPanel.gameObject, new Color(0.08f, 0.12f, 0.17f, 0.88f));
        SetAnchoredLayout(
            inventoryPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(780f, 476f),
            new Vector2(356f, 46f));

        RectTransform inventoryGrid = CreateUiObject(inventoryPanel, "InventoryGrid").GetComponent<RectTransform>();
        StretchFull(inventoryGrid);
        inventoryGrid.offsetMin = new Vector2(18f, 18f);
        inventoryGrid.offsetMax = new Vector2(-18f, -18f);

        GridLayoutGroup gridLayout = inventoryGrid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(62f, 62f);
        gridLayout.spacing = new Vector2(8f, 8f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 10;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.padding = new RectOffset(4, 4, 4, 4);

        for (int i = 0; i < 50; i++)
        {
            RectTransform slot = CreateUiObject(inventoryGrid, $"ItemSlot{i + 1:00}").GetComponent<RectTransform>();
            Image slotImage = AddImage(slot.gameObject, i % 2 == 0
                ? new Color(0.16f, 0.22f, 0.3f, 1f)
                : new Color(0.13f, 0.18f, 0.25f, 1f));

            LayoutElement layoutElement = slot.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 62f;
            layoutElement.preferredHeight = 62f;
            layoutElement.minWidth = 62f;
            layoutElement.minHeight = 62f;

            Text slotLabel = CreateText(slot, "Label", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.75f, 0.84f, 0.93f), $"{i + 1}");
            StretchFull(slotLabel.rectTransform);
            slotLabel.raycastTarget = false;
            slotImage.raycastTarget = true;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void CreateEquipmentSlot(Transform parent, Font font, string label)
    {
        RectTransform slot = CreateUiObject(parent, $"{label}Slot").GetComponent<RectTransform>();
        slot.sizeDelta = new Vector2(116f, 100f);

        Image background = AddImage(slot.gameObject, new Color(0.14f, 0.19f, 0.27f, 1f));
        background.raycastTarget = true;

        Outline outline = slot.gameObject.GetComponent<Outline>() ?? slot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.44f, 0.68f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);

        Text text = CreateText(slot, "Label", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, label);
        StretchFull(text.rectTransform);
        text.raycastTarget = false;
    }

    private static Transform FindRequired(Scene scene, string path)
    {
        string[] segments = path.Split('/');
        Transform current = null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == segments[0])
            {
                current = roots[i].transform;
                break;
            }
        }

        if (current == null)
        {
            return null;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            current = current.Find(segments[i]);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image AddImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, Font font, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, string textValue)
    {
        GameObject textObject = CreateUiObject(parent, name);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.text = textValue;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredLayout(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }
}
#endif
