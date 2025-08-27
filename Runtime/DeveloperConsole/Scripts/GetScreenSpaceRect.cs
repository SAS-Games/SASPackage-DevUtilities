using UnityEngine;

public static class RectTransformExtensions
{
    /// <summary>
    /// Returns the screen-space rectangle of a RectTransform.
    /// </summary>
    public static Rect GetScreenSpaceRect(this RectTransform rt, Camera cam = null)
    {
        Vector3[] worldCorners = new Vector3[4];
        rt.GetWorldCorners(worldCorners);

        // If no camera is provided and canvas is Overlay, no conversion needed
        if (cam == null)
        {
            cam = Camera.main;
        }

        Vector3 bl = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[0]); // bottom-left
        Vector3 tr = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[2]); // top-right

        return new Rect(bl, tr - bl);
    }
    public static Bounds GetWorldBounds(this RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        var bounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++)
            bounds.Encapsulate(corners[i]);

        return bounds;
    }

    /// <summary>
    /// Returns the Bounds of a RectTransform relative to a root transform.
    /// </summary>
    public static Bounds GetRelativeBounds(this RectTransform rt,Transform root)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        for (int i = 0; i < 4; i++)
            corners[i] = root.InverseTransformPoint(corners[i]);

        var bounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++)
            bounds.Encapsulate(corners[i]);

        return bounds;
    }
}