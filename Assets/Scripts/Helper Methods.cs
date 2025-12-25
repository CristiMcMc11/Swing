using Unity.VisualScripting;
using UnityEngine;

public static class HelperMethods
{
    /// <summary>
    /// Sends a raycast with the rigidbody position as the origin. MAKE SURE A CONTACT POINT EXISTS!
    /// </summary>
    /// <param name="rb"> </param>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="layerMask"></param>
    /// <returns> The Vector2 point of the first raycast contact </returns>
    public static Vector2 RaycastReturnPoint(this Rigidbody2D rb, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D hit = Physics2D.Raycast(rb.position, direction, distance, layerMask);

        return hit.point;
    }

    /// <summary>
    /// Sends a raycast with the rigidbody position as the origin.
    /// </summary>
    /// <param name="rb"></param>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="layerMask"></param>
    /// <returns> Whether or not a hit exists </returns>
    public static bool Raycast(this Rigidbody2D rb, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D hit = Physics2D.Raycast(rb.position, direction, distance, layerMask);
        return hit;
    }
}
