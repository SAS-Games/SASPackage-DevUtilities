using UnityEngine;

public sealed class InputLatencyGraphUI : MonoBehaviour
{
    [SerializeField] private InputLatencyProfiler m_Profiler;
   
    [SerializeField] private GraphGraphic m_RawGraph;
    [SerializeField] private GraphGraphic m_ActionGraph;
    
    [SerializeField] private GraphStatsView m_RawStatsView;
    [SerializeField] private GraphStatsView m_ActionStatsView;

    private void Start()
    {
        m_RawGraph.SetDataSource(new LatencyGraphSource(m_Profiler.RawStatistics, 20f));
        m_ActionGraph.SetDataSource(new LatencyGraphSource(m_Profiler.ActionStatistics, 50f));
        
        m_RawStatsView.SetSource(m_Profiler.RawStatistics);
        m_ActionStatsView.SetSource(m_Profiler.ActionStatistics);
    }

    private void Update()
    {
        m_RawGraph.SetVerticesDirty();
        m_ActionGraph.SetVerticesDirty();

        m_RawStatsView.Refresh();
        m_ActionStatsView.Refresh();
    }
}