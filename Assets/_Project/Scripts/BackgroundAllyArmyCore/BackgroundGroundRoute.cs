using System;
using UnityEngine;

/// <summary>
/// Closed, distance-sampled Catmull-Rom route in StageVisualRoot local space.
/// The sample table is built once; runtime sampling is allocation free.
/// </summary>
public sealed class BackgroundGroundRoute
{
    private readonly Vector3[] samples;
    private readonly float[] cumulativeDistances;

    public int SampleCount => samples.Length;
    public float TotalLength { get; }

    public BackgroundGroundRoute(Vector3[] controlPoints, int samplesPerSegment = 12)
    {
        if (controlPoints == null || controlPoints.Length < 4)
        {
            throw new ArgumentException("A closed ground route requires at least four control points.", nameof(controlPoints));
        }

        int segmentSamples = Mathf.Max(2, samplesPerSegment);
        int count = controlPoints.Length * segmentSamples;
        samples = new Vector3[count];
        cumulativeDistances = new float[count + 1];

        for (int segment = 0; segment < controlPoints.Length; segment++)
        {
            Vector3 p0 = controlPoints[WrapIndex(segment - 1, controlPoints.Length)];
            Vector3 p1 = controlPoints[segment];
            Vector3 p2 = controlPoints[(segment + 1) % controlPoints.Length];
            Vector3 p3 = controlPoints[(segment + 2) % controlPoints.Length];
            for (int sample = 0; sample < segmentSamples; sample++)
            {
                float t = sample / (float)segmentSamples;
                samples[segment * segmentSamples + sample] = EvaluateCatmullRom(p0, p1, p2, p3, t);
            }
        }

        float length = 0f;
        cumulativeDistances[0] = 0f;
        for (int i = 0; i < count; i++)
        {
            length += Vector3.Distance(samples[i], samples[(i + 1) % count]);
            cumulativeDistances[i + 1] = length;
        }

        TotalLength = Mathf.Max(0.0001f, length);
    }

    public void Sample(float distance, out Vector3 localPosition, out Vector3 localTangent)
    {
        float wrapped = WrapDistance(distance, TotalLength);
        int low = 0;
        int high = samples.Length;
        while (low + 1 < high)
        {
            int mid = (low + high) >> 1;
            if (cumulativeDistances[mid] <= wrapped)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        int next = (low + 1) % samples.Length;
        float segmentStart = cumulativeDistances[low];
        float segmentEnd = cumulativeDistances[low + 1];
        float segmentLength = Mathf.Max(0.0001f, segmentEnd - segmentStart);
        float t = Mathf.Clamp01((wrapped - segmentStart) / segmentLength);
        localPosition = Vector3.LerpUnclamped(samples[low], samples[next], t);
        Vector3 tangent = samples[next] - samples[low];
        localTangent = tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.forward;
    }

    public static float WrapDistance(float distance, float totalLength)
    {
        if (totalLength <= 0f)
        {
            return 0f;
        }

        distance %= totalLength;
        return distance < 0f ? distance + totalLength : distance;
    }

    public static Vector3 EvaluateCatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float clamped = Mathf.Clamp01(t);
        float t2 = clamped * clamped;
        float t3 = t2 * clamped;
        return 0.5f * ((2f * p1)
                       + (-p0 + p2) * clamped
                       + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                       + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static int WrapIndex(int index, int count)
    {
        index %= count;
        return index < 0 ? index + count : index;
    }
}
