using System.Collections;

//using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    #region Variables
    public enum PlayerStates
    {
        Grounded,
        InAir,
        OnWall,
        WallClimb,
        Vault,
        GrappleThrow,
        GrappleSwing,
        GrapplePull,
        WallHold
    }

    public enum PlayerDirection
    {
        Left,
        Right
    }

    //References
    private Rigidbody2D rb;
    private GrapplerMovement gmScript;

    private GameObject visualsGO;
    private PlayerVisuals visualsScript;

    [Header("Runtime")]
    public PlayerStates playerState  = PlayerStates.Grounded;
    public PlayerDirection playerDirection = PlayerDirection.Right; //SET GET; PRIVATE SET

    [SerializeField] private Vector2 velocity;
    [SerializeField] private Vector2 additionalVelocity;

    [SerializeField] private Vector2 playerDirectionalInput = Vector2.zero;

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

    [SerializeField] private float groundSnapHorizontal = 0.3f;
    [SerializeField] private float groundSnapVertical = 0.1f;
    [SerializeField] private float groundSnapCorrection = 0.1f;

    [Header("Ground")]
    [SerializeField] private float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private Vector2 newPos = Vector2.zero;

    [Header("Jumping")]
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float maxJumpTime = 1f; 
    [SerializeField] private float terminalVelocity = -20;
    [SerializeField] private float banWallAfterJumpTimeSec = 0.2f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);

    [Header("Acceleration")]
    [SerializeField] private float accelerationTime = 0.5f; //time to get to walkSpeed
    [SerializeField] private float airResistance = 1f; //units a second;
    [SerializeField] private float groundFriction = 5f;
    [SerializeField] private bool instantAccelerate = false;
    [SerializeField] private bool instantTurn = true;
    [SerializeField] private float accelerationValue;
    private int accelerationTweenId;

    [Header("Wall")]
    private Vector2 wallHoldPos;
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

    public bool disableVault = false;

    [Header("Extra Velocity")]
    [SerializeField] private float PGVxDecayFactor = 0.5f;
    [SerializeField] private float PGVyDecayFactor = 7f;
    public Vector2 grapplerDirectionFromPrevPoint { get; private set; }

    [Header("Animation")]
    private Animator animator;

    [Header("testing/misc")]
    [SerializeField] private float gameSpeed;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gmScript = GetComponent<GrapplerMovement>();
        animator = GetComponent<Animator>();

        visualsGO = transform.Find("Visuals").gameObject;
        visualsScript = visualsGO.GetComponent<PlayerVisuals>();
        //Time.timeScale = 0.5f;
    }

    private void OnDrawGizmos()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        Gizmos.color = UnityEngine.Color.yellow;
        Vector2 position = rb.position + new Vector2(0, -groundBoxCastYOffset);
        Vector2 size = new Vector2(groundBoxCastLength, 0.1f);
        Gizmos.DrawWireCube(position, size);

        Vector2 offset = new Vector2(0, groundBoxCastYOffset);
        Gizmos.DrawWireCube(rb.position + offset, new Vector2(groundBoxCastLength, 0.1f));

        Gizmos.color = UnityEngine.Color.deepPink;
        size = new Vector2(0.1f, wallBoxCastSize);
        Vector2 leftOffset = new Vector2(-wallBoxCastOffset, 0);
        Vector2 rightOffset = new Vector2(wallBoxCastOffset, 0);
        Gizmos.DrawWireCube(rb.position + leftOffset, size);
        Gizmos.DrawWireCube(rb.position + rightOffset, size);

        offset = new Vector2(0, groundBoxCastYOffset);
        Gizmos.DrawWireCube(rb.position + offset, new Vector2(groundBoxCastLength, 0.1f));

        //Gizmos.color = UnityEngine.Color.yellow;
        //Gizmos.DrawWireCube(transform.position + new Vector3(0, 0.1f, 0), new Vector2(groundBoxCastLength + 0.2f, wallBoxCastSize - groundSnapVertical));
        //Gizmos.DrawWireCube(transform.position + new Vector3(0, -groundBoxCastYOffset, 0), new Vector2(groundBoxCastLength - groundSnapHorizontal, 0.2f));
    }

    private void FixedUpdate()
    {
        //CheckForWallTouch();
        //CheckForGrounded();
        SetMoveSpeed();
        CheckForHeadhit();
        DecayAdditionalVelocity();
        SetVelocity();
        FindRotations();
        ApplyMovement();
    }

    private void LateUpdate()
    {
        FindPlayerState(true);
        Time.timeScale = gameSpeed;
    }

    public Vector2 GetVelocity()
    {
        return velocity;
    }
    
    public float GetGravity()
    {
        return gravity;
    }

    public void ZeroVelocity()
    {
        velocity = Vector2.zero;
    }

    private void ResetAirAbilities()
    {
        gmScript.ResetGrapples();
    }

    private void SetMoveSpeed()
    {
        if (playerState == PlayerStates.Grounded || playerState == PlayerStates.InAir)
        {
            moveSpeed = walkSpeed * accelerationValue;
            if (banMoveLeft && moveSpeed < 0 || banMoveRight && moveSpeed > 0)
            {
                moveSpeed = 0;
            }
        }
        else
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

    private void AddForce(Vector2 force, bool resetVelocityBefore)
    {
        velocity = resetVelocityBefore ? Vector2.zero : velocity;
        velocity += force;
    }

    private void SetVelocity()
    {
        float accelerationRate = (walkSpeed / accelerationTime) * Time.fixedDeltaTime;
        if (instantAccelerate) {
            accelerationRate = Mathf.Max(velocity.x, walkSpeed);
        }

        switch (playerState)
        {
            case PlayerStates.Grounded:
                if (Mathf.Abs(velocity.x) > walkSpeed)
                {
                    velocity.x -= groundFriction * Mathf.Sign(velocity.x) * Time.fixedDeltaTime;
                }
                else
                {
                    if (Mathf.Sign(playerDirectionalInput.x) != Mathf.Sign(velocity.x) && playerDirectionalInput.x != 0) //opposite input to velocity
                    {
                        velocity.x -= accelerationRate * Mathf.Sign(velocity.x) * 2;
                    }
                    else if (playerDirectionalInput.x > 0) //input is right
                    {
                        velocity.x = Mathf.Min(velocity.x + accelerationRate, walkSpeed);
                    }
                    else if (playerDirectionalInput.x < 0) //input is left
                    {
                        velocity.x = Mathf.Max(velocity.x - accelerationRate, -walkSpeed);
                    }
                    else if (playerDirectionalInput.x == 0) //input is neutral
                    {
                        if (velocity.x > 0)
                        {
                            velocity.x = Mathf.Clamp(velocity.x - accelerationRate, 0, walkSpeed);
                        }
                        else if (velocity.x < 0)
                        {
                            velocity.x = Mathf.Clamp(velocity.x + accelerationRate, -walkSpeed, 0);
                        }
                    }
                }
                
                break;

            case PlayerStates.InAir:
                ApplyGravity();

                if (Mathf.Abs(velocity.x) > walkSpeed)
                {
                    velocity.x -= airResistance * Mathf.Sign(velocity.x) * Time.fixedDeltaTime;

                    if (Mathf.Sign(playerDirectionalInput.x) != Mathf.Sign(velocity.x) && playerDirectionalInput.x != 0)
                    {
                        velocity.x -= accelerationRate * Mathf.Sign(velocity.x);
                    }
                }
                else
                {
                    if (playerDirectionalInput.x > 0)
                    {
                        velocity.x = Mathf.Min(velocity.x + accelerationRate, walkSpeed);
                    }
                    else if (playerDirectionalInput.x < 0)
                    {
                        velocity.x = Mathf.Max(velocity.x - accelerationRate, -walkSpeed);
                    }
                    else if (playerDirectionalInput.x == 0)
                    {
                        if (velocity.x != 0)
                        {
                            velocity.x = velocity.x > 0 ? Mathf.Clamp(velocity.x - accelerationRate, 0, walkSpeed) : Mathf.Clamp(velocity.x + accelerationRate, -walkSpeed, 0);
                        }
                    }
                }
                    break;

            case PlayerStates.GrappleSwing:
                velocity = Vector2.zero;
                break;

            //case PlayerStates.GrappleThrow:
            //    //Gravity but no horizontal input
            //    ApplyGravity();
            //    rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
            //    break;

            case PlayerStates.OnWall:
                velocity.x = 0;
                velocity.y = wallVelocity;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

                //case PlayerStates.WallClimbing:
                //    velocity.y = climbHeight / climbTime;
                //    rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                //    break;
        }
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
                //SetHorizontalSpeed(true);
                //velocity.x = horizontalSpeed;
                newPos = rb.position + velocity * Time.fixedDeltaTime;
                rb.MovePosition(newPos);
                break;

            case PlayerStates.InAir:
                //Use gravity and horizontal input

                //SetHorizontalSpeed(true);
                //ApplyAdditionalVerticalVelocity();
                //velocity.x = horizontalSpeed;
                newPos = rb.position + velocity * Time.fixedDeltaTime;
                rb.MovePosition(newPos);
                break;

            case PlayerStates.GrappleThrow:
                //Gravity but no horizontal input
                ApplyGravity();
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.GrappleSwing:
                //Special grapple stuff

                Vector2 prevPoint = rb.position;
                newPos = rb.position + velocity * Time.fixedDeltaTime;
                //rb.MovePosition(newPos);

                //Vector2 newPosition = gmScript.GrappleSwingMovement();
                //rb.MovePosition(newPosition);

                //grapplerDirectionFromPrevPoint = (newPosition - prevPoint).normalized;
                break;

            case PlayerStates.GrapplePull:
                rb.MovePosition(gmScript.GrapplePullMovement());
                break;

            case PlayerStates.OnWall:
                //velocity.x = 0;
                //velocity.y = wallVelocity;
                //SetHorizontalSpeed(false);
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            //case PlayerStates.WallClimb:
            //    velocity.y = climbHeight / climbTime;
            //    rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
            //    break;

            case PlayerStates.WallHold:
                rb.MovePosition(wallHoldPos);
                break;
        }
    }

    private void FindRotations()
    {
        if (playerState == PlayerStates.Grounded || playerState == PlayerStates.InAir)
        {
            if (velocity.x < 0)
            {
                playerDirection = PlayerDirection.Left;
            }
            else if (velocity.x > 0)
            {
                playerDirection = PlayerDirection.Right;
            }
        }

        float zRotation = 0;
        if (playerState == PlayerStates.GrappleSwing)
        {
            playerDirection = gmScript.grappleSpeed < 0 ? PlayerDirection.Left : PlayerDirection.Right;
            zRotation = gmScript.GrappleRotationDeg();
            zRotation = playerDirection == PlayerDirection.Right ? zRotation : -zRotation;
        }
        else if (playerState == PlayerStates.GrapplePull)
        {
            zRotation = gmScript.GrappleRotationDeg();
            zRotation = playerDirection == PlayerDirection.Right ? zRotation : -zRotation;
        }

        int yRotation = playerDirection == PlayerDirection.Left ? 180 : 0;

        visualsGO.transform.rotation = Quaternion.Euler(0, yRotation, zRotation);
        //rb.rotation = zRotation;
        //transform.rotation = Quaternion.Euler(0, yRotation, rb.rotation);
    }

    private void StartAcceleration(float playerMoveInput)
    {
        LeanTween.cancel(accelerationTweenId);

        if (playerMoveInput == 0)
        {
            accelerationTweenId = LeanTween.value(accelerationValue, 0, Mathf.Abs(accelerationValue) * accelerationTime)
                //.setEaseOutQuad()
                .setOnUpdate((float val) =>
                {
                    accelerationValue = val;
                }).id;
        }

        else if (playerMoveInput < 0 && moveSpeed > 0 || playerMoveInput > 0 && moveSpeed < 0)
        {
            accelerationTweenId = LeanTween.value(accelerationValue, 0, (Mathf.Abs(accelerationValue) * accelerationTime)/4)
                //.setEaseOutQuad()
                .setOnUpdate((float val) =>
                {
                    accelerationValue = val;
                })
                .setOnComplete(() =>
                {
                    accelerationTweenId = LeanTween.value(accelerationValue, 1, (accelerationTime - Mathf.Abs(accelerationValue) * accelerationTime)/4)
                    .setEaseOutQuad()
                    .setOnUpdate((float val) =>
                    {
                        accelerationValue = playerMoveInput < 0 ? -val : val;
                    }).id;
                }).id;
            
        }

        else
        {
            accelerationTweenId = LeanTween.value(accelerationValue, 1, accelerationTime - Mathf.Abs(accelerationValue) * accelerationTime)
                .setEaseOutQuad()
                .setOnUpdate((float val) =>
                {
                    accelerationValue = playerMoveInput < 0 ? -val : val;
                }).id;
        }
    }

    private void StopAcceleration()
    {
        LeanTween.cancel(accelerationTweenId);
        accelerationValue = 0;
    }

    #region Player States

    private void FindPlayerState(bool requireWallCorrectDirectionalInput)
    {
        RaycastHit2D groundedHit = CheckForGrounded();
        RaycastHit2D leftWallHit = CheckForWallTouch(true);
        RaycastHit2D rightWallHit = CheckForWallTouch(false);

        bool wallHit = leftWallHit || rightWallHit;
        bool playerIsCorrectState = playerState == PlayerStates.InAir || playerState == PlayerStates.GrappleSwing;
        bool playerHasCorrectDirectionalInput = (leftWallHit && playerDirectionalInput.x < 0) || (rightWallHit && playerDirectionalInput.x > 0);

        if (groundedHit && wallHit) //both ground and wall hit
        {
            OnWallAndGroundHit(groundedHit, leftWallHit, rightWallHit, playerHasCorrectDirectionalInput);
        }
        else if (!groundedHit && !wallHit && playerState == PlayerStates.Grounded) //not hitting anything but state is still grounded
        {
            playerState = PlayerStates.InAir;
        }
        else if (groundedHit && playerState == PlayerStates.InAir && velocity.y < 0) //grounded hit while falling
        {
            BecomeGrounded(groundedHit);
        }
        else if (wallHit && !groundedHit && playerIsCorrectState && (playerHasCorrectDirectionalInput || !requireWallCorrectDirectionalInput) && canGoOnWall) //wall hit and player can go on wall
        {
            OnWallHit(leftWallHit, rightWallHit);
        }
        else if (wallHit && playerState == PlayerStates.WallClimb) //Checking for a vault while wall climbing
        {
            onRightWall = rightWallHit ? true : false;
            RaycastHit2D correctHit = onRightWall ? rightWallHit : leftWallHit;
            //CheckForVault(correctHit, onRightWall);
        }
        else if (playerState == PlayerStates.OnWall && ((onRightWall && playerDirectionalInput.x < 0) || (!onRightWall && playerDirectionalInput.x > 0))) //checking whether input doesn't match the wall direction
        {
            LeaveWall(false);
        }
        else if (!wallHit && playerState == PlayerStates.OnWall) //fell off the wall
        {
            LeaveWall(false);
        }

        //Grappler Cases
        else if (wallHit && playerState == PlayerStates.GrapplePull)
        {
            if (gmScript.CheckForValidWallHold(leftWallHit ? leftWallHit : rightWallHit))
            {
                EnterWallHold(leftWallHit);
            }
        }
        else if (groundedHit && playerState == PlayerStates.GrapplePull)
        {
            gmScript.CancelGrapplePull();
        }
    }

    private void OnWallAndGroundHit(RaycastHit2D groundHit, RaycastHit2D leftWallHit, RaycastHit2D rightWallHit, bool playerHasCorrectDirectionalInput)
    {
        if (leftWallHit && rightWallHit)
        {
            BecomeGrounded(groundHit);
        }
        else if ((groundHit.point.x > transform.position.x && rightWallHit) || (groundHit.point.x < transform.position.x && leftWallHit))
        {
            if (playerState == PlayerStates.InAir && playerHasCorrectDirectionalInput)
            {
                OnWallHit(leftWallHit, rightWallHit);
            }
        }
        else if (Mathf.Round(groundHit.point.x * 100) == Mathf.Round(rb.position.x * 100) && (playerState == PlayerStates.OnWall || (playerState == PlayerStates.InAir && velocity.y < 0)))
            //if groundhit.point.x is basically equal to rb.pos.x AND (playerState is onWall OR (player state is inAir AND moving downwards))
        {
            BecomeGrounded(groundHit);
        }
    }

    //GROUNDED
    public RaycastHit2D CheckForGrounded()
    {
        Vector2 offset = new Vector2(0, -groundBoxCastYOffset);
        Vector2 size = new Vector2(groundBoxCastLength, 0.1f);

        RaycastHit2D hitGround = rb.BoxCast(offset, size, 0, Vector2.up, 1, playerRaycastLayerMask);
        return hitGround;
    }

    private void BecomeGrounded(RaycastHit2D hitGround)
    {
        if (CheckSnapToGround())
        {
            transform.position = new Vector2(transform.position.x, transform.position.y + 0.01f);
        }
        ResetAirAbilities();
        velocity.y = 0;
        playerState = PlayerStates.Grounded;
        //transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, 0);
    }

    private bool CheckSnapToGround()
    {
        Vector2 offset1 = new Vector2(0, groundSnapVertical);
        Vector2 size1 = new Vector2(groundBoxCastLength + 0.2f, wallBoxCastSize- 0.5f);

        Vector2 offset2 = new Vector2(0, -groundBoxCastYOffset);
        Vector2 size2 = new Vector2(groundBoxCastLength - groundSnapHorizontal, 0.2f);

        RaycastHit2D hit = rb.BoxCast(offset1, size1, 0, Vector2.up, 1, playerRaycastLayerMask);
        RaycastHit2D hit2 = rb.BoxCast(offset2, size2, 0, Vector2.up, 1, playerRaycastLayerMask);

        return !hit && !hit2;
    }

    //private void OffsetGroundPlayerPosition(RaycastHit2D hit)
    //{
    //    Vector2 pos = new Vector2(hit.point.x, transform.position.y);
    //    float hitYCoord = Physics2D.Raycast(pos, Vector2.down, transform.position.y - hit.point.y + 0.1f, playerRaycastLayerMask).point.y;

    //    float yOffset = groundRaycastDistance - (rb.position.y - hitYCoord);
    //    Vector3 newPosition = new Vector3(newPos.x, rb.position.y + yOffset);
    //    transform.position = newPosition;
    //}

    private float CalculateRaycastExtraLength(float velocity)
    {
        return Mathf.Abs(velocity * Time.fixedDeltaTime);
    }

    //AIR
    public bool CheckForHeadhit()
    {
        Vector2 offset = new Vector2(0, groundBoxCastYOffset);
        RaycastHit2D hit = rb.BoxCast(offset, new Vector2(groundBoxCastLength, 0.1f), 0, Vector2.up, 0, playerRaycastLayerMask);

        if (hit && velocity.y > 0)
        {
            velocity.y = 0;
            additionalVelocity.y = 0;
        }
        return hit;
    }

    //WALL
    public RaycastHit2D CheckForWallTouch(bool returnHitLeft)
    {
        Vector2 leftOffset = new Vector2(-wallBoxCastOffset, 0);
        Vector2 rightOffset = new Vector2(wallBoxCastOffset, 0);
        Vector2 size = new Vector2(0.1f, wallBoxCastSize);

        RaycastHit2D hitLeft = rb.BoxCast(leftOffset, size, 0, Vector2.zero, 1, playerRaycastLayerMask);
        RaycastHit2D hitRight = rb.BoxCast(rightOffset, size, 0, Vector2.zero, 1, playerRaycastLayerMask);

        return returnHitLeft ? hitLeft : hitRight;
    }

    private void OnWallHit(RaycastHit2D hitLeft, RaycastHit2D hitRight)
     {
        RaycastHit2D correctHit;
        onRightWall = hitRight ? true : false;
        correctHit = onRightWall ? hitRight : hitLeft;

        if (playerState == PlayerStates.GrappleSwing)
        {
            gmScript.CancelGrappleSwing();
        }

        //if (CheckForVault(correctHit, onRightWall)) //CheckForVault() will call Vault() if it detects a hit
        //{
        //    return;
        //}

        if (CheckSnapToGround())
        {
            transform.position = new Vector2(transform.position.x, transform.position.y + groundSnapCorrection);
            playerState = PlayerStates.Grounded;
            return;
        }

        if (playerDirection == PlayerDirection.Left && onRightWall)
        {
            playerDirection = PlayerDirection.Right;
        }
        else if (playerDirection == PlayerDirection.Right && !onRightWall)
        {
            playerDirection = PlayerDirection.Left;
        }

            OffsetWallPlayerPosition(correctHit, onRightWall);
        EnterWall();
    }

    private void EnterWallHold(bool isLeftWall)
    {
        if (CheckForHeadhit())
        {
            gmScript.CancelGrapplePull();
            return;
        }

        gmScript.CancelGrapplePull();
        playerDirection = isLeftWall ? PlayerDirection.Left : PlayerDirection.Right;
        wallHoldPos = rb.position;
        playerState = PlayerStates.WallHold;
    }

    private void OffsetWallPlayerPosition(RaycastHit2D hit, bool onTheRight)
    {
        print(hit.point);
        Vector2 position = new Vector2(rb.position.x, hit.point.y);

        RaycastHit2D wallHit = Physics2D.Raycast(position, onTheRight ? Vector2.right : Vector2.left, wallBoxCastOffset + CalculateRaycastExtraLength(velocity.x), playerRaycastLayerMask);
        Vector2 wallPoint = wallHit.point;
        if (!wallHit)
        {
            return;
        }

        float newXCoord = onTheRight ? wallPoint.x - 0.4f : wallPoint.x + 0.4f;
        Vector3 newPosition = new Vector3(newXCoord, rb.position.y);
        transform.position = newPosition;
    }

    //VAULT
    //private bool CheckForVault(RaycastHit2D wallHit, bool hitOnRight)
    //{
    //    if (disableVault) { return false; }

    //    Vector2 direction = hitOnRight ? Vector2.right : Vector2.left;
    //    RaycastHit2D vaultHit = rb.Raycast(Vector2.zero, direction, wallBoxCastOffset + 0.5f, playerRaycastLayerMask);

    //    if (!vaultHit && wallHit.point.y <= rb.position.y)
    //    {
    //        StartCoroutine(Vault(hitOnRight, wallHit.point));
    //        return true;
    //    } else
    //    {
    //        return false;
    //    }
    //}

    #endregion

    #region Recieving Input

    /// <summary>
    /// Catches the horizontal movement input from the player
    /// </summary>
    /// <param name="context"></param>
    public void OnMove(InputAction.CallbackContext context)
    {
        if (playerState == PlayerStates.Grounded || playerState == PlayerStates.InAir)
        {
            //StartAcceleration(playerMoveInput.x);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        bool keyPressed = value == 1;

        if (keyPressed && playerState == PlayerStates.Grounded) //holding/pressed jump
        {
            playerState = PlayerStates.InAir;
            velocity.y += value * jumpForce;
            StartCoroutine(SetCannotGoOnWallTimer(banWallAfterJumpTimeSec));
        }
        else if (!keyPressed && playerState == PlayerStates.InAir) //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }
        else if (keyPressed && playerState == PlayerStates.GrappleSwing)
        {
            AddForce(gmScript.JumpOutOfGrapple(), true);
        }
        else if (keyPressed && playerState == PlayerStates.OnWall)
        {
            OnWallJumpInput();
        }
    }

    public void OnGrappleInput(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;
        print(gmScript.canGrapple);
        if (keyPressed && (playerState == PlayerStates.InAir || playerState == PlayerStates.OnWall) && gmScript.canGrapple)
        {
            playerState = PlayerStates.GrappleThrow;
            StartCoroutine(gmScript.GrappleThrowCoroutine());
        }
        else if (keyPressed && playerState == PlayerStates.GrappleSwing)
        {
            gmScript.StartGrapplePulling();
        }
        else if (!keyPressed && playerState == PlayerStates.GrapplePull)
        {
            velocity = Vector2.zero;
            AddForce(gmScript.CancelGrapplePull() / 1.2f, true);
            FindPlayerState(false);
        }
        else if (!keyPressed && playerState == PlayerStates.WallHold)
        {
            EnterWall();
        }
    }

    public void OnGrapplePullInput(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;

        if (keyPressed && (playerState == PlayerStates.InAir || playerState == PlayerStates.OnWall || playerState == PlayerStates.Grounded) && gmScript.canGrapple)
        {
            if (playerState == PlayerStates.Grounded)
            {
                gmScript.groundedGrapple = true;
            }

            playerState = PlayerStates.GrappleThrow;
            StartCoroutine(gmScript.GrappleThrowCoroutine());
            gmScript.grapplePullOnStartGrappling = true;
        }
        else if (keyPressed && playerState == PlayerStates.GrappleSwing)
        {
            gmScript.StartGrapplePulling();
        }
        else if (keyPressed && playerState == PlayerStates.GrappleThrow)
        {
            gmScript.grapplePullOnStartGrappling = true;
        }
        else if (!keyPressed && playerState == PlayerStates.GrapplePull)
        {
            velocity = Vector2.zero;
            AddForce(gmScript.CancelGrapplePull() / 1.2f, true);
            FindPlayerState(false);
        }
        else if (!keyPressed && playerState == PlayerStates.WallHold)
        {
            EnterWall();
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

    private void ApplyAdditionalVerticalVelocity()
    {
        velocity.y += additionalVelocity.y;
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
                //Time.timeScale = 0.25f; 
                additionalVelocity.x += accelerationValue;
                //StopAcceleration();
            }

            //postGrappleVelocity = new Vector2(Mathf.Max(postGrappleVelocity.x, 0), Mathf.Max(postGrappleVelocity.y, 0));
        }
    }

    #endregion

    #region On Wall

    private void OnWallJumpInput()
    {
        //if (playerDirectionalInput.y > 0)
        //{
        //    StartCoroutine(WallClimb());
        //}
        //else
        //{
        //    WallJump();
        //}

        WallJump();
    }

    private void EnterWall()
    {
        playerState = PlayerStates.OnWall;

        TweenWallSlideSpeed();
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

    //private IEnumerator WallClimb()
    //{
    //    LeanTween.cancel(gameObject);
    //    playerState = PlayerStates.WallClimb;
    //    StartCoroutine(SetBanMoveTimer(onRightWall, !onRightWall, climbTime));

    //    yield return new WaitForSeconds(climbTime);

    //    if (playerState != PlayerStates.Vault)
    //    {
    //        playerState = PlayerStates.OnWall;
    //        velocity.y = 0;
    //        TweenWallSlideSpeed();
    //    }
    //}

    private void WallJump()
    {
        LeaveWall(true);
        ResetAirAbilities();
        //StopAcceleration();

        if (playerDirectionalInput.x != 0)
        {
            StartAcceleration(playerDirectionalInput.x);
        }
        //StartAcceleration(playerMoveInput.x);
        //additionalVelocity = wallJumpVelocity;
        //additionalVelocity.x = onRightWall ? -additionalVelocity.x : additionalVelocity.x;
        Vector2 velocity = playerDirection == PlayerDirection.Right ? new Vector2(-wallJumpVelocity.x, wallJumpVelocity.y) : wallJumpVelocity;
        AddForce(velocity, false);
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

    //private IEnumerator Vault(bool onRightWall, Vector2 hitPoint)
    //{
    //    playerState = PlayerStates.Vault;

    //    //Part 1: Move the player up
    //    velocity = Vector2.zero;
    //    float yOffset = groundBoxCastYOffset - (transform.position.y - hitPoint.y);
    //    transform.position = new Vector2(transform.position.x, transform.position.y + yOffset);

    //    yield return new WaitForSeconds(vaultTime / 2);

    //    //Part 2: Move the player right/left
    //    float newX = onRightWall ? transform.position.x + wallBoxCastOffset * 2 : transform.position.x - wallBoxCastOffset * 2;
    //    Vector2 newPos = new Vector2(newX, transform.position.y);

    //    LeanTween.move(gameObject, newPos, vaultTime / 2)
    //        .setOnUpdate((float nothing) =>
    //        {
    //            if (playerState != PlayerStates.Vault)
    //            {
    //                LeanTween.cancel(gameObject);
    //            }
    //        });
    //    yield return new WaitForSeconds(vaultTime / 2);
    //    playerState = playerState == PlayerStates.Vault ? PlayerStates.Grounded : playerState;
    //}

    #endregion

    #region Grappler

    public void JumpOutOfGrapple()
    {
        playerState = PlayerStates.InAir;
        Vector2 grappleTransferVelocity = gmScript.SetPostGrappleVelocity();
        AddForce(grappleTransferVelocity, true);
        transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, 0);
    }

    #endregion
}
