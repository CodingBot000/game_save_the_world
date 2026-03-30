using UnityEngine;

public class MoonOrbitController : MonoBehaviour
{
    [SerializeField] private Vector3 orbitAxis = new Vector3(0f, 1f, 0f);
    [SerializeField] private float orbitSpeed = 14f;

    private void Update()
    {
        transform.Rotate(orbitAxis.normalized, orbitSpeed * Time.unscaledDeltaTime, Space.Self);
    }
}
