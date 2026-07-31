using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossWeakPointDebugFlash : MonoBehaviour
{
    private static readonly int ToonEmissionColorId = Shader.PropertyToID("_Emissive_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private sealed class RendererState
    {
        public Renderer Renderer;
        public MaterialPropertyBlock OriginalBlock;
        public MaterialPropertyBlock FlashBlock;
        public bool OriginalBlockWasEmpty;
    }

    [SerializeField] private Color flashColor = new(1f, 0.8f, 0.08f, 1f);
    [SerializeField, Min(0.05f)] private float flashInterval = 0.18f;

    private readonly List<RendererState> rendererStates = new();
    private BossTestState bossTestState;
    private Transform visualRoot;
    private Coroutine flashRoutine;
    private bool subscribed;

    public int FlashRendererCount => rendererStates.Count;
    public bool IsFlashing => flashRoutine != null;

    public void Configure(BossTestState testState, Transform root)
    {
        StopFlashingAndRestore();
        Unsubscribe();
        bossTestState = testState;
        visualRoot = root;
        Subscribe();
        HandleWeakPointStateChanged(bossTestState != null && bossTestState.IsWeakPointOpen);
    }

    private void CacheRendererStates()
    {
        rendererStates.Clear();
        if (visualRoot == null)
        {
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
            {
                continue;
            }

            MaterialPropertyBlock originalBlock = new();
            MaterialPropertyBlock flashBlock = new();
            renderer.GetPropertyBlock(originalBlock);
            renderer.GetPropertyBlock(flashBlock);
            Color emissionColor = flashColor * 2f;
            emissionColor.a = flashColor.a;
            if (HasMaterialProperty(renderer, ToonEmissionColorId))
            {
                flashBlock.SetColor(ToonEmissionColorId, emissionColor);
            }
            else if (HasMaterialProperty(renderer, EmissionColorId))
            {
                flashBlock.SetColor(EmissionColorId, emissionColor);
            }
            else
            {
                flashBlock.SetColor(BaseColorId, flashColor);
                flashBlock.SetColor(ColorId, flashColor);
            }

            rendererStates.Add(new RendererState
            {
                Renderer = renderer,
                OriginalBlock = originalBlock,
                FlashBlock = flashBlock,
                OriginalBlockWasEmpty = originalBlock.isEmpty,
            });
        }
    }

    private void HandleWeakPointStateChanged(bool open)
    {
        if (!open || !isActiveAndEnabled)
        {
            StopFlashingAndRestore();
            return;
        }

        if (flashRoutine == null)
        {
            CacheRendererStates();
            if (rendererStates.Count == 0)
            {
                return;
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        bool highlighted = true;
        while (bossTestState != null && bossTestState.IsWeakPointOpen)
        {
            ApplyFlash(highlighted);
            highlighted = !highlighted;
            yield return new WaitForSeconds(Mathf.Max(0.05f, flashInterval));
        }

        RestoreOriginalBlocks();
        rendererStates.Clear();
        flashRoutine = null;
    }

    private void ApplyFlash(bool highlighted)
    {
        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.SetPropertyBlock(
                    highlighted ? state.FlashBlock : state.OriginalBlock);
            }
        }
    }

    private void StopFlashingAndRestore()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        RestoreOriginalBlocks();
        rendererStates.Clear();
    }

    private void RestoreOriginalBlocks()
    {
        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            if (state.Renderer != null)
            {
                if (state.OriginalBlockWasEmpty)
                {
                    state.Renderer.SetPropertyBlock(null);
                }
                else
                {
                    state.Renderer.SetPropertyBlock(state.OriginalBlock);
                }
            }
        }
    }

    private static bool HasMaterialProperty(Renderer renderer, int propertyId)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].HasProperty(propertyId))
            {
                return true;
            }
        }

        return false;
    }

    private void Subscribe()
    {
        if (subscribed || bossTestState == null)
        {
            return;
        }

        bossTestState.OnWeakPointStateChanged += HandleWeakPointStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && bossTestState != null)
        {
            bossTestState.OnWeakPointStateChanged -= HandleWeakPointStateChanged;
        }

        subscribed = false;
    }

    private void OnEnable()
    {
        Subscribe();
        HandleWeakPointStateChanged(bossTestState != null && bossTestState.IsWeakPointOpen);
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopFlashingAndRestore();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        StopFlashingAndRestore();
    }

    private void OnValidate()
    {
        flashInterval = Mathf.Max(0.05f, flashInterval);
    }
}
