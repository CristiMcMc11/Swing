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
        OnWall,
        WallClimbing
    }

    //References
    private Rigidbody2D rb;
    private GrapplerMovement grapplerMovementScript;

    [Header("Runtime")]
    public PlayerStates playerState  = PlayerStates.Grounded;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private Vector2 additionalVelocity;
    [SerializeField] private Vector2 playerDirectionalInput = Vector2.zero;

    [SerializeField] private float moveSpeed;

    [Header("Raycasting")]
    [SerializeField] private float groundBoxCastLength = 0.75f;
    [SerializeField] private float groundBoxCastYOffset = 1;
    [SerializeField] private float groundRaycastDistance = 1f;
    [SerializeField] private LayerMask groundRaycastLayerMask;

    [SerializeField] private float wallBoxCastOffset = 0.5f;
    [SerializeField] private float wallBoxCastSize = 2f;

    [Header("Ground")]
    [SerializeField] private float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;

    [Header("Jumping")]
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float maxJumpTime = 1f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);

    [Header("Wall")]
    [SerializeField] private bool onRightWall;
    [SerializeField] private float wallVelocity = 0f;
    [SerializeField] private bool canGoOnWall = true;
    [SerializeField] private bool isWallClimbing = true;

    [SerializeField] private float minSlideSpeed = 0.2f;
    [SerializeField] private float maxSlideSpeed = 1f;
    [SerializeField] private float slideSpeedTweenTime = 0.4f;
    [SerializeField] private float climbHeight = 15;
    [SerializeField] private float climbTime = 0.1f;
    [SerializeField] private Vector2 wallJumpVelocity = new Vector2(10, 10);

    [Header("Extra Velocity")]
    public Vector2 grapplerDirectionFromPrevPoint { get; private set; }
    [SerializeField] private float PGVxDecayFactor = 0.5f;
    [SerializeField] private float PGVyDecayFactor = 7f;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        grapplerMovementScript = GetComponent<GrapplerMovement>();
        //Time.timeScale = 0.5f;
    }

    private void Update()
    {
        
    }

    private void FixedUpdate()
    {
        CheckForGrounded();
        CheckForWallTouch();
        DecayAdditionalVelocity();
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
                ApplyAdditionalVelocity();

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

            case PlayerStates.OnWall:
                velocity.x = 0;
                velocity.y = wallVelocity;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.WallClimbing:
                velocity.y = climbHeight / climbTime;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

        }

        CheckForWallTouch();
        CheckForGrounded();
    }

    #region Player States

    //GROUNDED
    private void CheckForGrounded()
    {
        Vector2 offset = new Vector2(0, -groundBoxCastYOffset);

        RaycastHit2D hitGround = rb.BoxCast(offset, new Vector2(groundBoxCastLength, 0.1f), 0, Vector2.up, 1, groundRaycastLayerMask);

        RaycastHit2D hitCenter = rb.Raycast(Vector2.zero, Vector2.down, groundRaycastDistance + 0.1f, groundRaycastLayerMask);

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
        float hitYCoord = hit.point.y;

        float yOffset = groundRaycastDistance - (rb.position.y - hitYCoord);
        Vector3 newPosition = new Vector3(rb.position.x, rb.position.y + yOffset);
        transform.position = newPosition;
    }

    //WALL
    private void CheckForWallTouch()
    {
        Vector2 leftOffset = new Vector2(-wallBoxCastOffset, 0);
        Vector2 rightOffset = new Vector2(wallBoxCastOffset, 0);
        Vector2 size = new Vector2(0.1f, wallBoxCastSize);

        RaycastHit2D hitLeft = rb.BoxCast(leftOffset, size, 0, Vector2.zero, 1, groundRaycastLayerMask);
        RaycastHit2D hitRight = rb.BoxCast(rightOffset, size, 0, Vector2.zero, 1, groundRaycastLayerMask);

        if ((hitLeft || hitRight) && playerState == PlayerStates.InAir && canGoOnWall)
        {
            onRightWall = hitRight ? true : false;

            playerState = PlayerStates.OnWall;
            TweenWallSlideSpeed();
            OffsetWallPlayerPosition(hitLeft ? hitLeft : hitRight, hitLeft ? false : true);
        }
        else if (!(hitLeft || hitRight) && playerState == PlayerStates.OnWall)
        {
            playerState = PlayerStates.InAir;
        }
    }

    private void OffsetWallPlayerPosition(RaycastHit2D hit, bool onTheRight)
    {
        float xCoordinate = hit.point.x;
        float playerXPos = onTheRight ? xCoordinate - wallBoxCastOffset : xCoordinate + wallBoxCastOffset;
        Vector3 newPosition = new Vector3(playerXPos, rb.position.y);
        transform.position = newPosition;
    }

    #endregion

    #region Recieving Input

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
        bool keyPressed = value == 1;

        if (keyPressed && playerState == PlayerStates.Grounded) //holding/pressed jump
        {
            playerState = PlayerStates.InAir;
            velocity.y += value * jumpForce;
        }
        else if (!keyPressed && playerState == PlayerStates.InAir) //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }
        else if (keyPressed && playerState == PlayerStates.GrappleSwinging)
        {
            JumpOutOfGrapple();
        }
        else if (keyPressed && playerState == PlayerStates.OnWall)
        {
            OnWallJumpInput();
        }
    }

    public void Grapple(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;

        if (keyPressed && (playerState == PlayerStates.InAir || playerState == PlayerStates.OnWall))
        {
            playerState = PlayerStates.GrappleThrow;
            StartCoroutine(grapplerMovementScript.GrappleThrowCoroutine());
        }
    }

    public void DirectionalInput(InputAction.CallbackContext context)
    {
        playerDirectionalInput = context.ReadValue<Vector2>();
    }

    #endregion

    #region Air

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;

        //velocity.y = Mathf.Max(velocity.y, gravity / 2f);
    }

    private void ApplyAdditionalVelocity()
    {
        velocity.y += additionalVelocity.y;

        if (additionalVelocity.x == 0)
        {
            velocity.x = moveSpeed;
        }

        if (playerDirectionalInput.x == 0)
        {
            velocity.x = additionalVelocity.x;
            return;
        }

        bool inputOpposingVelocity = (playerDirectionalInput.x < 0 && additionalVelocity.x > 0) || (playerDirectionalInput.x > 0 && additionalVelocity.x < 0);
        if (inputOpposingVelocity)
        {
            velocity.x = moveSpeed;
        }
        else
        {
            if (playerDirectionalInput.x > 0)
            {
                velocity.x = Mathf.Max(moveSpeed, additionalVelocity.x);
            }
            else if (playerDirectionalInput.x < 0)
            {
                velocity.x = Mathf.Min(moveSpeed, additionalVelocity.x);
            }
        }
    }

    private void DecayAdditionalVelocity()
    {
        if (playerState != PlayerStates.InAir)
        {
            additionalVelocity = Vector2.zero;
        }

        if (additionalVelocity.magnitude > 0)
        {
            additionalVelocity.y /= PGVyDecayFactor;
            additionalVelocity.y = additionalVelocity.y < 0.01f ? 0 : additionalVelocity.y;

            additionalVelocity.x = additionalVelocity.x < 0 ? Mathf.Min(additionalVelocity.x + PGVxDecayFactor, 0) : Mathf.Max(additionalVelocity.x - PGVxDecayFactor, 0);
            if ((playerDirectionalInput.x < 0 && additionalVelocity.x > 0) || (playerDirectionalInput.x > 0 && additionalVelocity.x < 0))
            {
                additionalVelocity.x = 0;
            }

            //postGrappleVelocity = new Vector2(Mathf.Max(postGrappleVelocity.x, 0), Mathf.Max(postGrappleVelocity.y, 0));
        }
    }

    #endregion

    #region On Wall

    private void OnWallJumpInput()
    {
        if (playerDirectionalInput.y > 0)
        {
            StartCoroutine(WallClimb());
            
        }
        else
        {
            StartCoroutine(WallJump());
        }
    }

    private IEnumerator WallClimb()
    {
        LeanTween.cancel(gameObject);
        playerState = PlayerStates.WallClimbing;
        yield return new WaitForSeconds(climbTime);
        playerState = PlayerStates.OnWall;
        TweenWallSlideSpeed();
        
    }

    private IEnumerator WallJump()
    {
        playerState = PlayerStates.InAir;
        additionalVelocity = wallJumpVelocity;
        additionalVelocity.x = onRightWall ? -additionalVelocity.x : additionalVelocity.x;

        canGoOnWall = false;
        yield return new WaitForSeconds(0.1f);
        canGoOnWall = true;
    }

    private void TweenWallSlideSpeed()
    {
        if (LeanTween.isTweening())
        {
            LeanTween.cancel(gameObject);
        }

        LeanTween.value(-minSlideSpeed, -maxSlideSpeed, slideSpeedTweenTime)
            .setOnUpdate((float value) =>
            {
                wallVelocity = value;
            });
    }

    #endregion

    #region Grappler

    public void JumpOutOfGrapple()
    {
        playerState = PlayerStates.InAir;
        additionalVelocity = grapplerMovementScript.SetPostGrappleVelocity();
        print(additionalVelocity);
        velocity = additionalVelocity;
    }

    #endregion
}
