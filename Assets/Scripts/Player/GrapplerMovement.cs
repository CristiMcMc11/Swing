using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerMovement playerMovementScript;

    [Header("Grappler Swing Runtime")]
    [SerializeField] private Vector2 grappleDirectionalInput;
    [SerializeField] private float grappleDistance;
    [SerializeField] private Vector2 grapplePoint;

    [SerializeField] private Vector2 directionFromPrevPoint;
    [SerializeField] private float grappleSpeed;
    [SerializeField] private float originalAngleSpeed;
    [SerializeField] private float speedEquationFactor;
    [SerializeField] private bool movingRight = true;

    [Header("Grappler Settables")]
    [SerializeField] private float maxGrappleDistance = 20;
    [SerializeField] private float maxGrappleThrowTime = 0.5f;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovementScript = GetComponent<PlayerMovement>();
    }

    public void SetGrappleDirectionalInput(InputAction.CallbackContext context)
    {
        grappleDirectionalInput = context.ReadValue<Vector2>();
    }

    #region Grappler Swing Logic

    public IEnumerator GrappleThrowCoroutine()
    {
        int layerMask = LayerMask.GetMask("Terrain");
        bool grappleHit = true;

        grapplePoint = FindGrapplePoint(ref grappleHit);
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
            yield return new WaitForSeconds(time / 2);
            playerMovementScript.playerState = PlayerMovement.PlayerStates.InAir;
        }
    }

    private void StartGrappleSwinging()
    {
        grappleSpeed = VelocityToAngleSpeed(playerMovementScript.GetVelocity(), grappleDistance);
        //grappleSpeed = velocity.x > 0 ? grappleSpeed : -grappleSpeed;
        playerMovementScript.playerState = PlayerMovement.PlayerStates.GrappleSwinging;
    }

    private Vector2 FindGrapplePoint(ref bool grappleHit)
    {
        int layerMask = LayerMask.GetMask("Terrain");

        if (rb.Raycast(grappleDirectionalInput, maxGrappleDistance, layerMask))
        {
            return rb.RaycastReturnPoint(grappleDirectionalInput, maxGrappleDistance, layerMask);
        }
        else
        {
            grappleHit = false;
            return Vector2.zero;
        }
    }

    /// <summary>
    /// Calculates the point the player should move to next while grapple swinging.
    /// </summary>
    /// <param name="distance"></param>
    /// <param name="grapplePoint"></param>
    /// <param name="angleToMove"></param>
    /// <returns> The point the player should move to next </returns>
    private Vector2 GrappleSwingNextPosition(float distance, Vector2 grapplePoint, float angleToMove)
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

    public Vector2 GrappleSwingMovement()
    {
        float currentPlayerAngle = FindCurrentPlayerAngleRad(grapplePoint, grappleDistance);
        Vector2 newPosition = GrappleSwingNextPosition(grappleDistance, grapplePoint, CalculateAngleSpeed(currentPlayerAngle));
        return newPosition;
    }

    public Vector2 SetPostGrappleVelocity()
    {
        return AngleSpeedToVelocity(grappleSpeed, grappleDistance, playerMovementScript.grapplerDirectionFromPrevPoint);
    }

    #endregion

    #region Grappler Swing Calculations

    private float VelocityToAngleSpeed(Vector2 playerVelocity, float radius)
    {
        //1. Find the downward velocity of the player, or zero if the player is moving up
        float yVelocity = playerVelocity.y; //We know that the player must move this distance on the circle
        yVelocity = playerVelocity.x >= 0 ? yVelocity : -yVelocity;

        //2. Find the angle in radians needed to move the player that distance on the circle
        float angleSpeedRad = -(yVelocity / radius);
        return angleSpeedRad;
    }

    private Vector2 AngleSpeedToVelocity(float angleSpeed, float radius, Vector2 directionFromPrevPoint)
    {
        //Find the distance of an arc with the radius and the angle
        float magnitude = angleSpeed * radius;

        Vector2 velocity = directionFromPrevPoint * magnitude;
        velocity.y = angleSpeed < 0 ? -velocity.y : velocity.y;
        velocity.x = directionFromPrevPoint.x < 0 ? -velocity.x : velocity.x;

        return velocity;
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
        float gravityAngleSpeed = VelocityToAngleSpeed(new Vector2(0, -playerMovementScript.gravity), grappleDistance);

        //If the player is on the right, add gravityAngleSpeed. If the player is on the right, subtract gravityAngleSpeed
        grappleSpeed = isOnTheRight ? grappleSpeed + gravityAngleSpeed * Time.fixedDeltaTime : grappleSpeed - gravityAngleSpeed * Time.fixedDeltaTime;
        return grappleSpeed;
    }

    private float CalculateGrappleThrowTime(float distance, bool grappleHit)
    {
        if (grappleHit)
        {
            return (distance / maxGrappleDistance) * maxGrappleThrowTime;
        }
        return maxGrappleThrowTime;
    }

    #endregion
}
