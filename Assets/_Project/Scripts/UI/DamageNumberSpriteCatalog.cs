using UnityEngine;

[CreateAssetMenu(menuName = "Titan Destroyer/UI/Damage Number Sprite Catalog")]
public class DamageNumberSpriteCatalog : ScriptableObject
{
    [SerializeField] private Sprite[] normalDigits = new Sprite[10];
    [SerializeField] private Sprite[] criticalDigits = new Sprite[10];

    public Sprite GetDigitSprite(int digit, bool critical)
    {
        int index = Mathf.Clamp(digit, 0, 9);
        Sprite[] source = critical ? criticalDigits : normalDigits;
        if (source == null || index >= source.Length || source[index] == null)
        {
            source = normalDigits;
        }

        return source != null && index < source.Length ? source[index] : null;
    }
}
