using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class RollerMovement : MonoBehaviour
{
    //References
    private PlayerMovement pmScript;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private CircleCollider2D circleCollider;
    private GameObject visualsGO;

    public enum RollerState
    {
        None,
        Windup,
        RollingGround,
        RollingAir
    }

    public enum WallSide
    {
        Bottom,
        Top,
        Left,
        Right,
        None
    }

    public enum Direction
    {
        Down,
        Up,
        Left,
        Right
    }


    [Header("Runtime")]
    [SerializeField] private Vector2 velocity;
    [SerializeField] private RollerState rollerState = RollerState.None;
    [SerializeField] private WallSide side = WallSide.Bottom;
    [SerializeField] private WallSide prevSide = WallSide.Bottom;
    [SerializeField] private Direction direction = Direction.Right;
    [SerializeField] float currentSpeed;
    [SerializeField] bool canBounce = true;
    [SerializeField] bool holdingJump = true;

    public bool onBottomTop => side == WallSide.Bottom || side == WallSide.Top;
    public bool onLeftRight => side == WallSide.Left || side == WallSide.Right;

    [Header("Settables")]
    [SerializeField] private float jumpForce = 10;
    [SerializeField] private float terminalVelocity = -10;
    [SerializeField] private float minSpeed = 12;
    [SerializeField] private float rollerWindup = 0.1f;
    [SerializeField] private float rollerAirAccelTime = 1.5f;
    [SerializeField] private float bounceCooldown = 0.2f;
    private float accelRate => rollerAirAccelTime / minSpeed;

    [Header("Raycasting Vars")]
    [SerializeField] private float circleRadius;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circleCollider = GetComponent<CircleCollider2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        pmScript = GetComponent<PlayerMovement>();
        visualsGO = transform.Find("Visuals").gameObject;
        currentSpeed = minSpeed;

        circleRadius = circleCollider.radius;
    }

    private void FixedUpdate()
    {
        if (rollerState == RollerState.RollingGround || rollerState == RollerState.RollingAir)
        {
            FindSide();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        //Bottom
        Vector2 size = new Vector2(circleRadius * 2 - 0.2f, 0.2f);
        Vector2 position = rb.position - new Vector2(0, circleRadius);
        Gizmos.DrawWireCube(position, size);

        //Top
        position = rb.position + new Vector2(0, circleRadius);
        Gizmos.DrawWireCube(position, size);

        //Left
        size = new Vector2(0.2f, circleRadius * 2 - 0.2f);
        position = rb.position - new Vector2(circleRadius, 0);
        Gizmos.DrawWireCube(position, size);

        //Right
        position = rb.position + new Vector2(circleRadius, 0);
        Gizmos.DrawWireCube(position, size);

        //check hit
        Gizmos.color = Color.red;
        size = new Vector2(circleRadius + 0.2f, circleRadius * 2);
        position = rb.position - new Vector2(circleRadius / 2, 0);
        Gizmos.DrawWireCube(position, size);
    }

    private IEnumerator StartRolling()
    {
        boxCollider.enabled = false;
        circleCollider.enabled = true;

        pmScript.checkPlayerState = false;
        pmScript.PlayerState = PlayerMovement.PlayerStates.ClassMovement;

        if (rollerWindup > 0)
        {
            rollerState = RollerState.Windup;
            yield return new WaitForSeconds(rollerWindup);
        }

        rollerState = RollerState.RollingGround;
        prevSide = WallSide.Bottom;
        direction = pmScript.playerDirection == PlayerMovement.PlayerDirection.Right ? Direction.Right : Direction.Left;
        velocity = direction == Direction.Right ? new Vector2(minSpeed, 0) : new Vector2(-minSpeed, 0);
    }


    private void StopRolling()
    {
        rollerState = RollerState.None;

        pmScript.checkPlayerState = true;
        pmScript.velocity = this.velocity;
        pmScript.PlayerState = PlayerMovement.PlayerStates.InAir;
    }

    private void ChangeSide(WallSide newSide, bool comingFromAir = false)
    {
        if (onBottomTop && (newSide == WallSide.Left || newSide == WallSide.Right))
        {
            velocity.y = velocity.x;
            velocity.x = 0;

            //Check top -> right or bot -> left transition and switch sign of velocity
            if (direction == Direction.Right && side == WallSide.Top && newSide == WallSide.Right || direction == Direction.Left && side == WallSide.Bottom && newSide == WallSide.Left)
            {
                velocity.y = -velocity.y;
            }
        }
        else if (onLeftRight && (newSide == WallSide.Top || newSide == WallSide.Bottom))
        {
            velocity.x = velocity.y;
            velocity.y = 0;

            //Check left -> bot or right -> top transition and switch sign of velocity
            if (direction == Direction.Up && side == WallSide.Right && newSide == WallSide.Top || direction == Direction.Down && side == WallSide.Left && newSide == WallSide.Bottom)
            {
                velocity.x = -velocity.x;
            }
        }
        else if (comingFromAir)
        {
            if (newSide == WallSide.Left || newSide == WallSide.Right)
            {
                velocity = new Vector2(0, Mathf.Max(5, velocity.y));
                direction = Direction.Up;
                rollerState = RollerState.RollingGround;
            }

            prevSide = newSide;
        }

        side = newSide;
    }

    #region Player Stuff

    public void SetPlayerMovement()
    {
        switch (rollerState)
        {
            case RollerState.RollingGround:
                GroundMovement();
                break;
            case RollerState.RollingAir:
                AirMovement();
                break;
        }

        if (rollerState == RollerState.RollingGround || rollerState == RollerState.RollingAir)
        {
            rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        }
    }

    #endregion

    #region Movement

    private void GroundMovement()
    {
        float input = onBottomTop ? Mathf.Round(pmScript.directionalInput.x) : Mathf.Round(pmScript.directionalInput.y);
        print(input);
        float speed = onBottomTop ? velocity.x : velocity.y;
        float maxSpeed = Mathf.Abs(velocity.x) > minSpeed ? Mathf.Abs(velocity.x) : minSpeed;
        float accelRate = this.accelRate / 2;

        bool opposite = input == 1 && (direction == Direction.Left || direction == Direction.Down) || input == -1 && (direction == Direction.Right || direction == Direction.Up);
        bool same = input == -1 && (direction == Direction.Left || direction == Direction.Down) || input == 1 && (direction == Direction.Right || direction == Direction.Up);

        //if (Mathf.Abs(speed) < minSpeed)
        //{
        //    speed = speed > 0 ? Mathf.Min(speed + accelRate, minSpeed) : Mathf.Max(speed - accelRate, -minSpeed);
        //}

        if (opposite) //opposite input to velocity
        {
            speed -= Mathf.Sign(speed) * accelRate;
        }
        else if (same) //same input to velocity
        {
            speed = direction == Direction.Left || direction == Direction.Down ? Mathf.Clamp(speed - accelRate, -maxSpeed, 0) : Mathf.Clamp(speed + accelRate, 0, maxSpeed);
        }
        else if (input == 0 && Mathf.Abs(speed) < minSpeed)
        {
            speed = speed > 0 ? Mathf.Min(speed + accelRate, minSpeed) : Mathf.Max(speed - accelRate, -minSpeed);
        }


        if (side == WallSide.Bottom || side == WallSide.Top)
        {
            velocity.x = speed;
        }
        else if (side == WallSide.Left || side == WallSide.Right)
        {
            velocity.y = speed;
        }        
    }

    private void AirMovement()
    {
        float input = Mathf.Round(pmScript.directionalInput.x);
        float maxSpeed = Mathf.Abs(velocity.x) > minSpeed ? Mathf.Abs(velocity.x) : minSpeed;

        if (direction != Direction.Left && direction != Direction.Right && input != 0)
        {
            direction = input < 0 ? Direction.Left : Direction.Right;
        }
        
        if (input == 1 && direction == Direction.Left || input == -1 && direction == Direction.Right) //opposite input to velocity
        {
            velocity.x = direction == Direction.Left ? velocity.x + accelRate : velocity.x - accelRate;
        }
        else if (input == 1 && direction == Direction.Right || input == -1 && direction == Direction.Left) //same input to velocity
        {
            velocity.x = direction == Direction.Left ? Mathf.Clamp(velocity.x - accelRate, -maxSpeed, 0) : Mathf.Clamp(velocity.x + accelRate, 0, maxSpeed);
        }

        velocity.y = Mathf.Max(velocity.y + pmScript.gravity * Time.fixedDeltaTime, terminalVelocity);

        //Finding Direction
        if (velocity.x < 0)
        {
            direction = Direction.Left;
        }
        else if (velocity.x > 0)
        {
            direction = Direction.Right;
        }
    }

    private void FindDirection()
    {
        if (rollerState == RollerState.RollingGround)
        {
            if (onBottomTop && velocity.x != 0)
            {
                direction = velocity.x < 0 ? Direction.Left : Direction.Right;
            }
            if (onLeftRight && velocity.y != 0)
            {
                direction = velocity.y < 0 ? Direction.Down : Direction.Up;
            }
        }
        else if (rollerState == RollerState.RollingAir)
        {
            if (velocity.x != 0)
            {
                direction = velocity.x < 0 ? Direction.Left : Direction.Right;
            }
        }
    }

    private void Bounce(RaycastHit2D leftHit, RaycastHit2D rightHit, RaycastHit2D topHit)
    {
        if (holdingJump && (leftHit || rightHit))
        {
            ChangeSide(leftHit ? WallSide.Left : WallSide.Right, true);
            return;
        }

        if (leftHit || rightHit)
        {
            velocity.x = -velocity.x;
            direction = direction == Direction.Left ? Direction.Right : Direction.Left;
        }
        else if (topHit)
        {
            velocity.y = -velocity.y;
        }
        StartCoroutine(BounceCooldown());
    }

    private void AddForce(Vector2 force)
    {
        velocity += force;
    }


    private void EnterRollGround()
    {
        velocity.y = 0;
        rollerState = RollerState.RollingGround;
        ChangeSide(WallSide.Bottom, true);
        FindDirection();
    }
    #endregion

    #region Raycasting

    private void FindSide()
    {
        Vector2 horizontalSize = new Vector2(circleRadius * 2 - 0.2f, 0.2f);
        Vector2 verticalSize = new Vector2(0.1f, circleRadius * 2 - 0.2f);
        LayerMask layerMask = pmScript.playerRaycastLayerMask;

        RaycastHit2D bottomHit = rb.BoxCast(new Vector2(0, -circleRadius), horizontalSize, 0, Vector2.up, 1, layerMask);
        RaycastHit2D topHit = rb.BoxCast(new Vector2(0, circleRadius), horizontalSize, 0, Vector2.up, 1, layerMask);
        RaycastHit2D leftHit = rb.BoxCast(new Vector2(-circleRadius, 0), verticalSize, 0, Vector2.up, 1, layerMask);
        RaycastHit2D rightHit = rb.BoxCast(new Vector2(circleRadius, 0), verticalSize, 0, Vector2.up, 1, layerMask);

        bool doubleHit = bottomHit && leftHit || bottomHit && rightHit;

        if (!bottomHit && !topHit && !leftHit && !rightHit)
        {
            rollerState = RollerState.RollingAir;
            ChangeSide(WallSide.None);
            prevSide = WallSide.None;
            return;
        }
        else if (rollerState == RollerState.RollingAir && doubleHit)
        {
            OnDoubleHit(bottomHit, leftHit, rightHit);
            return;
        }
        else if (rollerState == RollerState.RollingAir && bottomHit)
        {
            EnterRollGround();
            return;
        }
        else if (rollerState == RollerState.RollingAir && (topHit || leftHit || rightHit) && canBounce)
        {
            Bounce(leftHit, rightHit, topHit);
            return;
        }

        //Setting side
        if (bottomHit && side != WallSide.Bottom && prevSide != WallSide.Bottom) //bottom
        {
            ChangeSide(WallSide.Bottom);
        }
        else if (topHit && side != WallSide.Top && prevSide != WallSide.Top) //top
        {
            ChangeSide(WallSide.Top);
        }
        else if (prevSide == WallSide.Bottom && (leftHit && rightHit))
        {
            OnDoubleHit(bottomHit, leftHit, rightHit);
        }
        else if (leftHit && side != WallSide.Left && prevSide != WallSide.Left) //left
        {
            ChangeSide(WallSide.Left);
        }
        else if (rightHit && side != WallSide.Right && prevSide != WallSide.Right) //right
        {
            ChangeSide(WallSide.Right);
        }

        FindDirection();

        //Setting prevSide
        if ((!bottomHit && side != WallSide.Bottom && prevSide == WallSide.Bottom)
            || (!topHit && side != WallSide.Top && prevSide == WallSide.Top)
            || (!leftHit && side != WallSide.Left && prevSide == WallSide.Left)
            || (!rightHit && side != WallSide.Right && prevSide == WallSide.Right))
        {
            prevSide = side;
        }
    }

    private void OnDoubleHit(RaycastHit2D bottomHit, RaycastHit2D leftHit, RaycastHit2D rightHit)
    {
        Vector2 checkCastSize = new Vector2(circleRadius + 0.2f, circleRadius * 2);
        Vector2 checkCastOffset = new Vector2(circleRadius / 2, 0);

        RaycastHit2D checkHit;
        checkCastSize = new Vector2(circleRadius * 2, circleRadius + 0.2f);
        checkCastOffset = new Vector2(0, circleRadius / 2);

        checkHit = rb.BoxCast(checkCastOffset, checkCastSize, 0, Vector2.up, 1, pmScript.playerRaycastLayerMask);

        if (!checkHit) EnterRollGround(); else rollerState = RollerState.RollingAir;

        //if (leftHit && rightHit)
        //{
            
        //    return;
        //}
        //else if (leftHit)
        //{
        //    checkHit = rb.BoxCast(checkCastOffset, checkCastSize, 0, Vector2.up, 1, pmScript.playerRaycastLayerMask);
        //}
        //else
        //{
        //    checkHit = rb.BoxCast(-checkCastOffset, checkCastSize, 0, Vector2.up, 1, pmScript.playerRaycastLayerMask);
        //}

        //if (checkHit) EnterRollGround(); else rollerState = RollerState.RollingAir;
    }

    private IEnumerator BounceCooldown()
    {
        canBounce = false;
        yield return new WaitForSeconds(bounceCooldown);
        canBounce = true;
    }

    #endregion

    #region Input

    public void OnJumpInput(InputAction.CallbackContext context)
    {
        bool keyDown = context.ReadValue<float>() == 1;
        holdingJump = keyDown;

        if (rollerState == RollerState.RollingGround && keyDown)
        {
            print("jump");
            Vector2 force = Vector2.zero;
            switch(side)
            {
                case WallSide.Left:
                    force = new Vector2(jumpForce, 0);
                    direction = Direction.Right;
                    break;

                case WallSide.Right:
                    force = new Vector2(-jumpForce, 0);
                    direction = Direction.Left;
                    break;

                case WallSide.Bottom:
                    force = new Vector2(0, jumpForce);
                    break;

                case WallSide.Top:
                    force = new Vector2(0, -jumpForce);
                    break;
            }

            //prevSide = WallSide.None;
            ChangeSide(WallSide.None);
            AddForce(force);
        }
    }

    public void OnAbilityInput(InputAction.CallbackContext context)
    {
        if (pmScript.movementClass != PlayerMovement.MovementClass.Roller) return;

        if (rollerState == RollerState.None)
        {
            StartCoroutine(StartRolling());
        }
        else
        {
            StopRolling();
        }
    }

    #endregion
}
