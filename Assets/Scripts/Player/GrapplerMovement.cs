using System;
using System.Collections;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using static PlayerMovement;

public class GrapplerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerMovement pmScript;
    private DistanceJoint2D joint;
    private LineRenderer lineRenderer;
    [SerializeField] private Vector2 hitTerrainRaycastSize;
    public GameObject grappleHead;

    public enum GrappleStates
    {
        None,
        GrapplePrep,
        GrappleThrow,
        GrappleSwing,
        GrapplePull,
        WallHold
    }
    [SerializeField] private GrappleStates grappleState = GrappleStates.None;

    [Header("Grappler Runtime")]
    [SerializeField] private Vector2 grapplerDirectionalInput;
    [SerializeField] private Vector3 mousePos;
    [SerializeField] private float grapplePointDistance;
    [SerializeField] private Vector2 grapplePoint;
    [SerializeField] private Vector2 grapplePullPosition = Vector2.zero;
    [SerializeField] private Vector2 wallHoldPos = Vector2.zero;

    [SerializeField] private Vector2 directionFromPrevPoint;
    [SerializeField] public float grappleSpeed; //MAKE GET; PRIVATE SET
    [SerializeField] private float originalAngleSpeed;
    [SerializeField] private float speedEquationFactor;

    [SerializeField] private bool movingRight = true;
    [SerializeField] private bool canHitWall = true;
    [SerializeField] private bool inStillHang = false;
    public bool groundedGrapple = false;

    public bool grapplePullOnStartGrappling = false;

    [SerializeField] private bool grappleKeyDown = false;
    [SerializeField] private bool grapplePullKeyDown = false;

    [Header("Grappler Settables")]
    public bool mouseMode = false;
    [SerializeField] private float maxGrapples = 1;
    [SerializeField] private float currGrapples = 0;
    public bool canGrapple => currGrapples < maxGrapples;

    [SerializeField] private float maxDistance = 20;

    [SerializeField] private float leniency = 5f;
    [SerializeField] private float maxThrowTime = 0.5f;
    public float pullSpeed = 5f;
    [SerializeField] private float grappleEntrySpeedThreshold = 0.05f;
    public float grappleVelocityMultiplier = 1.1f;


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.azure;
        Gizmos.DrawWireCube(transform.position, new Vector2(leniency, maxDistance) * grapplerDirectionalInput);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pmScript = GetComponent<PlayerMovement>();
        lineRenderer = transform.Find("Visuals").GetComponent<LineRenderer>();
        joint = GetComponent<DistanceJoint2D>();
        joint.enabled = false;
    }

    private void Update()
    {
        mousePos = GetComponent<PlayerInput>().actions["Mouse Position"].ReadValue<Vector2>();
        mousePos.z = 10;
        if (grappleState == GrappleStates.GrapplePrep)
        {
            grappleHead.SetActive(true);
            bool grappleHit = true; //BANDAID SOLUTION
            if (mouseMode)
            {
                grappleHead.transform.position = FindMouseGrapplePoint(ref grappleHit);
                grappleHead.GetComponent<SpriteRenderer>().color = grappleHit ? Color.black : Color.red;
            }
            else
            {  
                grappleHead.transform.position = FindGrapplePoint(ref grappleHit, grapplerDirectionalInput);
            }
        }
        else if (grappleState == GrappleStates.GrappleThrow || grappleState == GrappleStates.GrappleSwing)
        {
            grappleHead.transform.position = grapplePoint;
        }
        else if (grappleState == GrappleStates.None)
        {
            grappleHead.SetActive(false);
        }
    }

    private void FixedUpdate()
    {
        FindGrapplerRotation();
    }

    private void LateUpdate()
    {
        FindPlayerState();
        if (grappleState == GrappleStates.GrappleSwing || grappleState == GrappleStates.GrapplePull)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, grapplePoint);
        }
        else
        {
            joint.enabled = false;
            lineRenderer.enabled = false;
        }
    }

    private void OnEnable()
    {
        OnPlayerStateChanged += SetGrappleStateToNone;
    }

    private void OnDisable()
    {
        OnPlayerStateChanged -= SetGrappleStateToNone;
    }

    #region Player Stuff

    public void SetPlayerMovement()
    {
        switch (grappleState)
        {
            case GrappleStates.GrapplePrep:
                pmScript.AirMovement();
                pmScript.MovePlayerByVelocity();
                break;

            case GrappleStates.GrappleThrow:
                pmScript.ApplyGravity();
                pmScript.MovePlayerByVelocity();
                break;

            case GrappleStates.GrapplePull:
                rb.MovePosition(GrapplePullMovement());
                break;

            case GrappleStates.WallHold:
                rb.MovePosition(wallHoldPos);
                break;
        }
    }

    private void FindGrapplerRotation()
    {
        float zRotation = 0;
        if (grappleState == GrappleStates.GrappleSwing)
        {
            pmScript.playerDirection = grappleSpeed < 0 ? PlayerDirection.Left : PlayerDirection.Right;
            zRotation = GrappleRotationDeg();
            zRotation = pmScript.playerDirection == PlayerDirection.Right ? zRotation : -zRotation;
        }
        else if (grappleState == GrappleStates.GrapplePull)
        {
            zRotation = GrappleRotationDeg();
            zRotation = pmScript.playerDirection == PlayerDirection.Right ? zRotation : -zRotation;
        }

        int yRotation = pmScript.playerDirection == PlayerDirection.Left ? 180 : 0;
        transform.Find("Visuals").transform.rotation = Quaternion.Euler(0, yRotation, transform.rotation.z);
    }

    private void FindPlayerState()
    {
        RaycastHit2D groundedHit = pmScript.CheckForGrounded();
        RaycastHit2D leftWallHit = pmScript.CheckForWallTouch(true);
        RaycastHit2D rightWallHit = pmScript.CheckForWallTouch(false);

        bool canGround = groundedHit && !groundedGrapple;
        bool wallHit = leftWallHit || rightWallHit;

        bool playerHasCorrectDirectionalInput = (leftWallHit && grapplerDirectionalInput.x < 0) || (rightWallHit && grapplerDirectionalInput.x > 0);
        bool playerHasCorrectVelocity = (Mathf.Sign(pmScript.velocity.x) == 1 && rightWallHit) || (Mathf.Sign(pmScript.velocity.x) == -1 && leftWallHit);

        if (wallHit && grappleState == GrappleStates.GrapplePull)
        {
            //if (CheckForValidWallHold(leftWallHit ? leftWallHit : rightWallHit) && grapplePullKeyDown)
            //{
            //    EnterWallHold(leftWallHit);
            //}
            //else
            //{
            //    CancelGrapplePull();
            //}
        }
        else if (canGround && grappleState == GrappleStates.GrapplePull)
        {
            CancelGrapplePull();
        }
        else if (grappleState == GrappleStates.GrappleSwing && wallHit)
        {
            //pmScript.OnWallHit(leftWallHit, rightWallHit);
            //CancelGrappleSwing();
        }
    }

    private void SetGrappleStateToNone()
    {
        if (pmScript.PlayerState != PlayerStates.ClassMovement) grappleState = GrappleStates.None;
    }

    private void SetGrappleState(GrappleStates state)
    {
        if (state != GrappleStates.None) pmScript.PlayerState = PlayerStates.ClassMovement;
        grappleState = state;
    }

    #endregion
    public void SetGrappleDirectionalInput(InputAction.CallbackContext context)
    {
        grapplerDirectionalInput = context.ReadValue<Vector2>();
    }

    public void ResetGrapples()
    {
        currGrapples = 0;
    }

    public IEnumerator GrappleThrowCoroutine()
    {
        int layerMask = LayerMask.GetMask("Terrain");
        bool grappleHit = true;

        if (grapplerDirectionalInput == Vector2.zero)
        {
            grapplerDirectionalInput = pmScript.playerDirection == PlayerDirection.Right ? Vector2.right : Vector2.left;
        }

        if (mouseMode)
        {
            grapplePoint = FindMouseGrapplePoint(ref grappleHit);
        }
        else
        {
            grapplePoint = FindGrapplePoint(ref grappleHit, grapplerDirectionalInput);
        }
            
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
            currGrapples++;
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
            groundedGrapple = false;
            grapplePullOnStartGrappling = false;
            pmScript.PlayerState = PlayerStates.InAir;
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

        RaycastHit2D[] boxHits = rb.BoxCastAll(offset, boxSize, 0, directionalInput, maxDistance, layerMask);

        if (boxHit)
        {
            return boxHit.point;
        }

        //3. Both missed and the grapple misses
        grappleHit = false;
        return rb.position + directionalInput * maxDistance;
    }

    private Vector2 FindMouseGrapplePoint(ref bool grappleHit)
    {
        Vector2 point = Camera.main.ScreenToWorldPoint(mousePos);

        //1. Raycast from player to mouse position at the max grapple distance
        float distanceAdjustFactor = maxDistance - (rb.position - point).magnitude;
        RaycastHit2D hit = rb.RaycastFromPlayer(point, pmScript.playerRaycastLayerMask, distanceAdjustFactor);
        if (hit)
        {
            return hit.point;
        }
        else
        {
            grappleHit = false;
            return point;
        }
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

        //print(pmScript.GetVelocity());
        rb.AddForce(pmScript.GetVelocity(), ForceMode2D.Impulse);
        //grappleSpeed = velocity.x > 0 ? grappleSpeed : -grappleSpeed;
        grappleState = GrappleStates.GrappleSwing;
    }

    public Vector2 JumpOutOfGrapple()
    {
        pmScript.PlayerState = PlayerStates.InAir;
        return rb.linearVelocity * grappleVelocityMultiplier;
    }

    public void CancelGrappleSwing()
    {
        joint.enabled = false;
        lineRenderer.enabled = false;
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
        grappleState = GrappleStates.GrapplePull;
        grapplePullPosition = Vector2.zero;
        pmScript.ZeroVelocity();
        joint.enabled = false;
    }

    public Vector2 GrapplePullMovement()
    {
        if (!pmScript.CheckForGrounded() && groundedGrapple)
        {
            groundedGrapple = false;
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

        pmScript.PlayerState = PlayerStates.InAir;
        transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, 0);
        lineRenderer.enabled = false;
        groundedGrapple = false;
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

    private void EnterWallHold(bool isLeftWall)
    {
        if (pmScript.CheckForHeadhit())
        {
            CancelGrapplePull();
            return;
        }

        CancelGrapplePull();
        pmScript.playerDirection = isLeftWall ? PlayerDirection.Left : PlayerDirection.Right;
        wallHoldPos = rb.position;
        grappleState = GrappleStates.WallHold;
    }

    #endregion

    #region Input

    public void OnGrappleInput(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;
        if (keyPressed && (pmScript.PlayerState == PlayerStates.InAir || pmScript.PlayerState == PlayerStates.OnWall) && canGrapple)
        {
            SetGrappleState(GrappleStates.GrapplePrep);
        }
        else if (!keyPressed && grappleState == GrappleStates.GrapplePrep)
        {
            SetGrappleState(GrappleStates.GrappleThrow);
            StartCoroutine(GrappleThrowCoroutine());
        }
        else if (keyPressed && grappleState == GrappleStates.GrappleSwing)
        {
            StartGrapplePulling();
        }
        else if (!keyPressed && grappleState == GrappleStates.GrapplePull)
        {
            pmScript.velocity = Vector2.zero;
            pmScript.AddForce(CancelGrapplePull() / 1.2f, true);
            pmScript.FindPlayerState(false);
        }
        else if (!keyPressed && grappleState == GrappleStates.WallHold)
        {
            pmScript.EnterWall();
        }
    }

    public void OnGrapplePullInput(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;
        grapplePullKeyDown = keyPressed;

        if (keyPressed && (pmScript.PlayerState == PlayerStates.InAir || pmScript.PlayerState == PlayerStates.OnWall || pmScript.PlayerState == PlayerStates.Grounded) && canGrapple)
        {
            if (pmScript.PlayerState == PlayerStates.Grounded)
            {
                groundedGrapple = true;
            }

            SetGrappleState(GrappleStates.GrappleThrow);
            StartCoroutine(GrappleThrowCoroutine());
            grapplePullOnStartGrappling = true;
        }
        else if (keyPressed && grappleState == GrappleStates.GrappleSwing)
        {
            StartGrapplePulling();
        }
        else if (keyPressed && grappleState == GrappleStates.GrappleThrow)
        {
            grapplePullOnStartGrappling = true;
        }
        else if (!keyPressed && grappleState == GrappleStates.GrapplePull)
        {
            pmScript.velocity = Vector2.zero;
            pmScript.AddForce(CancelGrapplePull() / 1.2f, true);
            pmScript.FindPlayerState(false);
        }
        else if (!keyPressed && grappleState == GrappleStates.WallHold)
        {
            pmScript.EnterWall();
        }
    }

    public void OnJumpGrappler(InputAction.CallbackContext context)
    {
        bool keyPressed = context.ReadValue<float>() == 1;
        if (keyPressed && grappleState == GrappleStates.GrappleSwing)
        {
            pmScript.AddForce(JumpOutOfGrapple(), true);
        }
    }

    #endregion
}




