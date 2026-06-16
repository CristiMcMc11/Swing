using UnityEngine;

public class TeleportBounds : MonoBehaviour
{

    public Vector2 teleportPoint = Vector2.zero;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player")
        {
            collision.transform.position = teleportPoint;
        }
    }
}
