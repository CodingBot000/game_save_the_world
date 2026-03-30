using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        GameFlowController.LoadIntro();
    }
}
