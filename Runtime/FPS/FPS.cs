using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FPS : UIBehaviour
{
    [SerializeField] private Text m_Display = default;
    [SerializeField] private float m_UpdateInterval = 0.5f;
    [SerializeField] private int m_TargetFrameRate = 60;

    private float mAccumulatedFPS = 0;
    private int mFrames = 0;
    private float mTimeLeft;
    private float mAvarageFPS = 0;

    protected override void Start()
    {
        mTimeLeft = m_UpdateInterval;
        Application.targetFrameRate = m_TargetFrameRate;
    }

    private void Update()
    {
        mTimeLeft -= Time.deltaTime;
        mAccumulatedFPS += Time.timeScale / Time.deltaTime;
        ++mFrames;

        if(mTimeLeft <= 0)
        {
            mAvarageFPS = mAccumulatedFPS / mFrames;
            m_Display.text = System.String.Format("{0:F1} FPS", mAvarageFPS);
            m_Display.color = mAvarageFPS < 30 ? mAvarageFPS < 10 ? Color.red : Color.yellow : Color.green;

            mAccumulatedFPS = 0;
            mFrames = 0;
            mAvarageFPS = 0;
            mTimeLeft = m_UpdateInterval;
        }
    }
}
