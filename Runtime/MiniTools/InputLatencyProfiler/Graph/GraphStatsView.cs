#if ENABLE_DEBUG
using TMPro;
using UnityEngine;

public interface IGraphStatisticsSource
{
    float Average { get; }
    float Min { get; }
    float Max { get; }
}

public sealed class GraphStatsView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_AvgText;
    [SerializeField] private TMP_Text m_MaxText;

    private IGraphStatisticsSource _source;

    public void SetSource(IGraphStatisticsSource source)
    {
        _source = source;
        Refresh();
    }

    public void Refresh()
    {
        if (_source == null)
            return;

        m_AvgText.text = $"AVG : {_source.Average:F2} ms";
        m_MaxText.text = $"MAX : {_source.Max:F2} ms";
    }
}
#endif
