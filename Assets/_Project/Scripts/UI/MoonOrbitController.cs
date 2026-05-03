using UnityEngine;

public class MoonOrbitController : MonoBehaviour
{
    // Toggle this to stop StageVisualRoot/map orbit again while keeping cloud motion independent.
    [SerializeField] private bool temporarilyDisableOrbit;

    [SerializeField] private Vector3 orbitAxis = new Vector3(0f, 1f, 0f);
    [SerializeField] private float orbitSpeed = 4f;

    private void Update()
    {
        if (temporarilyDisableOrbit)
        {
            return;
        }

        transform.Rotate(orbitAxis.normalized, orbitSpeed * Time.unscaledDeltaTime, Space.Self);
    }
}
