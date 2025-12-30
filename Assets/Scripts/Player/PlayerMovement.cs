using System;
using System.Collections;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    #region Variables
    public enum PlayerStates
    {
        Grounded,
        InAir,
        GrappleThrow,
        GrappleSwinging,
        OnWall
    }

    //References
    private Rigidbody2D rb;
    private GrapplerMovement grapplerMovementScript;

    [Header("Runtime")]
    public PlayerStates playerState  = PlayerStates.Grounded;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private Vector2 additionalVelocity;
    [SerializeField] private Vector2 postGrappleVelocity;
    [SerializeField] private float moveSpeed;

    [Header("Raycasting")]
    [SerializeField] private float groundBoxCastLength = 0.75f;
    [SerializeField] private float groundBoxCastYOffset = 1;
    [SerializeField] private float groundRaycastDistance = 1f;
    [SerializeField] private LayerMask groundRaycastLayerMask;

    [Header("Ground")]
    [SerializeField] private float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;

    [Header("Jumping")]
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float maxJumpTime = 1f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);

    [Header("Extra Velocity")]
    public Vector2 grapplerDirectionFromPrevPoint { get; private set; }
    [SerializeField] private float PGVxDecayFactor = 0.5f;
    [SerializeField] private float PGVyDecayFactor = 7f;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        grapplerMovementScript = GetComponent<GrapplerMovement>();
    }

    private void Update()
    {
        
    }

    private void FixedUpdate()
    {
        CheckForGrounded();
        additionalVelocity = CalculateAdditionalVelocity();
        ApplyMovement();
    }

    public Vector2 GetVelocity()
    {
        return velocity;
    }

     /// <summary>
     /// Applies the changes in velocity through code to the player's position in the scene. This is applied differently for each playerState
     /// </summary>
    private void ApplyMovement()
    {
        switch (playerState)
        {
            case PlayerStates.Grounded:
                //Get horizontal input to move but don't use gravity
                velocity.x = moveSpeed;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.InAir:
                //Use gravity and horizontal input
                ApplyGravity();
                velocity.x = moveSpeed;
                velocity += additionalVelocity;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.GrappleThrow:
                //Gravity but no horizontal input
                ApplyGravity();
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.GrappleSwinging:
                //Special grapple stuff
                Vector2 prevPoint = rb.position;

                Vector2 newPosition = grapplerMovementScript.GrappleSwingMovement();
                rb.MovePosition(newPosition);

                grapplerDirectionFromPrevPoint = (newPosition - prevPoint).normalized;
                break;
        }
        CheckForGrounded();
    }

    #region Player States

    private void CheckForGrounded()
    {
        Vector2 groundBoxCastPos = new Vector2(rb.position.x, rb.position.y - groundBoxCastYOffset);

        RaycastHit2D hitGround = rb.BoxCast(groundBoxCastPos, new Vector2(groundBoxCastLength, 0.1f), 0, Vector2.up, 20, groundRaycastLayerMask);

        RaycastHit2D hitCenter = rb.Raycast(rb.position, Vector2.down, groundRaycastDistance + 0.1f, groundRaycastLayerMask);

        //print(((hitLeft || hitRight), playerState == PlayerStates.InAir, velocity.y <= 0));

        if (hitGround && playerState == PlayerStates.InAir && velocity.y < 0)
        {
            playerState = PlayerStates.Grounded;
            velocity.y = 0;
            OffsetGroundPlayerPosition(hitCenter);
        }
        else if (hitCenter && playerState == PlayerStates.InAir && velocity.y < 0)
        {
            OffsetGroundPlayerPosition(hitCenter);
        }
        else if (!hitGround && playerState == PlayerStates.Grounded)
        {
            playerState = PlayerStates.InAir;
        }
    }

    private void OffsetGroundPlayerPosition(RaycastHit2D hit)
    {
        float hitYCoord = 0;

        if (hit)
        {
            hitYCoord = hit.point.y;
        }

        float yOffset = groundRaycastDistance - (rb.position.y - hitYCoord);
        Vector3 newPosition = new Vector3(rb.position.x, rb.position.y + yOffset);
        transform.position = newPosition;
    }

    //private void OnCollisionEnter2D(Collision2D collision)
    //{
    //    playerState = PlayerStates.Grounded;
    //    velocity.y = 0;
    //}

    //private void OnCollisionExit2D(Collision2D collision)
    //{
    //    playerState = PlayerStates.InAir;
    //}

    #endregion

    #region Ground

    /// <summary>
    /// Catches the horizontal movement input from the player
    /// </summary>
    /// <param name="context"></param>
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 moveInput = context.ReadValue<Vector2>();
        //directionToGrapplePoint = moveInput.normalized;
        moveSpeed = walkSpeed * moveInput.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();

        if (value == 1 && playerState == PlayerStates.Grounded) //holding/pressed jump
        {
            playerState = PlayerStates.InAir;
            velocity.y += value * jumpForce;
        }
        else if (value == 0 && playerState == PlayerStates.InAir) //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }

        if (value == 1 && playerState == PlayerStates.GrappleSwinging)
        {
            JumpOutOfGrapple();
        }
    }

    #endregion

    #region Air

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;

        //velocity.y = Mathf.Max(velocity.y, gravity / 2f);
    }

    private Vector2 CalculateAdditionalVelocity()
    {
        if (playerState != PlayerStates.InAir)
        {
            postGrappleVelocity = Vector2.zero;
            return Vector2.zero;
        }

        Vector2 totalAdditionalVelocity = Vector2.zero;

        if (postGrappleVelocity.magnitude > 0)
        {
            totalAdditionalVelocity += postGrappleVelocity;

            postGrappleVelocity.y /= PGVyDecayFactor;
            postGrappleVelocity.y = postGrappleVelocity.y < 0.01f ? 0 : postGrappleVelocity.y;

            postGrappleVelocity.x = postGrappleVelocity.x < 0 ? Mathf.Min(postGrappleVelocity.x + PGVxDecayFactor, 0) : Mathf.Max(postGrappleVelocity.x - PGVxDecayFactor, 0);

            //postGrappleVelocity = new Vector2(Mathf.Max(postGrappleVelocity.x, 0), Mathf.Max(postGrappleVelocity.y, 0));
        }
        return totalAdditionalVelocity;
    }

    #endregion

    #region Grappler

    public void Grapple(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;

        if (keyPressed && playerState == PlayerStates.InAir)
        {
            playerState = PlayerStates.GrappleThrow;
            StartCoroutine(grapplerMovementScript.GrappleThrowCoroutine());
        }
    }

    public void JumpOutOfGrapple()
    {
        playerState = PlayerStates.InAir;
        postGrappleVelocity = grapplerMovementScript.SetPostGrappleVelocity();
        print(postGrappleVelocity);
        velocity = Vector2.zero;
    }

    #endregion
}
