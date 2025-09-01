using UnityEngine;

public class DebugOnly : MonoBehaviour
{
    private void Awake()
    {
#if ENABLE_DEBUG
    Destroy(gameObject);
#endif
    }
}