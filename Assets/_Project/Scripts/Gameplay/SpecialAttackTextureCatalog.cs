using UnityEngine;

[CreateAssetMenu(menuName = "Titan Destroyer/Battle/Special Attack Texture Catalog")]
public class SpecialAttackTextureCatalog : ScriptableObject
{
    [SerializeField] private Texture2D sceneTopTexture;
    [SerializeField] private Texture2D sceneBottomTexture;

    public Texture2D SceneTopTexture => sceneTopTexture;
    public Texture2D SceneBottomTexture => sceneBottomTexture;
}
