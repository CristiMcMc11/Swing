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
        GrapplePulling,
        OnWall,
        WallClimbing,
        Vaulting
    }

    //References
    private Rigidbody2D rb;
    private GrapplerMovement grapplerMovementScript;

    [Header("Runtime")]
    public PlayerStates playerState  = PlayerStates.Grounded;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private Vector2 additionalVelocity;

    [SerializeField] private Vector2 playerDirectionalInput = Vector2.zero;
    [SerializeField] private Vector2 playerMoveInput = Vector2.zero;

    [SerializeField] private bool banMoveRight = false;
    [SerializeField] private bool banMoveLeft = false;

    [SerializeField] private bool playerCannotMove = false;
    [SerializeField] private float moveSpeed;

    [Header("Raycasting")]
    public LayerMask playerRaycastLayerMask;
    [SerializeField] private float groundBoxCastLength = 0.75f;
    [SerializeField] private float groundBoxCastYOffset = 1;
    [SerializeField] private float groundRaycastDistance = 1f;

    [SerializeField] private float wallBoxCastOffset = 0.5f;
    [SerializeField] private float wallBoxCastSize = 2f;

    [Header("Ground")]
    [SerializeField] private float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;

    [Header("Jumping")]
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float maxJumpTime = 1f; 
    [SerializeField] private float terminalVelocity = -20;
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

    [SerializeField] private float vaultTime = 0.25f;

    [Header("Extra Velocity")]
    [SerializeField] private float PGVxDecayFactor = 0.5f;
    [SerializeField] private float PGVyDecayFactor = 7f;
    public Vector2 grapplerDirectionFromPrevPoint { get; private set; }
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
        SetMoveSpeed();
        CheckForWallTouch();
        CheckForGrounded();
        CheckForHeadhit();
        DecayAdditionalVelocity();

        ApplyMovement();
    }

    public Vector2 GetVelocity()
    {
        return velocity;
    }

    private void SetMoveSpeed()
    {
        moveSpeed = walkSpeed * playerMoveInput.x;
        
        if (banMoveLeft && moveSpeed < 0 || banMoveRight && moveSpeed > 0)
        {
            moveSpeed = 0;
        } 
    }

    private IEnumerator SetBanMoveTimer(bool leftBan, bool rightBan, float timeSec)
    {
        banMoveLeft = leftBan ? true : banMoveLeft;
        banMoveRight = rightBan ? true : banMoveRight;
        yield return new WaitForSeconds(timeSec);
        banMoveLeft = leftBan ? false : banMoveLeft;
        banMoveRight = rightBan ? false : banMoveRight;
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

            case PlayerStates.GrapplePulling:
                rb.MovePosition(grapplerMovementScript.GrapplePullMovement());
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
        Vector2 size = new Vector2(groundBoxCastLength, 0.1f);

        RaycastHit2D hitGround = rb.BoxCast(offset, size, 0, Vector2.up, 1, playerRaycastLayerMask);

        offset.y -= CalculateRaycastExtraLength(velocity.y);
        size.y += CalculateRaycastExtraLength(velocity.y);

        RaycastHit2D hitCenter = rb.BoxCast(offset, new Vector2(groundBoxCastLength, 0.1f), 0, Vector2.up, 1, playerRaycastLayerMask);
        //RaycastHit2D hitCenter = rb.Raycast(Vector2.zero, Vector2.down, groundRaycastDistance + CalculateRaycastExtraLength(velocity.y), playerRaycastLayerMask);

        //print(((hitLeft || hitRight), playerState == PlayerStates.InAir, velocity.y <= 0));

        if (hitGround && velocity.y < 0)
        {
            playerState = PlayerStates.Grounded;
            velocity.y = 0;
            OffsetGroundPlayerPosition(hitCenter);
        }
        else if (hitGround && playerState == PlayerStates.GrappleSwinging)
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

    private float CalculateRaycastExtraLength(float velocity)
    {
        return Mathf.Abs(velocity/50);
    }

    //AIR
    private void CheckForHeadhit()
    {
        Vector2 offset = new Vector2(0, groundBoxCastYOffset);
        RaycastHit2D hit = rb.BoxCast(Vector2.zero, new Vector2(groundBoxCastLength, 0.1f), 0, Vector2.up, 1, playerRaycastLayerMask);

        if (hit && velocity.y > 0)
        {
            velocity.y = 0;
            additionalVelocity.y = 0;
        }
    }

    //WALL
    private void CheckForWallTouch()
    {
        Vector2 leftOffset = new Vector2(-wallBoxCastOffset, 0);
        Vector2 rightOffset = new Vector2(wallBoxCastOffset, 0);
        Vector2 size = new Vector2(0.1f, wallBoxCastSize);

        RaycastHit2D hitLeft = rb.BoxCast(leftOffset, size, 0, Vector2.zero, 1, playerRaycastLayerMask);
        RaycastHit2D hitRight = rb.BoxCast(rightOffset, size, 0, Vector2.zero, 1, playerRaycastLayerMask);

        float extraLength = CalculateRaycastExtraLength(velocity.x);
        RaycastHit2D rayLeft = rb.Raycast(Vector2.zero, Vector2.left, leftOffset.magnitude + extraLength, playerRaycastLayerMask);
        RaycastHit2D rayRight = rb.Raycast(Vector2.zero, Vector2.right, rightOffset.magnitude + extraLength, playerRaycastLayerMask);


        bool wallIsHit = hitLeft || hitRight || rayLeft || rayRight;
        bool playerIsCorrectState = playerState == PlayerStates.InAir || playerState == PlayerStates.GrappleSwinging;
        bool playerHasCorrectDirectionalInput = ((hitLeft || rayLeft) && moveSpeed < 0) || ((hitRight || rayRight) && moveSpeed > 0);

        if (wallIsHit && playerIsCorrectState && canGoOnWall && playerHasCorrectDirectionalInput) //General case (in the air)
        {
            RaycastHit2D correctHit;
            if (hitLeft || hitRight)
            {
                onRightWall = hitRight ? true : false;
                correctHit = onRightWall ? hitRight : hitLeft;
            }
            else
            {
                onRightWall = rayRight ? true : false;
                correctHit = onRightWall ? rayRight : rayLeft;
            }

            if (CheckForVault(correctHit, onRightWall)) //CheckForVault() will call Vault() if it detects a hit
            {
                return;
            }

            EnterWall(correctHit);
        }
        else if ((hitLeft || hitRight) && playerState == PlayerStates.WallClimbing) //Checking for a vault while wall climbing
        {
            onRightWall = hitRight ? true : false;
            RaycastHit2D correctHit = onRightWall ? hitRight : hitLeft;
            CheckForVault(correctHit, onRightWall);
        }
        else if (playerState == PlayerStates.OnWall && ((onRightWall && moveSpeed < 0) || (!onRightWall && moveSpeed > 0))) //checking whether input doesn't match the wall direction
        {
            LeaveWall(false);
        }
        else if (!(hitLeft || hitRight) && playerState == PlayerStates.OnWall) //fell off the wall
        {
            LeaveWall(false);
        }
    }

    private void OffsetWallPlayerPosition(RaycastHit2D hit, bool onTheRight)
    {
        float xCoordinate = hit.point.x;
        float playerXPos = onTheRight ? xCoordinate - wallBoxCastOffset : xCoordinate + wallBoxCastOffset;
        Vector3 newPosition = new Vector3(playerXPos, rb.position.y);
        transform.position = newPosition;
    }

    //VAULT
    private bool CheckForVault(RaycastHit2D wallHit, bool hitOnRight)
    {
        Vector2 direction = hitOnRight ? Vector2.right : Vector2.left;
        RaycastHit2D vaultHit = rb.Raycast(Vector2.zero, direction, wallBoxCastOffset + 0.5f, playerRaycastLayerMask);
        if (!vaultHit && wallHit.point.y <= rb.position.y)
        {
            StartCoroutine(Vault(hitOnRight, wallHit.point));
            return true;
        } else
        {
            return false;
        }
    }

    #endregion

    #region Recieving Input

    /// <summary>
    /// Catches the horizontal movement input from the player
    /// </summary>
    /// <param name="context"></param>
    public void OnMove(InputAction.CallbackContext context)
    {
        playerMoveInput = context.ReadValue<Vector2>();
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
        else if (keyPressed && playerState == PlayerStates.GrappleSwinging)
        {
            grapplerMovementScript.StartGrapplePulling();
        }
        else if (!keyPressed && playerState == PlayerStates.GrapplePulling)
        {
            velocity = Vector2.zero;
            additionalVelocity = grapplerMovementScript.CancelGrapplePull();
            CheckForWallTouch();
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
        velocity.y = Mathf.Max(velocity.y, terminalVelocity);
    }

    private void ApplyAdditionalVelocity()
    {
        velocity.y += additionalVelocity.y;

        if (additionalVelocity.x == 0)
        {
            velocity.x = moveSpeed;
            return;
        }

        if (moveSpeed == 0)
        {
            velocity.x = additionalVelocity.x;
            return;
        }

        bool inputOpposingVelocity = (moveSpeed < 0 && additionalVelocity.x > 0) || (moveSpeed > 0 && additionalVelocity.x < 0);  
        if (inputOpposingVelocity)
        {
            velocity.x = moveSpeed;
        }
        else
        {
            if (playerMoveInput.x > 0)
            {
                velocity.x = Mathf.Max(moveSpeed, additionalVelocity.x);
            }
            else if (playerMoveInput.x < 0)
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
            if ((moveSpeed < 0 && additionalVelocity.x > 0) || (moveSpeed > 0 && additionalVelocity.x < 0))
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
            WallJump();
        }
    }

    private void EnterWall(RaycastHit2D correctHit)
    {
        playerState = PlayerStates.OnWall;
        TweenWallSlideSpeed();
        OffsetWallPlayerPosition(correctHit, onRightWall);
    }

    private void LeaveWall(bool wallJump)
    {
        velocity.y = additionalVelocity.y;
        if (wallJump)
        {
            StartCoroutine(SetBanMoveTimer(!onRightWall, onRightWall, 0.1f));
        }
        StartCoroutine(SetCannotGoOnWallTimer(0.05f));
        playerState = PlayerStates.InAir;
    }

    private IEnumerator WallClimb()
    {
        LeanTween.cancel(gameObject);
        playerState = PlayerStates.WallClimbing;
        StartCoroutine(SetBanMoveTimer(onRightWall, !onRightWall, climbTime));

        yield return new WaitForSeconds(climbTime);

        if (playerState != PlayerStates.Vaulting)
        {
            playerState = PlayerStates.OnWall;
            velocity.y = 0;
            TweenWallSlideSpeed();
        }
    }

    private void WallJump()
    {
        LeaveWall(true);
        additionalVelocity = wallJumpVelocity;
        additionalVelocity.x = onRightWall ? -additionalVelocity.x : additionalVelocity.x;
    }

    private IEnumerator SetCannotGoOnWallTimer(float timeSeconds)
    {
        canGoOnWall = false;
        yield return new WaitForSeconds(timeSeconds);
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

    #region Vaulting

    private IEnumerator Vault(bool onRightWall, Vector2 hitPoint)
    {
        playerState = PlayerStates.Vaulting;

        //Part 1: Move the player up
        velocity = Vector2.zero;
        float yOffset = groundBoxCastYOffset - (transform.position.y - hitPoint.y);
        transform.position = new Vector2(transform.position.x, transform.position.y + yOffset);

        yield return new WaitForSeconds(vaultTime / 2);

        //Part 2: Move the player right/left
        float newX = onRightWall ? transform.position.x + wallBoxCastOffset * 2 : transform.position.x - wallBoxCastOffset * 2;
        Vector2 newPos = new Vector2(newX, transform.position.y);

        LeanTween.move(gameObject, newPos, vaultTime / 2)
            .setOnUpdate((float nothing) =>
            {
                if (playerState != PlayerStates.Vaulting)
                {
                    print("cancelling move");
                    LeanTween.cancel(gameObject);
                }
            });
        yield return new WaitForSeconds(vaultTime / 2);
        playerState = playerState == PlayerStates.Vaulting ? PlayerStates.Grounded : playerState;
    }

    #endregion

    #region Grappler

    public void JumpOutOfGrapple()
    {
        playerState = PlayerStates.InAir;
        additionalVelocity = grapplerMovementScript.SetPostGrappleVelocity();
        velocity = additionalVelocity;
    }

    #endregion
}
