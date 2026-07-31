using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossPhaseDebugHud : MonoBehaviour
{
    private const string PhaseTextName = "BossPhaseDebugText";

    private BossTestState bossTestState;
    private Canvas battleCanvas;
    private Text phaseText;
    private bool ownsPhaseText;
    private bool subscribed;

    public Text PhaseText => phaseText;

    public void Configure(BossTestState testState, Canvas canvas)
    {
        Unsubscribe();
        bossTestState = testState;
        battleCanvas = canvas;
        EnsurePhaseText();
        Subscribe();
        UpdatePhaseText(bossTestState != null ? bossTestState.CurrentPhase : 1);
    }

    private void EnsurePhaseText()
    {
        if (phaseText != null || battleCanvas == null)
        {
            return;
        }

        Transform generatedHud = battleCanvas.transform.Find("GeneratedHUD");
        Transform bossBar = generatedHud != null
            ? generatedHud.Find("BossBarBackground")
            : null;
        Transform parent = bossBar ?? generatedHud ?? battleCanvas.transform;
        bool anchoredInsideBossBar = bossBar != null;
        Transform existing = parent.Find(PhaseTextName);
        if (existing != null)
        {
            phaseText = existing.GetComponent<Text>();
        }

        if (phaseText == null)
        {
            GameObject textObject = new(PhaseTextName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            phaseText = textObject.AddComponent<Text>();
            ownsPhaseText = true;
        }

        phaseText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phaseText.fontSize = anchoredInsideBossBar ? 20 : 24;
        phaseText.fontStyle = FontStyle.Bold;
        phaseText.alignment = TextAnchor.MiddleCenter;
        phaseText.color = new Color(1f, 0.86f, 0.24f, 1f);
        phaseText.raycastTarget = false;
        phaseText.horizontalOverflow = HorizontalWrapMode.Overflow;
        phaseText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = phaseText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = anchoredInsideBossBar
            ? new Vector2(280f, 24f)
            : new Vector2(360f, 32f);
        rect.anchoredPosition = anchoredInsideBossBar
            ? new Vector2(0f, -1f)
            : new Vector2(0f, -104f);

        Outline outline = phaseText.GetComponent<Outline>() ?? phaseText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        phaseText.transform.SetAsLastSibling();
        phaseText.gameObject.SetActive(isActiveAndEnabled);
    }

    private void UpdatePhaseText(int phase)
    {
        if (phaseText == null)
        {
            EnsurePhaseText();
        }

        if (phaseText != null)
        {
            phaseText.text = $"PHASE {Mathf.Max(1, phase)}";
        }
    }

    private void Subscribe()
    {
        if (subscribed || bossTestState == null)
        {
            return;
        }

        bossTestState.OnBossPhaseChanged += UpdatePhaseText;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && bossTestState != null)
        {
            bossTestState.OnBossPhaseChanged -= UpdatePhaseText;
        }

        subscribed = false;
    }

    private void OnEnable()
    {
        Subscribe();
        if (phaseText != null)
        {
            phaseText.gameObject.SetActive(true);
        }

        UpdatePhaseText(bossTestState != null ? bossTestState.CurrentPhase : 1);
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (phaseText != null)
        {
            phaseText.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (ownsPhaseText && phaseText != null)
        {
            Destroy(phaseText.gameObject);
        }
    }
}
