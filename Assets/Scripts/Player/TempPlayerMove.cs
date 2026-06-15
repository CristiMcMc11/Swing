using UnityEngine;
using UnityEngine.InputSystem;

public class TempPlayerMove : MonoBehaviour
{
    private Rigidbody2D rb;
    private DistanceJoint2D joint;
    public Vector2 velocity = Vector2.zero;
    private Vector2 mousePos = Vector2.zero;
    private float dirInput = 0;
    public GameObject box;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<DistanceJoint2D>();
        joint.enabled = false;
    }

    private void Update()
    {
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        velocity = new Vector2(8 * dirInput, 0);
        rb.MovePosition(velocity * Time.fixedDeltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        dirInput = input.x == 0 ? 0 : Mathf.Sign(input.x);
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        if (value == 1)
        {
            Vector2 point = new Vector2(transform.position.x + 5, transform.position.y);
            box.transform.position = point;
            joint.enabled = true;
            joint.connectedAnchor = point;
            joint.distance = Vector3.Distance(point, transform.position);
        }
    }

    public void GetMousePos(InputAction.CallbackContext context)
    {
        mousePos = context.ReadValue<Vector2>();
        
    }
}
