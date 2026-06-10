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
        for (int i = 0; i <= FillSegmentCount; i++)
        {
            float y = i / (float)FillSegmentCount;
            AddVertex(vertexHelper, rect, 0f, y, 0f, y);
            AddVertex(vertexHelper, rect, y, y, 1f, y);
        }

        AddCoverFillTriangles(vertexHelper);
    }

    private void AddLowerCoverFill(VertexHelper vertexHelper, Rect rect)
    {
        for (int i = 0; i <= FillSegmentCount; i++)
        {
            float y = i / (float)FillSegmentCount;
            AddVertex(vertexHelper, rect, y, y, 0f, y);
            AddVertex(vertexHelper, rect, 1f, y, 1f, y);
        }

        AddCoverFillTriangles(vertexHelper);
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

    private void AddVertex(VertexHelper vertexHelper, Rect rect, float normalizedX, float normalizedY, float u, float v)
    {
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
