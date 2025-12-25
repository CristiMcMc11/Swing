using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    #region Variables
    private enum PlayerStates
    {
        Grounded,
        InAir,
        Grappling,
        OnWall
    }

    //References
    private Rigidbody2D rb;

    [Header("Runtime")]
    [SerializeField] private PlayerStates playerState = PlayerStates.Grounded;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private float speed;

    [Header("Ground")]
    [SerializeField] private float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;

    [Header("Jumping")]
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float maxJumpTime = 1f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        ApplyGravity();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

     /// <summary>
     /// Applies the changes in velocity through code to the player's position in the scene
     /// </summary>
    private void ApplyMovement()
    {
        velocity.x = speed;

        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }

    #region Player States

    private void OnCollisionEnter2D(Collision2D collision)
    {
        playerState = PlayerStates.Grounded;
        velocity.y = 0;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        playerState = PlayerStates.InAir;
    }

    #endregion

    #region Ground

    /// <summary>
    /// Catches the horizontal movement input from the player
    /// </summary>
    /// <param name="context"></param>
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 moveInput = context.ReadValue<Vector2>();
        speed = walkSpeed * moveInput.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();

        if (value == 1) //holding/pressed jump
        {
            velocity.y += value * jumpForce / 2;
            print(velocity.y);
        }
        else //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }
    }

    #endregion

    #region Air

    private void ApplyGravity()
    {
        if (playerState != PlayerStates.Grounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        //velocity.y = Mathf.Max(velocity.y, gravity / 2f);
    }

    #endregion
}
