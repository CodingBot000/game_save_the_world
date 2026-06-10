using UnityEngine;
using UnityEngine.UI;

public class SpecialAttackDiagonalImage : RawImage
{
    private const int FillSegmentCount = 48;

    public enum DiagonalHalf
    {
        Upper,
        Lower
    }

    [SerializeField] private DiagonalHalf half = DiagonalHalf.Upper;

    public void Configure(Texture sourceTexture, DiagonalHalf targetHalf)
    {
        texture = sourceTexture;
        half = targetHalf;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (half == DiagonalHalf.Upper)
        {
            AddUpperCoverFill(vertexHelper, rect);
        }
        else
        {
            AddLowerCoverFill(vertexHelper, rect);
        }
    }

    private void AddUpperCoverFill(VertexHelper vertexHelper, Rect rect)
    {
        Vector2 imageCenter = new(1f / 3f, 2f / 3f);
        Vector2 imageSize = ResolveCenteredImageSize(rect, imageCenter);
        for (int i = 0; i <= FillSegmentCount; i++)
        {
            float y = i / (float)FillSegmentCount;
            AddVertex(vertexHelper, rect, imageCenter, imageSize, 0f, y);
            AddVertex(vertexHelper, rect, imageCenter, imageSize, y, y);
        }

        AddCoverFillTriangles(vertexHelper);
    }

    private void AddLowerCoverFill(VertexHelper vertexHelper, Rect rect)
    {
        Vector2 imageCenter = new(2f / 3f, 1f / 3f);
        Vector2 imageSize = ResolveCenteredImageSize(rect, imageCenter);
        for (int i = 0; i <= FillSegmentCount; i++)
        {
            float y = i / (float)FillSegmentCount;
            AddVertex(vertexHelper, rect, imageCenter, imageSize, y, y);
            AddVertex(vertexHelper, rect, imageCenter, imageSize, 1f, y);
        }

        AddCoverFillTriangles(vertexHelper);
    }

    private Vector2 ResolveCenteredImageSize(Rect rect, Vector2 imageCenter)
    {
        float textureAspect = texture != null && texture.height > 0
            ? texture.width / (float)texture.height
            : 1f;
        float rectAspect = rect.height > 0.01f ? rect.width / rect.height : textureAspect;
        float normalizedAspect = textureAspect / Mathf.Max(0.01f, rectAspect);

        float requiredWidth = Mathf.Max(imageCenter.x, 1f - imageCenter.x) * 2f;
        float requiredHeight = Mathf.Max(imageCenter.y, 1f - imageCenter.y) * 2f;
        if (requiredWidth / requiredHeight < normalizedAspect)
        {
            requiredWidth = requiredHeight * normalizedAspect;
        }
        else
        {
            requiredHeight = requiredWidth / normalizedAspect;
        }

        return new Vector2(requiredWidth, requiredHeight);
    }

    private static void AddCoverFillTriangles(VertexHelper vertexHelper)
    {
        for (int i = 0; i < FillSegmentCount; i++)
        {
            int currentLeft = i * 2;
            int currentRight = currentLeft + 1;
            int nextLeft = currentLeft + 2;
            int nextRight = currentLeft + 3;

            vertexHelper.AddTriangle(currentLeft, nextLeft, nextRight);
            vertexHelper.AddTriangle(currentLeft, nextRight, currentRight);
        }
    }

    private void AddVertex(
        VertexHelper vertexHelper,
        Rect rect,
        Vector2 imageCenter,
        Vector2 imageSize,
        float normalizedX,
        float normalizedY)
    {
        float u = (normalizedX - imageCenter.x) / imageSize.x + 0.5f;
        float v = (normalizedY - imageCenter.y) / imageSize.y + 0.5f;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector3(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY),
            0f);
        vertex.uv0 = new Vector2(u, v);
        vertexHelper.AddVert(vertex);
    }
}
