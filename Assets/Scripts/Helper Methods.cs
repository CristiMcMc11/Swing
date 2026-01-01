using Unity.VisualScripting;
using UnityEngine;

public static class HelperMethods
{
    /// <summary>
    /// Sends a raycast at the desired position and direction.
    /// </summary>
    /// <param name="rb"></param>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="layerMask"></param>
    /// <returns> The RaycastHit2D hit. </returns>
    public static RaycastHit2D Raycast(this Rigidbody2D rb, Vector2 offset, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D hit = Physics2D.Raycast(rb.position + offset, direction, distance, layerMask);
        return hit;
    }

    /// <summary>
    /// Sends a boxCast at the desired position, size, angle, and direction.
    /// </summary>
    /// <param name="rb"></param>
    /// <param name="origin"></param>
    /// <param name="size"></param>
    /// <param name="angle"></param>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="layerMask"></param>
    /// <returns> The RaycastHit2D hit.</returns>
    public static RaycastHit2D BoxCast(this Rigidbody2D rb, Vector2 offset, Vector2 size, float angle, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D hit = Physics2D.BoxCast(rb.position + offset, size, angle, direction, distance, layerMask);
        return hit;
    }
}
