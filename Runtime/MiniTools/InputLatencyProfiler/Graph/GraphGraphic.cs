using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IGraphDataSource
{
    int Count { get; }

    float GetValue(int index);

    // Fixed display range for graph scaling
    float MinValue { get; }

    float MaxValue { get; }
}

public class GraphGraphic : MaskableGraphic
{
    [Header("Graph")]
    [SerializeField]
    private float m_LineThickness = 2f;

    [SerializeField]
    private int m_MaxVisiblePoints = 100;

    [SerializeField]
    private Vector2 m_Padding =
        new Vector2(10f, 10f);

    [SerializeField]
    private bool m_DrawNewestRight = true;

    [Header("Labels")]
    [SerializeField]
    private TMP_Text m_MinLabel;

    [SerializeField]
    private TMP_Text m_MaxLabel;

    [SerializeField]
    private string m_LabelSuffix = " ms";

    private IGraphDataSource _dataSource;

    public Rect PlotRect => GetPlotRect();

    public void SetDataSource(
        IGraphDataSource dataSource)
    {
        _dataSource = dataSource;

        Refresh();
    }

    public void Refresh()
    {
        RefreshLabels();

        SetVerticesDirty();
    }

    private void RefreshLabels()
    {
        if (_dataSource == null)
            return;

        if (m_MaxLabel != null)
        {
            m_MaxLabel.text =
                $"{_dataSource.MaxValue:F0}" +
                $"{m_LabelSuffix}";
        }

        if (m_MinLabel != null)
        {
            m_MinLabel.text =
                $"{_dataSource.MinValue:F0}" +
                $"{m_LabelSuffix}";
        }
    }

    private Rect GetPlotRect()
    {
        Rect rect = GetPixelAdjustedRect();

        return new Rect(
            rect.xMin + m_Padding.x,
            rect.yMin + m_Padding.y,
            rect.width - (m_Padding.x * 2f),
            rect.height - (m_Padding.y * 2f));
    }

    protected override void OnPopulateMesh(
        VertexHelper vh)
    {
        vh.Clear();

        if (_dataSource == null)
            return;

        int pointCount =
            Mathf.Min(
                _dataSource.Count,
                m_MaxVisiblePoints);

        if (pointCount < 2)
            return;

        Rect plotRect = GetPlotRect();

        float width = plotRect.width;

        float height = plotRect.height;

        float min =
            _dataSource.MinValue;

        float max =
            _dataSource.MaxValue;

        float xStep =
            width / (pointCount - 1);

        for (int i = 0; i < pointCount - 1; i++)
        {
            int indexA =
                m_DrawNewestRight
                    ? (pointCount - 1 - i)
                    : i;

            int indexB =
                m_DrawNewestRight
                    ? (pointCount - 2 - i)
                    : (i + 1);

            float valueA =
                _dataSource.GetValue(indexA);

            float valueB =
                _dataSource.GetValue(indexB);

            float normalizedA =
                Mathf.InverseLerp(
                    min,
                    max,
                    valueA);

            float normalizedB =
                Mathf.InverseLerp(
                    min,
                    max,
                    valueB);

            Vector2 p1 =
                new Vector2(
                    plotRect.x +
                    (i * xStep),

                    plotRect.y +
                    (normalizedA * height));

            Vector2 p2 =
                new Vector2(
                    plotRect.x +
                    ((i + 1) * xStep),

                    plotRect.y +
                    (normalizedB * height));

            AddLine(
                vh,
                p1,
                p2,
                m_LineThickness,
                color);
        }

        DrawPlotBounds(
            vh,
            plotRect,
            1f,
            Color.gray);
    }

    private static void AddLine(
        VertexHelper vh,
        Vector2 start,
        Vector2 end,
        float thickness,
        Color color)
    {
        Vector2 direction =
            (end - start).normalized;

        Vector2 normal =
            new Vector2(
                -direction.y,
                direction.x);

        normal *= thickness * 0.5f;

        int index =
            vh.currentVertCount;

        vh.AddVert(
            start - normal,
            color,
            Vector2.zero);

        vh.AddVert(
            start + normal,
            color,
            Vector2.zero);

        vh.AddVert(
            end + normal,
            color,
            Vector2.zero);

        vh.AddVert(
            end - normal,
            color,
            Vector2.zero);

        vh.AddTriangle(
            index + 0,
            index + 1,
            index + 2);

        vh.AddTriangle(
            index + 2,
            index + 3,
            index + 0);
    }

    private static void DrawPlotBounds(
        VertexHelper vh,
        Rect rect,
        float thickness,
        Color color)
    {
        Vector2 topLeft =
            new Vector2(
                rect.xMin,
                rect.yMax);

        Vector2 topRight =
            new Vector2(
                rect.xMax,
                rect.yMax);

        Vector2 bottomLeft =
            new Vector2(
                rect.xMin,
                rect.yMin);

        Vector2 bottomRight =
            new Vector2(
                rect.xMax,
                rect.yMin);

        AddLine(
            vh,
            topLeft,
            topRight,
            thickness,
            color);

        AddLine(
            vh,
            topRight,
            bottomRight,
            thickness,
            color);

        AddLine(
            vh,
            bottomRight,
            bottomLeft,
            thickness,
            color);

        AddLine(
            vh,
            bottomLeft,
            topLeft,
            thickness,
            color);
    }
}