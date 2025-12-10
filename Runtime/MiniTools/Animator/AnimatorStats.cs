using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AnimatorStats: UIBehaviour
{
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 1f;

    private readonly StringBuilder sb = new StringBuilder(256);
    private Animator[] _animators;


    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshList();
    }

    private void RefreshList()
    {
        _animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        int activeAlways = 0;
        int activeCullUpdate = 0;
        int activeCull = 0;

        int disabledAlways = 0;
        int disabledCullUpdate = 0;
        int disabledCull = 0;

        foreach (var a in _animators)
        {
            if (!a) continue;

            bool isActive = (a.enabled && a.gameObject.activeInHierarchy);

            switch (a.cullingMode)
            {
                case AnimatorCullingMode.AlwaysAnimate:
                    if (isActive) activeAlways++;
                    else disabledAlways++;
                    break;

                case AnimatorCullingMode.CullUpdateTransforms:
                    if (isActive) activeCullUpdate++;
                    else disabledCullUpdate++;
                    break;

                case AnimatorCullingMode.CullCompletely:
                    if (isActive) activeCull++;
                    else disabledCull++;
                    break;
            }
        }

        sb.Length = 0;
        sb.AppendLine("<color=#00FFFF><b>ANIMATORS</b></color>");
      
        sb.AppendLine("<color=#00FF00>Active:</color>");
        sb.AppendFormat("  Always: {0}\n", activeAlways);
        sb.AppendFormat("  CullUpdate: {0}\n", activeCullUpdate);
        sb.AppendFormat("  Cull: {0}\n", activeCull);

        sb.AppendLine("<color=#FF4444>Disabled:</color>");
        sb.AppendFormat("  Always: {0}\n", disabledAlways);
        sb.AppendFormat("  CullUpdate: {0}\n", disabledCullUpdate);
        sb.AppendFormat("  Cull: {0}\n", disabledCull);

        m_Display.text = sb.ToString();
    }
}
