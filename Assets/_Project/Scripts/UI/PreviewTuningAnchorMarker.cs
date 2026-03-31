using UnityEngine;

[ExecuteAlways]
public class PreviewTuningAnchorMarker : MonoBehaviour
{
    private const string MarkerName = "PreviewMarkerMesh";

    [SerializeField] private Color markerColor = new Color(0.18f, 0.72f, 0.95f, 0.35f);
    [SerializeField] private Vector3 markerSize = new Vector3(1.8f, 1.2f, 1.8f);

    private Transform markerMesh;

    private void OnEnable()
    {
        EnsureMarker();
        RefreshMarker();
    }

    private void OnValidate()
    {
        EnsureMarker();
        RefreshMarker();
    }

    private void Reset()
    {
        EnsureMarker();
        RefreshMarker();
    }

    private void EnsureMarker()
    {
        markerMesh = transform.Find(MarkerName);
        if (markerMesh != null)
        {
            return;
        }

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        markerObject.name = MarkerName;
        markerObject.transform.SetParent(transform, false);

        Collider markerCollider = markerObject.GetComponent<Collider>();
        if (markerCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(markerCollider);
            }
            else
            {
                DestroyImmediate(markerCollider);
            }
        }

        markerMesh = markerObject.transform;
    }

    private void RefreshMarker()
    {
        if (markerMesh == null)
        {
            return;
        }

        markerMesh.localPosition = Vector3.zero;
        markerMesh.localRotation = Quaternion.identity;
        markerMesh.localScale = markerSize;

        MeshRenderer meshRenderer = markerMesh.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            return;
        }

        Material sharedMaterial = meshRenderer.sharedMaterial;
        if (sharedMaterial == null || sharedMaterial.shader == null || sharedMaterial.shader.name != "Universal Render Pipeline/Lit")
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                sharedMaterial = new Material(shader);
                sharedMaterial.name = "PreviewTuningMarkerMaterial";
                meshRenderer.sharedMaterial = sharedMaterial;
            }
        }

        if (sharedMaterial == null)
        {
            return;
        }

        if (sharedMaterial.HasProperty("_BaseColor"))
        {
            sharedMaterial.SetColor("_BaseColor", markerColor);
        }
        else if (sharedMaterial.HasProperty("_Color"))
        {
            sharedMaterial.SetColor("_Color", markerColor);
        }

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }
}
