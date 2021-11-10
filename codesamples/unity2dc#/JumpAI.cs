using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpAI : MonoBehaviour
{
    public bool enableObstacleJumping = true; // Enable jumping over obstacles.
    public bool enablePlatformJumping = true; // Enable if we can jump up to platforms.
    public bool enableGapJumping = true; // Enable if we can jump over gaps or not.
    public bool enableObstructionCheckAbove = true; // Enable if we'll check for obstructions above us before jumping.
    public float jumpDistanceMaximum = 5.0f; // The farthest amount of distance we can jump.
    public float jumpDistanceMinimum = 0.8f; // The minimum amount of distance to cause us to jump.
    public float jumpLookAheadDist = 2.0f; // The distance to which we should look ahead to detect obstacles.
    public int jumpHeightMaximum = 5; // The maximum height of an obstalce that we can jump over.
    public float jumpHeightMinimum = 0.3f; // The minimum height of an obstalce to cause us to jump.
    public float jumpClearanceHeight = 0.3f; // The additional clearance we want to add to our localScale.y to ensure we have space above target for jumping.
    public float jumpFallCheckDistance = 5.0f; // The distance below us that we should check to see if we can fall or not.
    public float jumpForce = 2.0f; // Force applied to AddForce calculation to give more of a push towards target while moving in air.
    public bool enableFailMethods = true; // Set whether or not we even want to enable fail fixing.
    public int jumpFailsBeforeReset = 2; // Maximum number of failed attempts to jump before we reset.
    public float jumpFailResetSeconds = 30.0f; // Duration of time in seconds to wait before resetting failed attempts.

    public float jumpNodeHeightRequirement = 0.8f; // How high a jump node needs to be to make us jump.
    public float jumpGravityInverseFactor = -14f; // Used for calculating the JumpVelocity. -18 was suggested. Influences Gravity power of jump.
    public float jumpHeightArc = 4; // How high the arc of our jump should go.
    public float isGroundedCheckOffset = 0.1f; // Used for our isGrounded calculation.

    Rigidbody2D rb;
    Collider2D col2D;

    private string[] jumpableLayers = { "Ground", "Climbable", "Transport" }; // The layers we'll check that we can jump onto.
    [HideInInspector]
    public int jumpLayers; // Layers, in integer form, that we'll want to detect we can jump on.

    [HideInInspector]
    public Vector2 lookDirection = Vector2.left; // Direction we're currently looking, determined by velocity.
    [HideInInspector]
    public bool isJumping;
    bool isOnJumpSurface;

    Vector2 prevJumpTarget; // Logging our previous jumpTarget to compare against new to see if we failed.
    bool jumpFailed; // If our jump failed, use this as boolean test.
    int jumpFailedCount;

    float prevX;
    bool moveBack; // Used for moving back a bit to get a better angle in FixedUpdate

    [HideInInspector]
    public bool lookDirectionSetByPathing;

    void Start()
    {
        // Which layers we want to detect.
        jumpLayers = LayerMask.GetMask(jumpableLayers);
        // Our rigid body.
        rb = transform.GetComponent<Rigidbody2D>();
        col2D = transform.GetComponent<Collider2D>();
    }

    void Update()
    {
        //See if colliding with anything
        isOnJumpSurface = Physics2D.Raycast(transform.position, -Vector3.up, col2D.bounds.extents.y + isGroundedCheckOffset, jumpLayers);
        /*
        if (!isJumping && isOnJumpSurface)
        {
            //JumpCheck();
        }*/
    }

    private void FixedUpdate()
    {
        if (!lookDirectionSetByPathing)
        {
            // The direction we're currently looking.
            if (prevX > transform.position.x)
            {
                lookDirection = Vector2.left;
            }
            else if (prevX < transform.position.x)
            {
                lookDirection = Vector2.right;
            }
            prevX = transform.position.x;
        } else
        {
            lookDirectionSetByPathing = false;
        }

        // We use the moveBack bool and below if series for when we need to make a jump that requires us to be back farther
        // Which often includes jumping to platforms and jumping up sheer obstacles, kind of like moving back a bit to get a better angle.
        if (moveBack)
        {
            if (lookDirection == Vector2.left)
            {
                // Move back right
                if (Vector2.Distance(new Vector2(transform.position.x + 0.30f, transform.position.y), transform.position) <= 0.02f)
                {
                    moveBack = false;
                }
                if (moveBack)
                {
                    transform.position = Vector2.MoveTowards(transform.position, new Vector2(transform.position.x + 0.30f, transform.position.y), 0.3f * Time.fixedDeltaTime);
                }
            }
            else if (lookDirection == Vector2.right)
            {
                // move back left
                if (Vector2.Distance(new Vector2(transform.position.x + -0.30f, transform.position.y), transform.position) <= 0.02f)
                {
                    moveBack = false;
                }
                if (moveBack)
                {
                    transform.position = Vector2.MoveTowards(transform.position, new Vector2(transform.position.x + -0.30f, transform.position.y), 0.3f * Time.fixedDeltaTime);
                }
            }
        }
    }

    public void JumpCheck(Component componentCallingThisMethod)
    {
        if (!isJumping)
        {
            isJumping = true;
            Vector2 jumpTarget = Vector2.zero;

            if (enableObstacleJumping)
            {
                if (jumpTarget == Vector2.zero)
                {
                    jumpTarget = JumpObstacleCheck();
                    if (jumpTarget != Vector2.zero)
                    {
                        moveBack = true;
                        Debug.Log("Possible jump target from the obstacle: " + jumpTarget);
                    }
                }
            }

            if (enableGapJumping && isOnJumpSurface)
            {
                if (jumpTarget == Vector2.zero)
                {
                    if (JumpGapCheck())
                    {
                        jumpTarget = JumpGapDistanceCheck();
                        if (jumpTarget != Vector2.zero)
                        {
                            Debug.Log("Possible jump target from the gap: " + jumpTarget);
                        }
                    }
                }
            }

            if (enablePlatformJumping)
            {
                if (JumpPlatformCheck() != Vector2.zero)
                {
                    if (jumpTarget == Vector2.zero)
                    {
                        jumpTarget = FindJumpTarget(JumpPlatformCheck());
                        if (jumpTarget != Vector2.zero)
                        {
                            moveBack = true;
                            Debug.Log("Platform jump target: " + jumpTarget);
                        }
                    }
                }


            }

            if (enableObstructionCheckAbove)
            {
                if (jumpTarget != Vector2.zero)
                {
                    if (CheckForObstructionAbove(jumpTarget))
                    {
                        // If there's an obstruction above us, blocking our way, then cancel the jump.
                        jumpTarget = Vector2.zero;
                    }
                }
            }


            if (jumpTarget != Vector2.zero)
            {
                Debug.Log("JumpAI: 144 jumpTarget != Vector2.zero");
                // If we want to enable fail checking
                if (enableFailMethods)
                {
                    Debug.Log("jumpAI: 148: enableFailMethods = true");
                    // In attempting to figure out if we've failed the jump or not
                    // we compare the current jumpTarget to the previous one, and if they're the same, we count
                    // that as a "failed" attempt since the target is unchanged.
                    if (jumpTarget == prevJumpTarget)
                    {
                        Debug.Log("154");
                        // However we want to check first if we can just realign ourselves to complete the jump
                        // i.e. approaching from a slightly farther out distance for the angle of the jump
                        if (jumpFailedCount != jumpFailsBeforeReset)
                        {
                            Debug.Log("159");
                            // If we haven't reached our max number of attempts, then try to JumpRealign
                            JumpRealign();
                            jumpFailed = false;
                        }
                        else
                        {
                            Debug.Log("166");
                            // If we've reached our maximum amount of attempts, then we want to
                            // change the bool to signify we've failed, 0 out the count, and start the reset
                            jumpFailed = true;
                            jumpFailedCount = 0;
                            StartCoroutine(JumpReset(componentCallingThisMethod));
                        }
                        jumpFailedCount++;
                    }
                    else
                    {
                        Debug.Log("JumpAI: 177: jumpTarget != prevJumpTarget");
                        // Otherwise if we didn't fail, then update the prevJumpTarget to our new one
                        // and resolve the other variables.
                        prevJumpTarget = jumpTarget;
                        jumpFailed = false;
                        jumpFailedCount = 0;
                    }
                }


                if (!jumpFailed || !enableFailMethods)
                {
                    //Debug.Log("JumpAI: 185: !jumpFailed || !enableFailMethods");
                    if (componentCallingThisMethod.GetType().Name == "PathingAI")
                    {
                        if (TryGetComponent<PathingAI>(out PathingAI component) && component.isActiveAndEnabled)
                        {
                            //Debug.Log("Calling JumpActive in PathingAI");
                            component.JumpActive();
                        }
                    }
                    Jump(jumpTarget);
                    // Attempting to give us a little more of a push to reach our goal, since
                    // the CalculateJumpTrajectory velocity is a little off target and I don't know why
                    // this though doesn't seem to make much difference and might not be doing much at all.
                    Vector2 direction = (jumpTarget - rb.position).normalized;
                    Vector2 force = jumpForce * Time.deltaTime * direction;
                    if (!isOnJumpSurface)
                    {
                        rb.AddForce(force);
                    }
                    //Debug.Log("JumpAI: 177 about to report JumpSuccess");
                    if(componentCallingThisMethod.GetType().Name == "PathingAI" || componentCallingThisMethod.GetType().Name == "RoamingAI")
                    {
                        JumpSuccess(componentCallingThisMethod);
                    }
                } else if (jumpFailed)
                {
                    if (componentCallingThisMethod.GetType().Name == "PathingAI" || componentCallingThisMethod.GetType().Name == "RoamingAI")
                    {
                        JumpFailed(componentCallingThisMethod);
                    }
                }
            }
            else
            {
                //Debug.Log("JumpAI: JumpCheck: jumpTarget == zero. Component: " + componentCallingThisMethod.GetType());
                isJumping = false;
                if (componentCallingThisMethod.GetType().Name == "PathingAI" || componentCallingThisMethod.GetType().Name == "RoamingAI")
                {
                    JumpCheckEnded(componentCallingThisMethod);
                }
            }
        }
    }

    public void PlatformJumpOnly(Component componentCallingThisMethod)
    {

        Vector2 jumpTarget = Vector2.zero;

        isJumping = true;

        if (enablePlatformJumping)
        {
            if (JumpPlatformCheck() != Vector2.zero && jumpTarget == Vector2.zero)
            {
                jumpTarget = FindJumpTarget(JumpPlatformCheck());
                Debug.Log("Platform jump target: " + jumpTarget);
            }
        }

        if (enableObstructionCheckAbove)
        {
            if (jumpTarget != Vector2.zero)
            {
                if (CheckForObstructionAbove(jumpTarget))
                {
                    // If there's an obstruction above us, blocking our way, then cancel the jump.
                    jumpTarget = Vector2.zero;
                }
            }
        }


        if (jumpTarget != Vector2.zero)
        {
            // If we want to enable fail checking
            if (enableFailMethods)
            {
                // In attempting to figure out if we've failed the jump or not
                // we compare the current jumpTarget to the previous one, and if they're the same, we count
                // that as a "failed" attempt since the target is unchanged.
                if (jumpTarget == prevJumpTarget)
                {
                    // However we want to check first if we can just realign ourselves to complete the jump
                    // i.e. approaching from a slightly farther out distance for the angle of the jump
                    if (jumpFailedCount != jumpFailsBeforeReset)
                    {
                        // If we haven't reached our max number of attempts, then try to JumpRealign
                        JumpRealign();
                        jumpFailed = false;
                        PlatformJumpOnly(componentCallingThisMethod);
                    }
                    else
                    {
                        // If we've reached our maximum amount of attempts, then we want to
                        // change the bool to signify we've failed, 0 out the count, and start the reset
                        jumpFailed = true;
                        jumpFailedCount = 0;
                        StartCoroutine(JumpReset(componentCallingThisMethod));
                    }
                    jumpFailedCount++;
                }
                else
                {
                    // Otherwise if we didn't fail, then update the prevJumpTarget to our new one
                    // and resolve the other variables.
                    prevJumpTarget = jumpTarget;
                    jumpFailed = false;
                    jumpFailedCount = 0;
                    JumpSuccess(componentCallingThisMethod);
                }
            }


            if (!jumpFailed || !enableFailMethods)
            {
                Jump(jumpTarget);
                // Attempting to give us a little more of a push to reach our goal, since
                // the CalculateJumpTrajectory velocity is a little off target and I don't know why
                // this though doesn't seem to make much difference and might not be doing much at all.
                Vector2 direction = (jumpTarget - rb.position).normalized;
                Vector2 force = jumpForce * Time.deltaTime * direction;
                if (!isOnJumpSurface)
                {
                    rb.AddForce(force);
                }
            }
        }
        else
        {
            isJumping = false;
        }
    }

    IEnumerator JumpReset(Component componentCallingThisMethod)
    {
        // When we need to reset the jump due to failing to make the jump
        // could possibly expand or further develop this in the future if needed
        yield return new WaitForSeconds(jumpFailResetSeconds);
        JumpRealign();
        isJumping = false;
        JumpFailed(componentCallingThisMethod);
    }

    void JumpRealign()
    {
        // A very simple attempt to fix failed jumps by moving us back a bit from the current position
        // which gives us a better angle for the CalculateJumpVelocity arc, and seems to allow us to
        // make jumps when the obstacle is a flat wall we're directly against, for example.
        if(prevJumpTarget.x > transform.position.x)
        {
            //transform.position = new Vector2(transform.position.x + -0.30f, transform.position.y);
            rb.MovePosition(new Vector2(transform.position.x + -0.30f, transform.position.y));
        } else
        {
            //transform.position = new Vector2(transform.position.x + Vector2.right.x, transform.position.y);
            rb.MovePosition(new Vector2(transform.position.x + 0.30f, transform.position.y));
        }
        
    }

    void JumpCheckEnded(Component componentCallingThisMethod)
    {
        if (componentCallingThisMethod.GetType().Name == "PathingAI")
        {
            if (TryGetComponent<PathingAI>(out PathingAI component) && component.isActiveAndEnabled)
            {
                component.JumpCheckEnded();
            }
        }
        else if (componentCallingThisMethod.GetType().Name == "RoamingAI")
        {
            if (TryGetComponent<RoamingAI>(out RoamingAI roamComp) && roamComp.isActiveAndEnabled)
            {
                roamComp.JumpFailed();
            }
        }
    }

    void JumpFailed(Component componentCallingThisMethod)
    {
        //Debug.Log("JumpAI: JumpFailed.");
        if (componentCallingThisMethod.GetType().Name == "PathingAI")
        {
            if (TryGetComponent<PathingAI>(out PathingAI component) && component.isActiveAndEnabled)
            {
                component.JumpFailed();
            }
        }
        else if (componentCallingThisMethod.GetType().Name == "RoamingAI")
        {
            if (TryGetComponent<RoamingAI>(out RoamingAI roamComp) && roamComp.isActiveAndEnabled)
            {
                roamComp.JumpFailed();
            }
        }
    }

    void JumpSuccess(Component componentCallingThisMethod)
    {
        Debug.Log("JumpAI: JumpSuccess reached.");
        if (componentCallingThisMethod.GetType().Name == "PathingAI")
        {
            Debug.Log("JumpAI: component = PathingAI");
            if (TryGetComponent<PathingAI>(out PathingAI component) && component.isActiveAndEnabled)
            {
                Debug.Log("Calling JumpSuccessful in PathingAI");
                component.JumpSuccessful();
            }
        } else if(componentCallingThisMethod.GetType().Name == "RoamingAI")
        {
            if (TryGetComponent<RoamingAI>(out RoamingAI roamComp) && roamComp.isActiveAndEnabled)
            {
                roamComp.JumpSuccessful();
            }
        }
    }

    bool JumpHeightClearanceCheck(Vector2 potentialGoalPoint)
    {
        // Take in the currentPoint that's being tested, the potentialGoalPoint that we have, and the previousPoint before it
        // using our current point we'll see if the distance between it and the potentialgoalpoint is enough for us to fit
        // and then we'll use the previouspoint to return a value we can jump to.
        // then compare that to our localScale.y + jumpClearanceHeight to make sure we have enough room.
        // This old way was too freaking buggy, in that it didn't accurately get spaces because other parts of the code were failing.
        /*float roundYDistPoints = Mathf.Abs(Mathf.Round((currentPoint.y - potentialGoalPoint.y) * 10.0f) * 0.1f);
        if (roundYDistPoints >= (transform.localScale.y + jumpClearanceHeight))
        {
            // If there's enough room, then we return true.
            return true;
        } else
        {
            return false;
        }*/
        // cast a ray up to the distance of the jumpHeightArc, seeing if we'd collide with anything if we made such a jump
        RaycastHit2D blockRay = Physics2D.Raycast(transform.position, Vector2.up, jumpHeightArc, jumpLayers);
        Debug.DrawRay(transform.position, Vector2.up * jumpHeightArc, Color.white, 20.0f);
        if (blockRay.collider != null)
        {
            
            return false;
        }
        else
        {
            RaycastHit2D checkSizeRay = Physics2D.Raycast(potentialGoalPoint, Vector2.up, transform.localScale.y, jumpLayers);
            Debug.DrawRay(potentialGoalPoint, Vector2.up * transform.localScale.y, Color.white, 20.0f);
            if (checkSizeRay.collider != null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    Vector2 JumpTargetMidPoint(Vector2 potentialGoalPoint, Vector2 previousPoint)
    {
        //  we want to find the middle point between the obstacle and the space behind it
        // so that we can use the middlepoint for our jumpTarget vector2.
        //float xVal = potentialGoalPoint.x + (Mathf.Abs(Mathf.Round(previousPoint.x - potentialGoalPoint.x)) / 2);
        float xVal = (potentialGoalPoint.x + previousPoint.x) * 0.5f;
        Vector2 jumpTarget = new Vector2(xVal, potentialGoalPoint.y);
        // We want the distance between our position and the jumpCast.point to see if we can jump to it from where we are.
        float distanceBetween = Vector2.Distance(rb.position, jumpTarget);
        // Then test to see if it's within our maximum jumping distance.
        if (distanceBetween <= jumpDistanceMaximum)
        {
            // We'd then call the actual jump method with the jumpTarget vector points.
            return jumpTarget;
        } else
        {
            return Vector2.zero;
        }
    }

    bool CheckForXObstruction(Vector2 targetPoint)
    {
        // Just a quick method to shoot a ray to the point to make sure there aren't any obstructions
        // for where we might want to jump. You might make this a little more nuanced later, depending on need, as it's pretty basic now.
        Vector2 offsetPoint = targetPoint + new Vector2((transform.localScale.x - 0.5f), 0);
        Vector2 direction = Vector2.left;
        float distance = Vector2.Distance(new Vector2(targetPoint.x + (transform.localScale.x - 0.5f), targetPoint.y), new Vector2(targetPoint.x - (transform.localScale.x - 0.5f), targetPoint.y));
        RaycastHit2D obstCast = Physics2D.Raycast(offsetPoint, direction, distance, jumpLayers);
        //Debug.DrawRay(offsetPoint, direction * distance, Color.green, 2.0f);
        if (obstCast.collider == null)
        {
            return false;
        }
        return true;
    }

    bool CheckForObstructionAbove(Vector2 targetPoint)
    {
        // We want to make sure there isn't anything that would block us from our jump, this is especially true to
        // avoid jumping into a platform we're attemping to jump up to, so we don't just keep clunking our head.
        // First create an adjusted point based on up + scale + heightArc which gives us a rough distance of clearance.
        Vector2 arcPoint = new Vector2(0, jumpHeightArc);
        // Add that to our distance check.
        float distance = Vector2.Distance(transform.position, targetPoint + arcPoint);
        RaycastHit2D obstCast = Physics2D.BoxCast(transform.position, transform.localScale, 0.0f, Vector2.up, distance, jumpLayers);
        Debug.DrawRay(transform.position, Vector2.up * distance, Color.green, 2.0f);
        if (obstCast.collider == null)
        {
            return false;
        }

        return true;
    }


    Vector2 EstimateTargetFromOnePoint(Vector2 whatVecYouDoHave)
    {
        // We use this method when we have one vector2 from the rays, but we don't have a second to compare
        // meaning that we've likely found a spot where a null space (nothing above the ray's collision point) exists.
        Vector2 bestGuessPoint;
        // Determine if the vector we do have is left or right of us so we can adjust our bestGuessPoint accordingly
        if (whatVecYouDoHave.x > transform.position.x)
        {
            bestGuessPoint = whatVecYouDoHave + new Vector2(0.0f, (transform.localScale.y - 0.5f) + jumpClearanceHeight ) + Vector2.right;

        }
        else
        {
            bestGuessPoint = whatVecYouDoHave + new Vector2(0.0f, (transform.localScale.y - 0.5f) + jumpClearanceHeight ) + Vector2.left;
        }
        // Then find the adjusted jumpTarget between our bestGuessPoint and the whatVecYouDoHave.
        Vector2 potentialJumpTarget = JumpTargetMidPoint(bestGuessPoint, whatVecYouDoHave);
        //Debug.Log("bestguesspoint: " + bestGuessPoint + " whatVecYouDoHave: " + whatVecYouDoHave + " potentialJumpTarget: " + potentialJumpTarget);
        // If we've found a midpoint that works for us
        if (potentialJumpTarget != Vector2.zero)
        {
            // Then do a quick check for obstructions at that point just to make sure there isn't something we didn't detect from previous rays.
            // We don't use a JumpHeightCheck here because it's likely there's nothing above the spot that would necessitate checking we fit height-wise.
            // But checking for obstructions just double-checks that's true without needing to modify our y values to meet the heightcheck.
            if (!CheckForXObstruction(potentialJumpTarget))
            {
                // Return the potential target.
                return potentialJumpTarget;
            }
            else
            {
                //Debug.Log("EstimateTargetFromOnePoint: Obstruction Detected? bestguesspoint: " + bestGuessPoint + " whatVecYouDoHave: " + whatVecYouDoHave);
            }
            
        }
        else
        {
            //Debug.Log("EstimateTargetFromOnePoint: DistanceBetween is above DistanceMaximum, cannot jump to it.");
        }
        
        return Vector2.zero;
    }

    Vector2 FindJumpTarget(Vector2 rayCastOrigin)
    {
        // Test 3: See if there's an obstacle in front of us, and if we can jump over it.
        // Our eventual target holder.
        Vector2 jumpTarget = Vector2.zero;

        float previousXPoint = transform.position.x; // We'll need this to check the distance between two collision points to see if there's room
                                                     // for us to jump to.
        Vector2 previousPoint = Vector2.zero;
        Vector2 newPoint = Vector2.zero;

        float x = 0.0f;
        //Debug.Log("rayCastOrigin: " + rayCastOrigin);
        while (x < jumpHeightMaximum)
        {
            //Debug.Log("JumpAI: FindJumpTarget: while x: " + x);
            // Incremently move our origin up via the while loop.
            Vector2 directionIncr = rayCastOrigin + new Vector2(0, x);
            //Debug.Log("directionIncr: " + directionIncr);
            // Cast a ray incrementally above us, up to our jumpHeightMaximum, at a distance of how far ahead we can look.
            RaycastHit2D jumpCast = Physics2D.Raycast(directionIncr, lookDirection, jumpLookAheadDist, jumpLayers);
            Debug.DrawRay(directionIncr, lookDirection, Color.red, 2.0f);
            if (jumpCast.collider != null)
            {
                //Debug.Log("JumpAI: FindJumpTarget: Collider: " + jumpCast.collider.name);
                // If our ray is colliding with objects
                // We want to compare just the Y values to see if it meets height requirements.
                float distanceBetweenYOnly = Vector2.Distance(new Vector2(0, rayCastOrigin.y), new Vector2(0, jumpCast.point.y));
                distanceBetweenYOnly = Mathf.Round(distanceBetweenYOnly * 10.0f) * 0.1f;
                // First test if the obstacle is within our heightmaximum and minimum to see if we need and can jump over it.
                //Debug.Log("JumpAI: FindJumpTarget: distanceBetweenYOnly: " + distanceBetweenYOnly + " jumpheightMaximum: " + jumpHeightMaximum + " jumpHeightMinimum: " + jumpHeightMinimum);
                if (distanceBetweenYOnly <= jumpHeightMaximum && distanceBetweenYOnly >= jumpHeightMinimum)
                {
                    // Compare the previousXPoint to the current point.x to see if there's enough space for us to jump.
                    // We round the values to the nearest x.x, then we get the abs value of that rounded number since our size cannot be negative.
                    float roundXDistPoints = Mathf.Abs(Mathf.Round((jumpCast.point.x - previousXPoint) * 10.0f) * 0.1f);
                    // If the absolute value is equal or larger to our size, then there is enough x space for us.
                    //Debug.Log("JumpAI: FindJumpTarget: roundXDistPoints: " + roundXDistPoints + " transform.localscale.x: " + transform.localScale.x);
                    if (roundXDistPoints >= transform.localScale.x)
                    {
                        // If our x size is enough of a fit for us, then log the point for later reference for our jumpTarget.
                        newPoint = jumpCast.point;
                        //Debug.Log("JumpAI: FindJumpTarget: newPoint: " + newPoint);
                    }

                    //If our newPoint is zero, change the previousPoint to our current one so that we can reference it if we find enough space
                    // but we say if it'z zero so that we're not overwriting it with the same value as newpoint. Untested.
                    if (newPoint == Vector2.zero)
                    {
                        previousPoint = jumpCast.point;
                        //Debug.Log("JumpAI: FindJumpTarget: newPoint == Vector2.Zero, so: PreviousPoint: " + previousPoint);
                    }

                    // If we've found a potential landing point, we then need to determine if we have enough y space above it.
                    if (newPoint != Vector2.zero)
                    {
                        
                        // Then find the adjusted jumpTargetMidPoint between our potentialGoalPoint and the previousPoint.
                        //Debug.Log("JumpAI: FindJumpTarget: newPoint: " + newPoint + " previousPoint: " + previousPoint);
                        Vector2 potentialJumpTarget = JumpTargetMidPoint(newPoint, previousPoint);
                        //Debug.Log("JumpAI: FindJumpTarget: potentialJumpTarget: " + potentialJumpTarget);
                        if (potentialJumpTarget != Vector2.zero)
                        {
                            //Debug.Log("JumpAI: FindJumpTarget: newPoint != Vector2.zero, so JumpHeightClearanceCheck: " + JumpHeightClearanceCheck(potentialJumpTarget) + " potentialJumpTarget: " + potentialJumpTarget);
                            // If there's enough space on the y axis for us to make the jump.
                            if (JumpHeightClearanceCheck(potentialJumpTarget))
                            {
                                // We only need the first jumpTarget that works, so, if it's currently zero.
                                if (jumpTarget == Vector2.zero)
                                {
                                    jumpTarget = potentialJumpTarget;
                                    return jumpTarget;
                                }
                            }
                            else
                            {
                                return Vector2.zero;
                            }
                        }
                    }
                }
                // Log this point.x for comparison to see if there's enough room for us.
                previousXPoint = jumpCast.point.x;
                //previousPoint = jumpCast.point;

            }
            else
            {
                //Debug.Log("JumpAI: FindJumpTarget: collider == null");
                // If the ray is no longer colliding with objects it means the space above it may be empty, so,
                // we then have to figure out how to calculate a space we can potentially jump from either the last
                // known point of collision, or whatever information we have, and our own made up or designed points.
                //Debug.Log("jumpCast.collider is null");
                if (jumpTarget == Vector2.zero)
                {
                    // Ensuring that our jumpTarget is indeed zero so we can proceed.
                    //Debug.Log("jumpTarget is zero");
                    if (newPoint != Vector2.zero)
                    {
                        // If we got as far as developing a newPoint before hitting null space.
                        //Debug.Log("newPoint != zero");
                        if (previousPoint != Vector2.zero)
                        {
                            // And if we have a previousPoint to work from as well...
                            // use those two values to move forward and calculate our jumpTarget.
                            // Then find the adjusted jumpTarget between our potentialGoalPoint and the previousPoint.
                            Vector2 potentialJumpTarget = JumpTargetMidPoint(newPoint, previousPoint);
                            //Debug.Log("newPoint: " + newPoint + "previousPoint: " + previousPoint + " potentialJumpTarget: " + potentialJumpTarget);
                            if (potentialJumpTarget != Vector2.zero)
                            {
                                // If there's enough space on the y axis for us to make the jump.
                                //Debug.Log("potentialJumpTarget: " + potentialJumpTarget);
                                if (JumpHeightClearanceCheck(potentialJumpTarget))
                                {
                                    if (jumpTarget == Vector2.zero)
                                    {
                                        jumpTarget = potentialJumpTarget;
                                        return jumpTarget;
                                    }
                                }
                                else
                                {
                                    //Debug.Log("DistanceBetween is above DistanceMaximum");
                                    return Vector2.zero;
                                }
                            }
                            else
                            {
                                //Debug.Log("JumpHeightCheck failed?");
                                return Vector2.zero;
                            }
                        }
                        else
                        {
                            // If our newPoint isn't null, but our previousPoint is for some reason,
                            // Then we want to run the method EstimateTargetFromOnePoint with our newPoint to devise the missing vector.
                            return EstimateTargetFromOnePoint(newPoint);
                        }

                    }
                    else
                    {
                        // If our newPoint is null, then we need to check if our previousPoint is as well
                        //Debug.Log("newPoint == zero");
                        if (previousPoint != Vector2.zero)
                        {
                            // If we do at least have a previousPoint, we can use that as a place to work from.
                            //Debug.Log("previousPoint != zero");
                            // If both newPoint and jumpCast.collider are null/zero, then we work from the
                            // previousPoint instead, as that will be the last point of contact with the obstacle from our ray.
                            return EstimateTargetFromOnePoint(previousPoint);
                        }
                        else
                        {
                            // If our newPoint AND previousPoints are both null, then we have to make up vectors
                            // based off of the rayCastOrigin instead.
                            // I added this if statement because I think it was erronously going to this portion
                            // due to there obviously being no previousPoint if it's starting at 0.
                            if (x != 0)
                            {
                                //Debug.Log("previous point is also zero, but let's make up our own luck.");
                                return EstimateTargetFromOnePoint(rayCastOrigin);
                            }

                        }
                    }
                }
            }
            // Add the jumpHeightMinimum to x so we can incrementally progress to it with the while loop.
            x += jumpHeightMinimum;
        }

        // If we've gotten this far without returning another value, just return zero to appease return rules.
        return Vector2.zero;
    }

    bool JumpGapCheck()
    {
        // Test 1: Detect if there's a gap in front of us that would require us to jump over it or find another way.
        Vector2 rayCastOrigin = new Vector2(transform.position.x + lookDirection.x, transform.position.y);
        // Simply cast a ray down, a bit in front of us, and if there's no collider, then there's a gap.
        RaycastHit2D jumpCast = Physics2D.BoxCast(rayCastOrigin, col2D.bounds.size, 0.0f, Vector2.down, jumpFallCheckDistance, jumpLayers);
        //Physics2D.Raycast(rayCastOrigin, Vector2.down, jumpFallCheckDistance, jumpLayers);
        Debug.DrawRay(rayCastOrigin, Vector2.down * jumpFallCheckDistance, Color.red, 2.0f);
        if (jumpCast.collider == null)
        {
            return true;
        }
        return false;
    }

    Vector2 JumpGapDistanceCheck()
    {
        // Test 2: If there's a gap in front of us that would require us to jump over it, we want to figure out the distance of that gap
        // and, if we can jump it, where the landing position will be to return that for our target.
        // Our eventual holder for the rayCastOrigin.
        Vector2 rayCastOrigin = new Vector2(transform.position.x + lookDirection.x, transform.position.y - (transform.localScale.y - 0.1f));
        // We'll want to set an origin position that's a little ahead of the object and a little beneath it
        // So that we get advanced position of upcoming gaps beneath us.
        // We then want to loop until we reach our jumpFallCheckDistance below us, sending rays out from that lowest spot, upward
        float x = 0.0f;
        while (x < jumpFallCheckDistance)
        {
            // Modify a vector with the changing x loop. Making it negative. Though you could change it so that x starts as the variable
            // and then subtract from it to go bottom to top?
            Vector2 rayCastFallDirect = rayCastOrigin + new Vector2(0, -x);
            RaycastHit2D jumpCast = Physics2D.Raycast(rayCastFallDirect, lookDirection, jumpDistanceMaximum, jumpLayers);
            //Debug.DrawRay(rayCastFallDirect, lookDirection * jumpDistanceMaximum, Color.red, 2.0f);
            if (jumpCast.collider != null)
            {
                //If we've hit an object that we can jump to below us.
                //Debug.Log("Collider hit: " + jumpCast.point);
                // We then want the distance between our ray's origin and the point of contact on the collider.
                float distanceBetween = Vector2.Distance(rayCastOrigin, jumpCast.point);
                // See if the distance between the origin and point of contact is greater than the minimum space needed to cause a jump.
                // We check three: the jumpFallCheckDistance just to be sure, the jumpDistanceMaximum to see if it's cose enough, and the
                // jumpDistanceMinimum so that we're not needlessly jumping a small thing. 
                if (distanceBetween <= jumpFallCheckDistance && distanceBetween <= jumpDistanceMaximum && distanceBetween >= jumpDistanceMinimum)
                {
                    Vector2 jumpTarget = FindJumpTarget(jumpCast.point);
                    //Debug.Log("FindJumpTarget: " + jumpTarget);
                    return jumpTarget;
                }
            }
            x += 0.5f;
        }
        return Vector2.zero;
    }

    Vector2 JumpObstacleCheck()
    {
        // Test 4: See if there's an object in front of us, and if we can jump over it.
        // Old way: rayCastOrigin = new Vector2(transform.position.x, transform.position.y + 0.06f - (transform.localScale.y / 2));
        // old way 2: Vector2 transformBottomPoint = new Vector2(transform.position.x, transform.position.y - (transform.localScale.y / 2));
        // Old way 2: Vector2 rayCastOrigin = new Vector2(transform.position.x, transform.position.y - (transform.localScale.y / 2) + jumpHeightMinimum);
        Vector2 transformBottomPoint = new Vector2(transform.position.x, transform.position.y - col2D.bounds.extents.y);
        Vector2 rayCastOrigin = new Vector2(transform.position.x, transform.position.y - col2D.bounds.extents.y + jumpHeightMinimum);
        // First cast a ray in front of us and slightly beneath our y position to detect if an obstacle is ahead.
        RaycastHit2D jumpCast = Physics2D.Raycast(rayCastOrigin, lookDirection, jumpLookAheadDist, jumpLayers);
        //Debug.Log("JumpAI: JumpObstacleCheck: rayCastOrigin: " + rayCastOrigin + " lookDirection: " + lookDirection + " jumpLookAheadDist: " + jumpLookAheadDist + " jumpLayers: " + jumpLayers);
        Debug.DrawRay(rayCastOrigin, lookDirection * jumpLookAheadDist, Color.green, 2.0f);
        if (jumpCast.collider != null)
        {
            //Debug.Log("JumpAI: JumpObstacleCheck: jumpCast.collider: " + jumpCast.collider.name + " passing this to FindJumpTarget: " + transformBottomPoint);
            // Then find the jumpTarget with our rayCastOrigin.
            Vector2 jumpTarget = FindJumpTarget(transformBottomPoint);
            return jumpTarget;
        }
        // If we've gotten this far without returning another value, just return zero to appease return rules.
        return Vector2.zero;
    }

    Vector2 JumpPlatformCheck()
    {
        // Test 5: Detect if there's a platform above us that we can jump to if needed.
        Vector2 rayCastOrigin = new Vector2(transform.position.x + lookDirection.x, transform.position.y);
        // Simply cast a ray up, a bit in front of us, and if there's a collider, then there's a platform.
        RaycastHit2D jumpCast = Physics2D.Raycast(rayCastOrigin, Vector2.up, jumpHeightMaximum, jumpLayers);
        Debug.DrawRay(rayCastOrigin, Vector2.up * jumpHeightMaximum, Color.blue, 2.0f);
        if (jumpCast.collider != null)
        {
            // Check the distances so that we only catch platforms within our maximum and minimum.
            float distanceBetweenYOnly = Vector2.Distance(new Vector2(0, rayCastOrigin.y), new Vector2(0, jumpCast.point.y));
            distanceBetweenYOnly = Mathf.Round(distanceBetweenYOnly * 10.0f) * 0.1f;
            // Test if the obstacle is within our heightmaximum and minimum to see if we need and can jump over it.
            if (distanceBetweenYOnly <= jumpHeightMaximum && distanceBetweenYOnly >= jumpHeightMinimum)
            {
                // Return the collision point, which will be, likely, the bottom of the platform.
                // We can then use this value to find our jumpTarget with another method call elsewhere.
                return jumpCast.point;
            }
        }
        return Vector2.zero;
    }

    public void JumpFromClimbableToClimbableCheck(Transform targetClimbable)
    {
        Debug.Log("JumpAI 706: " + targetClimbable.name);
        //Test 6: For when we're jumping from a climbable to another we need some special conditions.
        // This method is so far only called from the ClimbingAI script when it needs to jump from one to another climbable.
        Vector2 rayCastOrigin = transform.position;
        Vector2 jumpTarget = Vector2.zero;
        if(targetClimbable.position.x > transform.position.x)
        {
            lookDirection = Vector2.right;
        } else if (targetClimbable.position.x < transform.position.x)
        {
            lookDirection = Vector2.left;
        }
        
        // If the target is below us we need the rayCastOrigin to be the point of our cast's collision rather than our transform.position
        // or else our jumpTarget is going to be within the climbable with us rather than in relation to our target climbable below us.
        if(targetClimbable.position.y < transform.position.y)
        {
            //direction must be: destination - source (ORDER IS IMPORTANT)
            Vector3 direc = targetClimbable.position - transform.position;
            RaycastHit2D[] jumpCast = Physics2D.RaycastAll(rayCastOrigin, direc, jumpDistanceMaximum, LayerMask.GetMask("Climbable"));
            Debug.DrawRay(rayCastOrigin, direc * jumpDistanceMaximum, Color.white, 2.0f);
            if (jumpCast.Length != 0)
            {
                for(int i = 0; i < jumpCast.Length; i++)
                {
                    if(jumpCast[i].collider.transform == targetClimbable)
                    {
                        rayCastOrigin = jumpCast[i].point;
                    }
                }
            }
        }

        // Then find the jumpTarget with our rayCastOrigin.
        jumpTarget = FindJumpTarget(rayCastOrigin);

        if (jumpTarget != Vector2.zero)
        {
            // Find the middle distance between the climbable's center position and the jumpTarget, to prevent us from falling short or going too far.
            float valX = (targetClimbable.position.x + jumpTarget.x) * 0.5f;
            float valY = (targetClimbable.position.y + jumpTarget.y) * 0.5f;
            jumpTarget = new Vector2(valX, valY);
            // Log the current jumpHeightArc since we're going to modify it below. We do this because our calculations elsewhere use jumpHeightArc
            // so we can't just modify things locally, we have to modify the global value used elsewhere.
            // We do all this because otherwise the jumpHeightArc is sometimes too much for what we need, and we might hit things above us, messing
            // up our jump. But, sometimes we need more oomph to reach our jumpTarget, so then we need to revert to our initial value.
            float tempJumpHeightArc = jumpHeightArc;
            // Determine the difference in height between our y position and our jumpTarget's
            float difY = jumpTarget.y - transform.position.y;
            // Get the absolute positive value
            jumpHeightArc = Mathf.Abs(difY);
            // If it's above a certain threshold, 2.3, we likely just need the initial value instead.
            // but we want to run a check to make sure we're not going to hit anything when we do the jump.
            if(jumpHeightArc > 2.3 || jumpHeightArc < 1.6)
            {
                // Determine what direction we should check, i.e. what direction we're going to be jumping.
                Vector3 rayDir = Vector2.zero;
                if (jumpTarget.x > transform.position.x)
                {
                    rayDir = Vector2.right;
                }
                else
                {
                    rayDir = Vector2.left;
                }
                // use that direction to cast a ray up to the distance of the jumpHeightArc, seeing if we'd collide with anything if we made such a jump
                RaycastHit2D blockRay = Physics2D.Raycast(transform.position + rayDir, Vector2.up, jumpHeightArc, jumpLayers);
                if (blockRay.collider != null)
                {
                    // If there is an object there, then adjust the jumpHeightArc to take the intial value and subtract the distance between
                    // us and the impact point of the ray. This should return a value that's enough for us to jump without bumping into the object.
                    jumpHeightArc -= blockRay.distance;
                } else
                {
                    // If there isn't anything there, then we can just
                    // Revert the jumpHeightArc to the initial value.
                    jumpHeightArc = tempJumpHeightArc;
                }
            }

            // If the jumpTarget is good, then do the jump and let the ClimbingAI know we attempted it.
            Jump(jumpTarget);
            if (transform.TryGetComponent<ClimbingAI>(out ClimbingAI component))
            {
                Debug.Log("746 jumpTarget: " + jumpTarget);
                component.JumpAttempted();
            }
            // Return the jumpHeightArc to its original value.
            jumpHeightArc = tempJumpHeightArc;
        } else
        {
            // If we're unable to get a good JumpTarget, then let the ClimbingAI know it failed.
            if (transform.TryGetComponent<ClimbingAI>(out ClimbingAI component))
            {
                component.JumpCheckFailed();
            }
        }
    }

    public void JumpToElevator(Transform targetElevator)
    {
        //Test 7: For when we're jumping onto an elevator we need some special conditions.
        // This method is so far only called from the ElevatorUsingAI script.
        /*
        // We want to also give ourselves a bit of room for the jump so we don't hit a raised elevator platform and mess up the jump.
        // So we backpeddle a bit before making the jump.
        Vector2 startBackPos = new Vector2(transform.position.x, transform.position.y);
        if (targetElevator.position.x > transform.position.x)
        {
            // go left a bit
            startBackPos -= new Vector2(0.5f, 0.0f);
        }
        else
        {
            // go right a bit
            startBackPos += new Vector2(0.5f, 0.0f);
        }
        Debug.Log("startBackPos: " + startBackPos);
        transform.position = startBackPos;
        //transform.position = Vector2.MoveTowards(transform.position, startBackPos, 1.0f * Time.fixedDeltaTime);
        */

        Vector2 jumpTarget = FindJumpTarget(transform.position);

        if (jumpTarget != Vector2.zero)
        {
            // Find the middle distance between the elevators's center position and the jumpTarget, to prevent us from falling short or going too far.
            float valX = (targetElevator.position.x + jumpTarget.x) * 0.5f;
            float valY = (targetElevator.position.y + jumpTarget.y) * 0.5f;
            jumpTarget = new Vector2(valX, valY);
            // Log the current jumpHeightArc since we're going to modify it below. We do this because our calculations elsewhere use jumpHeightArc
            // so we can't just modify things locally, we have to modify the global value used elsewhere.
            // We do all this because otherwise the jumpHeightArc is sometimes too much for what we need, and we might hit things above us, messing
            // up our jump. But, sometimes we need more oomph to reach our jumpTarget, so then we need to revert to our initial value.
            float tempJumpHeightArc = jumpHeightArc;
            // Get the absolute positive value
            jumpHeightArc = 2.5f;

            // If the jumpTarget is good, then do the jump and let the ElevatorUsingAI know we attempted it.
            Jump(jumpTarget);
            if (transform.TryGetComponent<ElevatorUsingAI>(out ElevatorUsingAI component))
            {
                component.JumpAttempted();
            }
            // Return the jumpHeightArc to its original value.
            jumpHeightArc = tempJumpHeightArc;
        }
        else
        {
            // If we're unable to get a good JumpTarget, then let the ElevatorUsingAI know it failed.
            if (transform.TryGetComponent<ElevatorUsingAI>(out ElevatorUsingAI component))
            {
                component.JumpCheckFailed();
            }
        }
    }

    void Jump(Vector2 jumpTargetVector)
    {
        Vector3 vec3Target = (Vector3)jumpTargetVector;
        // TODO: Remove DrawPath once you're done testing.
        DrawPath(vec3Target);
        rb.velocity = CalculateJumpTrajectory(vec3Target).initialVelocity;
        Debug.Log("JumpAI: Actual Jump! performed! 817");
        isJumping = false;
    }

    #region SebastianLagueMethodsOnKinematicEquations
    // This region is based off of code from here: https://www.youtube.com/watch?v=IvT8hjy6q4o
    // and here: https://github.com/SebLague/Kinematic-Equation-Problems/tree/master/Kinematics%20problem%2002/Assets/Scripts
    LaunchData CalculateJumpTrajectory(Vector3 jumpTargetVec)
    {
        float displacementY = jumpTargetVec.y - rb.position.y;
        Vector3 displacementXZ = new Vector3(jumpTargetVec.x - rb.position.x, 0, jumpTargetVec.z - transform.position.z);
        float time = Mathf.Sqrt(-2 * jumpHeightArc / jumpGravityInverseFactor) + Mathf.Sqrt(2 * (displacementY - jumpHeightArc) / jumpGravityInverseFactor);
        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * jumpGravityInverseFactor * jumpHeightArc);
        Vector3 velocityXZ = displacementXZ / time;

        if (float.IsNaN(velocityXZ.x) || float.IsNaN(velocityXZ.y) || float.IsNaN(velocityXZ.z) || float.IsNaN(velocityY.x) || float.IsNaN(velocityY.y) || float.IsNaN(velocityY.z))
        {
            velocityY = Vector3.zero;
            velocityXZ = Vector3.zero;
        }
        return new LaunchData(velocityXZ + velocityY * -Mathf.Sign(jumpGravityInverseFactor), time);
    }

    void DrawPath(Vector3 jumpTargetVec)
    {
        // Used for debug purposes, helps you see the path of the jump in Scene view.
        LaunchData launchData = CalculateJumpTrajectory(jumpTargetVec);
        Vector3 previousDrawPoint = rb.position;

        int resolution = 30;
        for (int i = 1; i <= resolution; i++)
        {
            float simulationTime = i / (float)resolution * launchData.timeToTarget;
            Vector3 displacement = launchData.initialVelocity * simulationTime + Vector3.up * jumpGravityInverseFactor * simulationTime * simulationTime / 2f;
            Vector3 drawPoint = transform.position + displacement;
            Debug.DrawLine(previousDrawPoint, drawPoint, Color.green);
            previousDrawPoint = drawPoint;
        }
    }

    struct LaunchData
    {
        public readonly Vector3 initialVelocity;
        public readonly float timeToTarget;

        public LaunchData(Vector3 initialVelocity, float timeToTarget)
        {
            this.initialVelocity = initialVelocity;
            this.timeToTarget = timeToTarget;
        }

    }
    #endregion

    
}
