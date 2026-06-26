using System;
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
        ClassMovement
    }

    public enum PlayerDirection
    {
        Left,
        Right
    }

    public enum InputBan
    {
        None,
        Left,
        Right,
        All
    }

    //References
    private Rigidbody2D rb;
    private GrapplerMovement gmScript;

    private GameObject visualsGO;
    private PlayerVisuals visualsScript;


    public static event Action OnPlayerStateChanged;
    [SerializeField] private PlayerStates _playerState = PlayerStates.Grounded;

    [Header("Runtime")]
    public PlayerStates PlayerState
    {
        get => _playerState;
        set
        {
            if (_playerState == value) return;
            _playerState = value;
            OnPlayerStateChanged?.Invoke();
        }
    }

    public PlayerDirection playerDirection = PlayerDirection.Right; //SET GET; PRIVATE SET

    [SerializeField] public Vector2 velocity;
    [SerializeField] private Vector2 additionalVelocity;

    [SerializeField] private bool playerCannotMove = false;

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

    [Header("Inputs")]
    [SerializeField] private Vector2 directionalInput = Vector2.zero;
    [SerializeField] private InputBan inputBan = InputBan.None;
    [SerializeField] private bool jumpKeyDown = false;

    [Header("Ground")]
    public float walkSpeed = 10;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private Vector2 newPos = Vector2.zero;

    [Header("Jumping/Air")]
    public float maxJumpHeight = 5f;
    public float maxJumpTime = 1f; 
    public float terminalVelocity = -20;
    [SerializeField] private float banWallAfterJumpTimeSec = 0.2f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);

    [Header("Acceleration")]
    public float accelerationTime = 0.5f; //time to get to walkSpeed
    public float airResistance = 1f; //units a second;
    [SerializeField] private float neutralAirResistance = 0.1f;
    public float groundFriction = 5f;
    public bool instantAccelerate = false;
    [SerializeField] private bool instantTurn = true;
    [SerializeField] private float accelerationValue;
    [SerializeField] private float inputBanTime = 0.1f;
    private int accelerationTweenId;

    [Header("Wall")]
    [SerializeField] private bool onRightWall;
    [SerializeField] private float wallVelocity = 0f;
    [SerializeField] private bool canGoOnWall = true;
    [SerializeField] private bool isWallClimbing = true;

    public float minSlideSpeed = 0.2f;
    public float maxSlideSpeed = 1f;
    public float slideSpeedTweenTime = 0.4f;
    [SerializeField] private float climbHeight = 15;
    [SerializeField] private float climbTime = 0.1f;
    public Vector2 wallJumpVelocity = new Vector2(10, 10);
    [SerializeField] private float wallJumpInputBanTime = 0.2f;

    [SerializeField] private float oppositeInputTime = 0.3f;
    [SerializeField] private bool timingOppositeInput = false;

    [SerializeField] private float vaultTime = 0.25f;
    public bool disableVault = false;

    [Header("Extra Velocity")]
    [SerializeField] private float PGVxDecayFactor = 0.5f;
    [SerializeField] private float PGVyDecayFactor = 7f;
    public Vector2 grapplerDirectionFromPrevPoint { get; private set; }

    [Header("Animation")]
    private Animator animator;

    [Header("testing/misc")]
    public float gameSpeed;
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
        CheckForHeadhit();
        //DecayAdditionalVelocity();
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

    public void SetInputBan(InputBan inputBan)
    {
        this.inputBan = inputBan;
    }

    private void ResetAirAbilities()
    {
        gmScript.ResetGrapples();
    }

    private IEnumerator SetBanMoveTimer(bool leftBan, float timeSec)
    {
        StopCoroutine(SetBanMoveTimer(leftBan, timeSec));
        inputBan = InputBan.None;
        inputBan = leftBan ? InputBan.Left : InputBan.Right;
        yield return new WaitForSeconds(timeSec);
        inputBan = InputBan.None;
    }

    public void AddForce(Vector2 force, bool resetVelocityBefore)
    {
        velocity = resetVelocityBefore ? Vector2.zero : velocity;
        velocity += force;
    }

    private void SetVelocity()
    {
        switch (PlayerState)
        {
            case PlayerStates.Grounded:
                GroundMovement();
                break;

            case PlayerStates.InAir:
                AirMovement();
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
        switch (PlayerState)
        {
            case PlayerStates.Grounded:
                //Get horizontal input to move but don't use gravity
                newPos = rb.position + velocity * Time.fixedDeltaTime;
                rb.MovePosition(newPos);
                break;

            case PlayerStates.InAir:
                //Use gravity and horizontal input
                newPos = rb.position + velocity * Time.fixedDeltaTime;
                rb.MovePosition(newPos);
                break;

            case PlayerStates.OnWall:
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.ClassMovement:
                gmScript.SetPlayerMovement(); //TEMP, later make it whatever class is currently equipped
                break;
        }
    }

    private void FindRotations()
    {
        if (PlayerState == PlayerStates.Grounded || PlayerState == PlayerStates.InAir)
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
        int yRotation = playerDirection == PlayerDirection.Left ? 180 : 0;
        visualsGO.transform.rotation = Quaternion.Euler(0, yRotation, transform.rotation.z);
        //rb.rotation = zRotation;
        //transform.rotation = Quaternion.Euler(0, yRotation, rb.rotation);
    }

    private void StopAcceleration()
    {
        LeanTween.cancel(accelerationTweenId);
        accelerationValue = 0;
    }

    #region Movement Types
    public void MovePlayerByVelocity()
    {
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }

    public void GroundMovement()
    {
        float playerInput = FindPlayerInput();
        float accelerationRate = FindAccelerationRate();

        if (Mathf.Abs(velocity.x) > walkSpeed)
        {
            velocity.x -= groundFriction * Mathf.Sign(velocity.x) * Time.fixedDeltaTime;
        }
        else
        {
            if (Mathf.Sign(playerInput) != Mathf.Sign(velocity.x) && playerInput != 0) //opposite input to velocity
            {
                velocity.x -= accelerationRate * Mathf.Sign(velocity.x) * 2;
            }
            else if (playerInput > 0) //input is right
            {
                velocity.x = Mathf.Min(velocity.x + accelerationRate, walkSpeed);
            }
            else if (playerInput < 0) //input is left
            {
                velocity.x = Mathf.Max(velocity.x - accelerationRate, -walkSpeed);
            }
            else if (playerInput == 0) //input is neutral
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
    }

    public void AirMovement()
    {
        float playerInput = FindPlayerInput();
        float accelerationRate = FindAccelerationRate();
        ApplyGravity();

        if (Mathf.Abs(velocity.x) > walkSpeed)
        {
            velocity.x -= airResistance * Mathf.Sign(velocity.x);

            if (Mathf.Sign(playerInput) != Mathf.Sign(velocity.x) && playerInput != 0)
            {
                velocity.x -= accelerationRate * Mathf.Sign(velocity.x);
            }
        }
        else
        {
            if (playerInput > 0)
            {
                velocity.x = Mathf.Min(velocity.x + accelerationRate, walkSpeed);
            }
            else if (playerInput < 0)
            {
                velocity.x = Mathf.Max(velocity.x - accelerationRate, -walkSpeed);
            }
            else if (playerInput == 0)
            {
                if (velocity.x != 0)
                {
                    velocity.x = velocity.x > 0 ? Mathf.Clamp(velocity.x - neutralAirResistance, 0, walkSpeed) : Mathf.Clamp(velocity.x + neutralAirResistance, -walkSpeed, 0);
                }
            }
        }
    }

    private float FindPlayerInput()
    {
        float playerInput = directionalInput.x;
        if (inputBan == InputBan.Left)
        {
            playerInput = Mathf.Max(0, playerInput);
        }
        else if (inputBan == InputBan.Right)
        {
            playerInput = Mathf.Min(0, playerInput);
        }
        else if (inputBan == InputBan.All)
        {
            playerInput = 0;
        }
            return playerInput;
    }

    private float FindAccelerationRate()
    {
        float accelerationRate = (walkSpeed / accelerationTime) * Time.fixedDeltaTime;
        if (instantAccelerate)
        {
            accelerationRate = Mathf.Max(velocity.x, walkSpeed);
        }
        return accelerationRate;
    }

    #endregion

    #region Player States

    public void FindPlayerState(bool requireWallCorrectDirectionalInput)
    {
        RaycastHit2D groundedHit = CheckForGrounded();
        RaycastHit2D leftWallHit = CheckForWallTouch(true);
        RaycastHit2D rightWallHit = CheckForWallTouch(false);

        bool canGround = groundedHit && !gmScript.groundedGrapple;
        bool wallHit = leftWallHit || rightWallHit;

        bool playerIsCorrectStateforWall = PlayerState == PlayerStates.InAir;
        bool playerHasCorrectDirectionalInput = (leftWallHit && directionalInput.x < 0) || (rightWallHit && directionalInput.x > 0);
        bool playerHasCorrectVelocity = (Mathf.Sign(velocity.x) == 1 && rightWallHit) || (Mathf.Sign(velocity.x) == -1 && leftWallHit);

        if (canGround && wallHit) //both ground and wall hit
        {
            OnWallAndGroundHit(groundedHit, leftWallHit, rightWallHit, playerHasCorrectDirectionalInput);
        }
        else if (!canGround && !wallHit && PlayerState == PlayerStates.Grounded) //not hitting anything but state is still grounded
        {
            PlayerState = PlayerStates.InAir;
        }
        else if (canGround && PlayerState == PlayerStates.InAir && velocity.y < 0) //grounded hit while falling
        {
            BecomeGrounded(groundedHit);
        }
        else if (wallHit && !canGround && playerIsCorrectStateforWall && (playerHasCorrectDirectionalInput || !requireWallCorrectDirectionalInput || playerHasCorrectVelocity) && canGoOnWall && !CheckForHeadhit()) //wall hit and player can go on wall
        {
            OnWallHit(leftWallHit, rightWallHit);
        }
        else if (wallHit && PlayerState == PlayerStates.WallClimb) //Checking for a vault while wall climbing
        {
            onRightWall = rightWallHit ? true : false;
            RaycastHit2D correctHit = onRightWall ? rightWallHit : leftWallHit;
            //CheckForVault(correctHit, onRightWall);
        }
        else if (PlayerState == PlayerStates.OnWall && ((onRightWall && directionalInput.x < 0) || (!onRightWall && directionalInput.x > 0)) && !timingOppositeInput) //checking whether input doesn't match the wall direction
        {
            StartCoroutine(StartOppositeInputTimer());
        }
        else if (!wallHit && PlayerState == PlayerStates.OnWall) //fell off the wall
        {
            LeaveWall(false);
        }
    }

    private void OnWallAndGroundHit(RaycastHit2D groundHit, RaycastHit2D leftWallHit, RaycastHit2D rightWallHit, bool playerHasCorrectDirectionalInput)
    {
        if (leftWallHit && rightWallHit)
        {
            BecomeGrounded(groundHit);
        }
        else if (((groundHit.point.x > transform.position.x && rightWallHit) || (groundHit.point.x < transform.position.x && leftWallHit)))
        {
            if (PlayerState == PlayerStates.InAir && playerHasCorrectDirectionalInput)
            {
                OnWallHit(leftWallHit, rightWallHit);
            }
        }
        else if (Mathf.Round(groundHit.point.x * 100) == Mathf.Round(rb.position.x * 100) && (PlayerState == PlayerStates.OnWall || (PlayerState == PlayerStates.InAir && velocity.y < 0)))
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
        PlayerState = PlayerStates.Grounded;
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

        if (hit && velocity.y > 0 && !(CheckForWallTouch(true) || CheckForWallTouch(false)))
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
    
    public void OnWallHit(RaycastHit2D hitLeft, RaycastHit2D hitRight)
     {
        RaycastHit2D correctHit;
        onRightWall = hitRight ? true : false;
        correctHit = onRightWall ? hitRight : hitLeft;

        //if (CheckForVault(correctHit, onRightWall)) //CheckForVault() will call Vault() if it detects a hit
        //{
        //    return;
        //}

        if (CheckSnapToGround())
        {
            transform.position = new Vector2(transform.position.x, transform.position.y + groundSnapCorrection);
            PlayerState = PlayerStates.Grounded;
            return;
        }

        if (velocity.y >= 0) //cuz you can't wall jump if moving upwards
        {
            velocity.x = 0;
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

    private void OffsetWallPlayerPosition(RaycastHit2D hit, bool onTheRight)
    {
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
        if (PlayerState == PlayerStates.Grounded || PlayerState == PlayerStates.InAir)
        {
            //StartAcceleration(playerMoveInput.x);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        bool keyPressed = value == 1;

        if (keyPressed && PlayerState == PlayerStates.Grounded) //holding/pressed jump
        {
            PlayerState = PlayerStates.InAir;
            velocity.y += value * jumpForce;
            //StartCoroutine(SetCannotGoOnWallTimer(banWallAfterJumpTimeSec));
        }
        else if (!keyPressed && PlayerState == PlayerStates.InAir) //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }
        else if (keyPressed && PlayerState == PlayerStates.OnWall)
        {
            OnWallJumpInput();
        }
    }

    public void DirectionalInput(InputAction.CallbackContext context)
    {
        directionalInput = context.ReadValue<Vector2>();
    }
    #endregion

    #region Air

    public void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, terminalVelocity);
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

    public void EnterWall()
    {
        PlayerState = PlayerStates.OnWall;

        TweenWallSlideSpeed();
    }

    private void LeaveWall(bool wallJump)
    {
        velocity.y = additionalVelocity.y;
        if (wallJump)
        {
            StartCoroutine(SetBanMoveTimer(!onRightWall, wallJumpInputBanTime));
        }
        StartCoroutine(SetCannotGoOnWallTimer(0.05f));
        PlayerState = PlayerStates.InAir;
    }

    /// <summary>
    /// Starts a timer of oppositeInputTime seconds where player must hold the opposite direction input to the wallthe whole time. 
    /// If the player does, they will leave the wall
    /// </summary>
    private IEnumerator StartOppositeInputTimer()
    {
        float startTime = Time.time;
        bool inputLeft = directionalInput.x < 0 ? true : false;
        bool completedTimer = true;
        timingOppositeInput = true;

        while (Time.time - startTime <= oppositeInputTime)
        {
            if (inputLeft && directionalInput.x > 0 || !inputLeft && directionalInput.x < 0 || directionalInput.x == 0)
            {
                timingOppositeInput = false;
                completedTimer = false;
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (completedTimer) LeaveWall(false);
        timingOppositeInput = false;
    }

    private void WallJump()
    {
        LeaveWall(true);
        ResetAirAbilities();
        //StopAcceleration();
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

        float minSpeed = Mathf.Max(-velocity.y / 2.5f, minSlideSpeed);
        print(minSpeed);
        if (minSpeed > maxSlideSpeed)
        {
            wallVelocity = -minSpeed;
        }
        else
        {
            float time = slideSpeedTweenTime / (maxSlideSpeed - minSpeed);

            LeanTween.value(-minSpeed, -maxSlideSpeed, time)
                .setOnUpdate((float value) =>
                {
                    wallVelocity = value;
                });
        }
            
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

}
