using UnityEngine;

public static class MissileStrikeDistribution
{
    private const float GoldenAngleRadians = 2.39996323f;

    public static int GetAnchorIndex(int missileIndex, int anchorCount, int salvoSequence)
    {
        if (anchorCount <= 0)
        {
            return -1;
        }

        int clampedIndex = Mathf.Max(0, missileIndex);
        int anchorOffset = GetAnchorOffset(anchorCount, salvoSequence);
        return (clampedIndex + anchorOffset) % anchorCount;
    }

    public static int GetAnchorOrdinal(int missileIndex, int anchorCount)
    {
        return anchorCount > 0 ? Mathf.Max(0, missileIndex) / anchorCount : 0;
    }

    public static int GetAnchorAssignmentCount(
        int anchorIndex,
        int totalMissileCount,
        int anchorCount,
        int salvoSequence)
    {
        if (anchorIndex < 0 || anchorIndex >= anchorCount || anchorCount <= 0 || totalMissileCount <= 0)
        {
            return 0;
        }

        int baseCount = totalMissileCount / anchorCount;
        int remainder = totalMissileCount % anchorCount;
        int anchorOffset = GetAnchorOffset(anchorCount, salvoSequence);
        int relativeIndex = PositiveModulo(anchorIndex - anchorOffset, anchorCount);
        return baseCount + (relativeIndex < remainder ? 1 : 0);
    }

    public static Vector3 GetLocalOffset(
        int missileIndex,
        int anchorIndex,
        int anchorOrdinal,
        int assignedMissileCount,
        int salvoSequence,
        float spreadRadius,
        float verticalScale,
        float depth)
    {
        int pointCount = Mathf.Max(1, assignedMissileCount);
        int pointOrdinal = Mathf.Clamp(anchorOrdinal, 0, pointCount - 1);
        float clampedRadius = Mathf.Max(0f, spreadRadius);
        float normalizedRadius = Mathf.Sqrt((pointOrdinal + 0.5f) / pointCount);
        float phase = Hash01(salvoSequence, anchorIndex, 0x45D9) * Mathf.PI * 2f;
        float angle = phase + pointOrdinal * GoldenAngleRadians;
        float radius = clampedRadius * normalizedRadius;
        float depthOffset = HashSigned(salvoSequence, missileIndex, 0x27D4) * Mathf.Abs(depth);

        return new Vector3(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius * Mathf.Max(0f, verticalScale),
            depthOffset);
    }

    public static float Hash01(int seed, int index, int salt)
    {
        uint hash = Hash(seed, index, salt);
        return (hash & 0x00FFFFFFu) / 16777216f;
    }

    public static float HashSigned(int seed, int index, int salt)
    {
        return Hash01(seed, index, salt) * 2f - 1f;
    }

    private static int GetAnchorOffset(int anchorCount, int salvoSequence)
    {
        return anchorCount > 0
            ? (int)(Hash(salvoSequence, anchorCount, 0x1656) % (uint)anchorCount)
            : 0;
    }

    private static uint Hash(int seed, int index, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed ^ 0x9E3779B9u;
            hash ^= (uint)index + 0x85EBCA6Bu + (hash << 6) + (hash >> 2);
            hash ^= (uint)salt + 0xC2B2AE35u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
