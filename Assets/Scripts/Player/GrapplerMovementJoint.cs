using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using static PlayerMovement;

public class GrapplerMovementJoint : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerMovement pmScript;
    private DistanceJoint2D joint;
    [SerializeField] private Vector2 hitTerrainRaycastSize;
    public GameObject grappleHead;

    [Header("Grappler Swing Runtime")]
    [SerializeField] private Vector2 directionalInput;
    [SerializeField] private float grapplePointDistance;
    [SerializeField] private Vector2 grapplePoint;

    [SerializeField] private Vector2 directionFromPrevPoint;
    [SerializeField] public float grappleSpeed; //MAKE GET; PRIVATE SET
    [SerializeField] private float originalAngleSpeed;
    [SerializeField] private float speedEquationFactor;

    [SerializeField] private bool movingRight = true;
    [SerializeField] private bool canHitWall = true;
    [SerializeField] private bool inStillHang = false;

    public bool grapplePullOnStartGrappling = false;

    [Header("Grappler Settables")]
    [SerializeField] private float maxDistance = 20;

    [SerializeField] private float leniency = 5f;
    [SerializeField] private float maxThrowTime = 0.5f;
    [SerializeField] private float pullSpeed = 5f;
    [SerializeField] private float grappleEntrySpeedThreshold = 0.05f;


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.azure;
        Gizmos.DrawWireCube(transform.position, hitTerrainRaycastSize);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pmScript = GetComponent<PlayerMovement>();
        joint = GetComponent<DistanceJoint2D>();
        joint.enabled = false;
    }

    public void SetGrappleDirectionalInput(InputAction.CallbackContext context)
    {
        directionalInput = context.ReadValue<Vector2>();
    }

    public IEnumerator GrappleThrowCoroutine()
    {
        int layerMask = LayerMask.GetMask("Terrain");
        bool grappleHit = true;

        if (directionalInput == Vector2.zero)
        {
            directionalInput = pmScript.playerDirection == PlayerMovement.PlayerDirection.Right ? Vector2.right : Vector2.left;
        }

        grapplePoint = FindGrapplePoint(ref grappleHit, directionalInput);
        float tempDistance = Vector2.Distance(rb.position, grapplePoint);
        float time = CalculateGrappleThrowTime(tempDistance, grappleHit);

        //animation for throwing grapple

        grappleHead.SetActive(true);
        grappleHead.transform.position = transform.position;
        grappleHead.LeanMove(grapplePoint, time)
            .setOnComplete(() =>
            {
                if (!grappleHit)
                {
                    grappleHead.LeanMove(transform.position, time)
                    .setOnComplete(() =>
                    {
                        grappleHead.SetActive(false);
                    });
                }
            });

        yield return new WaitForSeconds(grappleHit ? time : time*2);

        grapplePointDistance = Vector2.Distance(rb.position, grapplePoint);

        if (grappleHit)
        {
            if (grapplePullOnStartGrappling)
            {
                StartGrapplePulling();
                grapplePullOnStartGrappling = false;
            }
            else
            {
                StartGrappleSwinging();
            }
        }
        else
        {
            yield return new WaitForSeconds(time / 2);
            pmScript.playerState = PlayerMovement.PlayerStates.InAir;
        }
    }

    #region Raycasting

    private Vector2 FindGrapplePoint(ref bool grappleHit, Vector2 directionalInput)
    {
        LayerMask layerMask = pmScript.playerRaycastLayerMask;

        //1. Throw a raycast in the desired direction
        RaycastHit2D rayHit = rb.Raycast(Vector2.zero, directionalInput, maxDistance, layerMask);

        if (rayHit)
        {
            return rayHit.point;
        }

        //2. Throw a boxCast in the desired direction to give leniency if the player sucks and missed
        Vector2 boxSize = new Vector2(leniency, maxDistance);
        Vector2 offset = directionalInput * (maxDistance / 2);
        RaycastHit2D boxHit = rb.BoxCast(offset, boxSize, 0, directionalInput, maxDistance, layerMask);

        if (boxHit)
        {
            return boxHit.point;
        }

        //3. Both missed and the grapple misses
        grappleHit = false;
        return rb.position + directionalInput * maxDistance;
    }

    #endregion

    #region Grappler Swing Logic

    private void StartGrappleSwinging()
    {
        rb.gravityScale = pmScript.GetGravity() / Physics2D.gravity.y;

        //grappleSpeed = InitialVelocityToAngleSpeed(pmScript.GetVelocity(), grapplePointDistance, FindCurrentPlayerAngleRad(grapplePoint, grapplePointDistance));
        joint.connectedAnchor = grapplePoint;
        joint.enabled = true;
        joint.distance = grapplePointDistance;
        rb.AddForce(pmScript.GetVelocity(), ForceMode2D.Impulse);
        //grappleSpeed = velocity.x > 0 ? grappleSpeed : -grappleSpeed;
        pmScript.playerState = PlayerMovement.PlayerStates.GrappleSwing;
    }

    public Vector2 JumpOutOfGrapple()
    {
        pmScript.playerState = PlayerStates.InAir;
        joint.enabled = false;
        return rb.linearVelocity;
    }

    public Vector2 SetPostGrappleVelocity()
    {
        return AngleSpeedToVelocity(grappleSpeed, grapplePointDistance, pmScript.grapplerDirectionFromPrevPoint);
    }

    #endregion

    #region Grappler Swing Calculations
    public float GrappleRotationDeg()
    {
        float rotation = FindCurrentPlayerAngleRad(grapplePoint, grapplePointDistance) * Mathf.Rad2Deg + 90;
        return rotation;
    }

    private float InitialVelocityToAngleSpeed(Vector2 playerVelocity, float radius, float playerAngleRad)
    {
        playerAngleRad = playerAngleRad % Mathf.PI; //Angle must be between 0 and pi for equations to work

        float speed = 0;

        speed += Mathf.Abs(playerVelocity.x * (-(2 / Mathf.PI) * Mathf.Abs(playerAngleRad - Mathf.PI / 2) + 1));
        speed += Mathf.Abs(playerVelocity.y * (2 / Mathf.PI * Mathf.Abs(playerAngleRad - Mathf.PI / 2)));

        //print((FindCurrentPlayerAngleRad(grapplePoint, radius) * Mathf.Rad2Deg, (-(2 / Mathf.PI) * Mathf.Abs(playerAngleRad - Mathf.PI / 2) + 1), (2 / Mathf.PI * Mathf.Abs(playerAngleRad - Mathf.PI / 2)), speed / (1 + radius), radius, playerMovementScript.GetVelocity()));

        float angleSpeedRad = speed / (1+radius);
        angleSpeedRad = playerVelocity.x >= 0 ? angleSpeedRad : -angleSpeedRad;

        if (Mathf.Abs(angleSpeedRad) < grappleEntrySpeedThreshold)
        {
            angleSpeedRad = 0;
            inStillHang = true;
        }
        else
        {
            inStillHang = false;
        }

        return angleSpeedRad;
    }

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
        float magnitude = Mathf.Abs(angleSpeed) * radius;

        Vector2 velocity = directionFromPrevPoint * magnitude;
        //velocity.y = angleSpeed < 0 ? -velocity.y : velocity.y;
        //velocity.x = directionFromPrevPoint.x < 0 ? -velocity.x : velocity.x;

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
        float gravityAngleSpeed = VelocityToAngleSpeed(new Vector2(0, -pmScript.gravity), grapplePointDistance);

        //If the player is on the right, add gravityAngleSpeed. If the player is on the left, subtract gravityAngleSpeed
        grappleSpeed = isOnTheRight ? grappleSpeed + gravityAngleSpeed * Time.fixedDeltaTime : grappleSpeed - gravityAngleSpeed * Time.fixedDeltaTime;

        return grappleSpeed;
    }

    private float CalculateGrappleThrowTime(float distance, bool grappleHit)
    {
        if (grappleHit)
        {
            return (distance / maxDistance) * maxThrowTime;
        }
        return maxThrowTime;
    }

    #endregion

    #region Grapple Pull
    public void StartGrapplePulling()
    {
        pmScript.playerState = PlayerMovement.PlayerStates.GrapplePull;
        joint.enabled = false;
    }

    public Vector2 GrapplePullMovement()
    {
        if (pmScript.CheckForGrounded() || pmScript.CheckForHeadhit())
        {
            return rb.position;
        }

        Vector2 directionToGrapplePoint = (grapplePoint - rb.position).normalized;
        return rb.position + directionToGrapplePoint * pullSpeed * Time.fixedDeltaTime;
    }

    public Vector2 CancelGrapplePull()
    {
        Vector2 exitVelocity = Vector2.zero;

        if (!pmScript.CheckForGrounded() && !pmScript.CheckForHeadhit())
        {
            Vector2 directionToGrapplePoint = (grapplePoint - rb.position).normalized;
            exitVelocity = directionToGrapplePoint * pullSpeed;
        }

        pmScript.playerState = PlayerMovement.PlayerStates.InAir;
        transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, 0);
        return exitVelocity;
    }

    public bool CheckForValidWallHold(RaycastHit2D hit)
    {
        if (Mathf.Sign(hit.point.x - transform.position.x) == Mathf.Sign(grapplePoint.x - transform.position.x))
        {
            return true;
        }
        return false;
    }

    private bool TouchingGround()
    {
        BoxCollider2D playerCollider = GetComponent<BoxCollider2D>();
        Vector2 size = new Vector2(playerCollider.size.x, playerCollider.size.y);
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(rb.position, hitTerrainRaycastSize, 0);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                return true;
            }
        }

        return false;
    }
}

#endregion