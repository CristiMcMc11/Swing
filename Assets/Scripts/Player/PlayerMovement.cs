using System.Collections;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Runtime")]
    public PlayerStates playerState  = PlayerStates.Grounded;
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

    [Header("Grapple Swing")]
    [SerializeField] private Vector2 directionToGrapplePoint;
    [SerializeField] private float grappleDistance;
    [SerializeField] private Vector2 grapplePoint;

    [SerializeField] private float grappleSpeed;
    [SerializeField] private float originalAngleSpeed;
    [SerializeField] private float speedEquationFactor;
    [SerializeField] private bool movingRight = true;

    [SerializeField] private float maxGrappleDistance = 20;
    [SerializeField] private float maxGrappleThrowTime = 0.5f;
    [SerializeField] private float grappleTurnThreshold = 0.02f;
    [SerializeField] private float dampeningFactor = 0.1f;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {

    }

    private void FixedUpdate()
    {
        ApplyMovement();
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
                velocity.x = speed;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.InAir:
                //Use gravity and horizontal input
                ApplyGravity();
                velocity.x = speed;
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.GrappleThrow:
                //Gravity but no horizontal input
                ApplyGravity();
                rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
                break;

            case PlayerStates.GrappleSwinging:
                //Special grapple stuff
                float currentPlayerAngle = FindCurrentPlayerAngleRad(grapplePoint, grappleDistance);
                rb.MovePosition(GrappleSwingMovement(grappleDistance, grapplePoint, CalculateAngleSpeed(currentPlayerAngle)));
                break;
        }
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
        directionToGrapplePoint = moveInput.normalized;
        speed = walkSpeed * moveInput.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();

        if (value == 1 && playerState == PlayerStates.Grounded) //holding/pressed jump
        {
            velocity.y += value * jumpForce / 2;
        }
        else if (value == 0 && playerState == PlayerStates.InAir) //let go of jump
        {
            velocity.y = velocity.y < 0 ? velocity.y : velocity.y / 4; //unchanged if velocity.y is negative, and divided by 4 if velocity.y is positive
        }

        //if (value == 1 && playerState == PlayerStates.GrappleSwinging)
        //{
        //    JumpOutOfGrapple();
        //}
    }

    #endregion

    #region Air

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;

        //velocity.y = Mathf.Max(velocity.y, gravity / 2f);
    }

    #endregion

    #region Grappler

    public void Grapple(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;

        if (keyPressed && playerState == PlayerStates.InAir)
        {
            playerState = PlayerStates.GrappleThrow;
            StartCoroutine(GrappleThrowCoroutine());
        }
    }

    private IEnumerator GrappleThrowCoroutine()
    {
        int layerMask = LayerMask.GetMask("Terrain");
        bool grappleHit = true;

        grapplePoint = FindGrapplePoint(ref grappleHit);
        print(grapplePoint);
        float tempDistance = Vector2.Distance(rb.position, grapplePoint);
        float time = CalculateGrappleThrowTime(tempDistance, grappleHit);

        //animation for throwing grapple
        yield return new WaitForSeconds(time);

        grappleDistance = Vector2.Distance(rb.position, grapplePoint);

        if (grappleHit)
        {
            StartGrappleSwinging();
        }
        else
        {
            yield return new WaitForSeconds(time/2);
            playerState = PlayerStates.InAir;
        }
    }

    private void StartGrappleSwinging()
    {
        grappleSpeed = VelocityToAngleSpeed(velocity, grappleDistance);
        playerState = PlayerStates.GrappleSwinging;
    }

    private Vector2 FindGrapplePoint(ref bool grappleHit)
    {
        int layerMask = LayerMask.GetMask("Terrain");

        if (rb.Raycast(directionToGrapplePoint, maxGrappleDistance, layerMask))
        {
            return rb.RaycastReturnPoint(directionToGrapplePoint, maxGrappleDistance, layerMask);
        }
        else
        {
            grappleHit = false;
            return Vector2.zero;
        }
    }

    private float CalculateGrappleThrowTime(float distance, bool grappleHit)
    {
        if (grappleHit)
        {
            return (distance / maxGrappleDistance) * maxGrappleThrowTime;
        }
        return maxGrappleThrowTime;
    }

    private void TweenVelocityToZero(float time)
    {
        LeanTween.value(gameObject, velocity, Vector2.zero, time / 2)
            .setOnUpdate((Vector2 val) =>
            {
                velocity = val;
            });
    }

    /// <summary>
    /// Calculates the point the player should move to next while grapple swinging.
    /// </summary>
    /// <param name="distance"></param>
    /// <param name="grapplePoint"></param>
    /// <param name="angleToMove"></param>
    /// <returns> The point the player should move to next </returns>
    private Vector2 GrappleSwingMovement(float distance, Vector2 grapplePoint, float angleToMove)
    {
        angleToMove *= Time.fixedDeltaTime;

        Vector2 prevPoint = rb.position;
        float radius = Vector2.Distance(grapplePoint, prevPoint);

        // 1. Get current angle in radians
        float currentAngle = Mathf.Atan2(prevPoint.y - grapplePoint.y, prevPoint.x - grapplePoint.x);

        // 2. Add move angle
        float newAngle = currentAngle + angleToMove;

        // 3. Calculate new point
        float x = grapplePoint.x + radius * Mathf.Cos(newAngle);
        float y = grapplePoint.y + radius * Mathf.Sin(newAngle);

        return new Vector2(x, y);
    }

    private float VelocityToAngleSpeed(Vector2 playerVelocity, float radius)
    {
        //1. Find the downward velocity of the player, or zero if the player is moving up
        float yVelocity = playerVelocity.y; //We know that the player must move this distance on the circle

        //2. Find the angle in radians needed to move the player that distance on the circle
        float angleSpeedRad = -(yVelocity / radius);
        return angleSpeedRad;
    }

    /// <summary>
    /// Finds the current angle in radians of the player on a grapple.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <returns> An angle in radians from 0 to 2pi. </returns>
    private float FindCurrentPlayerAngleRad(Vector2 center, float radius)
    {
        // 1. Get the direction vector from center to point
        Vector2 direction = rb.position - center;

        // 2. Calculate angle in radians 
        // Returns a value between -PI and PI (-3.14 to 3.14)
        float angleRadians = Mathf.Atan2(direction.y, direction.x);

        // 3.Normalize to 0 to 2*PI range
        if (angleRadians < 0)
        {
            angleRadians += 2 * Mathf.PI;
        }

        return angleRadians;
    }

    /// <summary>
    /// Calculates the angle speed for one frame of grapple movement. This is done by adding gravity as an anglespeed to the grappleSpeed.
    /// </summary>
    /// <param name="speedEquationFactor"></param>
    /// <param name="currentPlayerAngleRad"></param>
    /// <param name="radius"></param>
    /// <returns> A float, the angleSpeed.</returns>
    private float CalculateAngleSpeed(float currentPlayerAngleRad)
    {
        bool isOnTheRight = currentPlayerAngleRad * Mathf.Rad2Deg >= 270 || currentPlayerAngleRad * Mathf.Rad2Deg <= 90;
        float gravityAngleSpeed = VelocityToAngleSpeed(new Vector2(0, -gravity), grappleDistance);

        //If the player is on the right, add gravityAngleSpeed. If the player is on the right, subtract gravityAngleSpeed
        grappleSpeed = isOnTheRight ? grappleSpeed + gravityAngleSpeed * Time.fixedDeltaTime : grappleSpeed - gravityAngleSpeed * Time.fixedDeltaTime;
        return grappleSpeed; 
    }
    #endregion
}
