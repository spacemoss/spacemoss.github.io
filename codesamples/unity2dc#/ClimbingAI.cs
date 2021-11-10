using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;


public class ClimbingAI : MonoBehaviour
{

    public bool climbingEnabled = true;
    [Tooltip("Enables jumping from one climbable to another. REQUIRES JUMPAI COMPONENT ENABLED.")]
    public bool jumpFromClimbable2Climbable = true; // If we want to be able to jump from one climbable to another. Requires JumpAI component enabled.
    [Tooltip("If we should wait for others to finish climbing before we climb.")]
    public bool waitForOthersClimbing = true; // If we want to wait until others are done climbing

    [Tooltip("If we should move out of the way for oncoming climbing traffic.")]
    public bool moveForClimbTraffic = true; // If we want to move out of the way for oncoming climbing traffic

    [Tooltip("Distance for ray detecting oncoming traffic on climbable.")]
    public float visionDistance = 2.0f; // How far we can see ahead while checking for traffic.
    [Tooltip("Max distance from us that we can detect a climbable.")]
    public float maxClimbableDetDist = 30.0f; // The maximum distance from us that we can detect a climbable.
    [Tooltip("Layers we want to dodge out of the way for.")]
    public string[] trafficLayers = { "NPC", "Player" }; // Layers we want to dodge out of the way for.
    public string[] walkableLayers = { "Ground", "Climbable", "Transport" }; // The layers we'll check that we can walk onto.

    public LayerMask climbableMask;
    public int climbableMaskInt = 7;

    public float speed = 1.0f; // Speed of movement in general
    public float climbSpeed = 0.5f; // Speed when we're climbing.

    public float defaultGravity = 1.0f;
    [Tooltip("Used for our isGround calculation.")]
    public float isGroundedCheckOffset = 0.1f; // Used for our isGrounded calculation.
    [Tooltip("Distance we should be within to make us stop going after the target.")]
    public float distanceFromTarget2Stop = 2.0f; // The distance we should be within to make us stop going after the target.

    bool isClimbing;
    bool withinClimbableArea;
    bool aligningToClimbable; // If our movement is due to aligning with the climbable, we use this so we can move on to climbing once aligned.
    bool isGrounded;
    bool onTopClimbable = false; // Generic bool, whether we can climb or not, to tell if we're on top of a climable, used when making sure we don't fall through.

    bool pauseClimbing;

    bool reachedEndPosition; // Used to keep track of if we've reached the end position goal, to ensure we don't do a CheckTargetPosition when unneeded.

    // used for dodging traffic
    bool dodgedTraffic;

    bool jumpAttempted;
    bool fromClimbableToClimbable; // When moving from a climbable to a climbable.
    List<Transform> allowedClimbableAccess; // Climbables we've been allowed to access.

    Transform target;
    Transform climbableTarget;
    Transform withinClimbable;
    Transform currentClimbableArea;
    Transform previousClimbable; // Track the previous climbable so that we don't keep calling ExitClimbable if attempting to stay on it.
    Transform supportingClimbable;

    float tempSpeed;

    Rigidbody2D rb;
    Collider2D col2D;
    private MouseControls mouseControl; // Used for click activation of actions. TODO: Remove after testing, though use code for other things.

    Vector2 goalPosition; // The goal we're trying to reach while moving to a climbable, or climbing it.

    private int walkLayers; // Layers, in integer form, that we'll want to detect we can walk on.

    private bool islanded; // Used for determining if we're islanded on a climbable or not.

    private bool movingToWithinPoint; // Used in special cases when our FindClimbable methods are returning null, but only because we're moving within a climbable to its end.

    private void Awake()
    {

        mouseControl = new MouseControls();
        allowedClimbableAccess = new List<Transform>();
    }

    void Start()
    {
        // Our rigid body.
        rb = transform.GetComponent<Rigidbody2D>();
        col2D = transform.GetComponent<Collider2D>();
        // For the mouse controls, TODO: remove this after testing.
        mouseControl.Mouse.Click.started += _ => DetectObject();
        walkLayers = LayerMask.GetMask(walkableLayers);
        if (jumpFromClimbable2Climbable)
        {
            if (!gameObject.GetComponent<JumpAI>())
            {
                Debug.LogWarning("ClimbingAI for " + gameObject.name + " : jumpFromClimbable2Climbable is ENABLED but no JumpAI component is found! Please add JumpAI component to utilize functionality. jumpFromClimbable2Climbable has been made FALSE to avoid errors. Be sure to make TRUE again once you've added JumpAI component.");
                jumpFromClimbable2Climbable = false;
            } else
            {
                if (!gameObject.GetComponent<JumpAI>().isActiveAndEnabled)
                {
                    Debug.LogWarning("ClimbingAI for " + gameObject.name + " : jumpFromClimbable2Climbable is ENABLED but the JumpAI component is NOT Active and Enabled! Please activate/enable JumpAI component to utilize functionality. jumpFromClimbable2Climbable has been made FALSE to avoid errors. Be sure to make TRUE again once you've added JumpAI component.");
                    jumpFromClimbable2Climbable = false;
                }
            }
        }
    }

    private void Update()
    {
        isGrounded = Physics2D.Raycast(transform.position, -Vector3.up, GetComponent<Collider2D>().bounds.extents.y + isGroundedCheckOffset, LayerMask.GetMask("Ground"));
    }

    void FixedUpdate()
    {
        // Process:
        // 1) Find nearest Climbable we can use (FindClimbableUp and FindClimbableDown, though maybe combine into just one?)
        // 2) Move to the climbable. Create a temporary function to do this.
        // 3) Attempt handshake with Climbable object to get permission to use.
        // 4)
        //      a) If allowed, then we need an actual Climbing function (handle this within this script, don't have PathingAI handle it).
        //      b) If denied, then we need some other handling of that denial.
        // 5) Exit the climbable and do post-climb handshake with Climbable object.
        // 6) Handle next step if one is needed? Such as getting a new climbable while on top of another.

        // If we have a goal position that isn't zero we want to intiate our movement code.
        if (goalPosition != Vector2.zero)
        {
            // Check to see if our climbing has been paused.
            if (!pauseClimbing)
            {
                // If we're not within a climbable, then move freely regardless as we're likely on our way to the climbable
                if (withinClimbable == null)
                {
                    transform.position = Vector2.MoveTowards(transform.position, goalPosition, tempSpeed * Time.fixedDeltaTime);
                }
                else
                {
                    // But if we're within a climbable and withinClimbable isn't null, then we're climbing and need to adhere
                    // to the rules of other methods as to when we should stop climbing or not. As some methods set isClimbing false
                    // if the Climbable tells them to wait etc.
                    if (isClimbing)
                    {
                        transform.position = Vector2.MoveTowards(transform.position, goalPosition, tempSpeed * Time.fixedDeltaTime);
                    }
                    else
                    {
                        transform.position = Vector2.MoveTowards(transform.position, goalPosition, tempSpeed * Time.fixedDeltaTime);
                    }
                }
                // Check to see if we're on top a climbable
                ClimbingOnTop();
                // Check to see where our target is in case we need to change our direction while we're moving.
                CheckTargetPosition();
                // Check to see if there's oncoming traffic on the climbable.
                if (moveForClimbTraffic)
                {
                    CheckTraffic();
                }
            }
            // If our distance between the goalPosition and our transform.position is below the threshold
            // then we can treat it as if we've reached our goalPosition
            if (Vector2.Distance(goalPosition, transform.position) <= 0.02f)
            {
                Debug.Log("ClimbingAI: FixedUpdate: Reached goalPosition?");
                // Check to see if we're on top a climbable
                ClimbingOnTop();
                goalPosition = Vector2.zero;
                // If we're aligningToClimbable, then we can say we're withinClimbable it, and begin Climb method
                if (aligningToClimbable)
                {
                    if (climbableTarget != null && withinClimbable != climbableTarget)
                    {
                        aligningToClimbable = false;
                        withinClimbable = climbableTarget;
                        Climb();
                    }
                }
                else
                {
                    // If we were climbing when we reached our goal, call EndClimb method.
                    if (isClimbing)
                    {
                        Debug.Log("ClimbingAI: 181: FixedUpdate calling EndClimb");
                        EndClimb();
                    }
                    else
                    {
                        if(climbableTarget != null)
                        {
                            // If we're not climbing, but still within the climbable itself, we need to do some checks and either climb or requestclimb access
                            if (onTopClimbable && climbableTarget.position.y < transform.position.y && target.position.y < transform.position.y)
                            {
                                if (allowedClimbableAccess.Contains(climbableTarget))
                                {
                                    Climb();
                                }
                                else
                                {
                                    RequestClimb(climbableTarget);
                                }
                            }
                            else if (isGrounded && climbableTarget.position.y > transform.position.y && target.position.y > transform.position.y)
                            {
                                if (allowedClimbableAccess.Contains(climbableTarget))
                                {
                                    Climb();
                                }
                                else
                                {
                                    RequestClimb(climbableTarget);
                                }
                            }
                            else if (!isGrounded && !onTopClimbable && withinClimbable != null)
                            {
                                if (allowedClimbableAccess.Contains(climbableTarget))
                                {
                                    if(supportingClimbable != null)
                                    {
                                        if(climbableTarget != withinClimbable)
                                        {
                                            Climb();
                                        } else
                                        {
                                            Debug.Log("ClimbingAI: 221: FixedUpdate calling EndClimb");
                                            EndClimb();
                                        }
                                    }
                                    else
                                    {
                                        Climb();
                                    }
                                    
                                }
                                else
                                {
                                    RequestClimb(climbableTarget);
                                }
                            }
                        }
                    }
                }
            }
        }
        
    }

    public void SeekClimbable(Transform currentTarget)
    {
        // Set our climbableTarget to what climbableUp we're able to find.
        target = currentTarget;
        pauseClimbing = false;
        if (target.position.y > transform.position.y)
        {
            Debug.Log("ClimbingAI: SeekClimbable calling FindClimbableUP for target: " + target.name);
            climbableTarget = FindClimbableUp(target);
            if(climbableTarget != null)
            {
                Debug.Log("ClimbingAI: ClimbableTarget: " + climbableTarget.name);
            } else
            {
                Debug.Log("ClimbingAI: SeekClimbable: ClimbableTarget is Null from FindClimbableUp results.");
            }
        }
        else
        {
            climbableTarget = FindClimbableDown2(target);
        }

        if (climbableTarget != null)
        {
            // If we've found one, then MoveAndRequest to it.
            MoveAndRequest();
        } else
        {
            // If we haven't found one, and we're not movingToWithinPoint, we've reached our limit and cannot climb.
            if (!movingToWithinPoint)
            {
                Debug.Log("ClimbingAI: 264: calling ClimbingReachedLimit");
                ClimbingReachedLimit();
            }
        }
    }

    public Vector2 ClimbableAlignmentVec()
    {
        // This method helps us align with the climbable's entry points, which, ideally, will help us know where to start and end our climbs
        Vector2 alignmentGoal = Vector2.zero;
        // Test if there's children to get TopEntry and BottomEntry object positions.
        if (climbableTarget.childCount != 0)
        {
            Vector2 topEntryPos = Vector2.zero;
            Vector2 bottomEntryPos = Vector2.zero;
            for (int i = 0; i < climbableTarget.childCount; i++)
            {
                if (climbableTarget.GetChild(i).name == "TopEntry")
                {
                    topEntryPos = climbableTarget.GetChild(i).position;
                }
                else if (climbableTarget.GetChild(i).name == "BottomEntry")
                {
                    bottomEntryPos = climbableTarget.GetChild(i).position;
                }
            }
            // If we have both, then figure out which is closer and use that.
            if (topEntryPos != Vector2.zero && bottomEntryPos != Vector2.zero)
            {
                float distanceTop = Vector2.Distance(topEntryPos, transform.position);
                float distanceBottom = Vector2.Distance(bottomEntryPos, transform.position);
                if (distanceTop < distanceBottom)
                {
                    // meaning our top is closer than bottom
                    alignmentGoal = topEntryPos;
                }
                else
                {
                    alignmentGoal = bottomEntryPos;
                }

                if(climbableTarget.GetComponent<Collider2D>().bounds.size.x > climbableTarget.GetComponent<Collider2D>().bounds.size.y)
                {
                    Debug.Log("ClimbingAI: ClimbableAlignmentVec: bounds.size.x > bounds.size.y");
                    if(alignmentGoal.x > transform.position.x && target.position.x < alignmentGoal.x)
                    {
                        if(alignmentGoal == topEntryPos)
                        {
                            alignmentGoal = bottomEntryPos;
                        } else
                        {
                            alignmentGoal = topEntryPos;
                        }
                    } else if (alignmentGoal.x < transform.position.x && target.position.x > alignmentGoal.x)
                    {
                        if (alignmentGoal == topEntryPos)
                        {
                            alignmentGoal = bottomEntryPos;
                        }
                        else
                        {
                            alignmentGoal = topEntryPos;
                        }
                    }
                } else
                {
                    Debug.Log("ClimbingAI: ClimbableAlignmentVec: bounds.size.y > bounds.size.x");
                    if (alignmentGoal.y > transform.position.y && target.position.y < alignmentGoal.y)
                    {
                        Debug.Log("ClimbingAI: ClimbableAlignmentVec: 334: climbable target: " + climbableTarget.name + " alignmentGoal: " + alignmentGoal + " transform.position: " + transform.position + " target.position: " + target.position);
                        if (alignmentGoal == topEntryPos)
                        {
                            alignmentGoal = bottomEntryPos;
                        }
                        else
                        {
                            alignmentGoal = topEntryPos;
                        }
                        Debug.Log("ClimbingAI: ClimbableAlignmentVec: 343: climbable target: " + climbableTarget.name + " alignmentGoal: " + alignmentGoal);
                    }
                    else if (alignmentGoal.y < transform.position.y && target.position.y > alignmentGoal.y)
                    {
                        Debug.Log("ClimbingAI: ClimbableAlignmentVec: 346: climbable target: " + climbableTarget.name + " alignmentGoal: " + alignmentGoal + " transform.position: " + transform.position + " target.position: " + target.position);

                        if (alignmentGoal == topEntryPos)
                        {
                            alignmentGoal = bottomEntryPos;
                        }
                        else
                        {
                            alignmentGoal = topEntryPos;
                        }
                        Debug.Log("ClimbingAI: ClimbableAlignmentVec: 357: climbable target: " + climbableTarget.name + " alignmentGoal: " + alignmentGoal);
                    }
                }
            }
        }
        else
        {
            // If there are no children then just use the climbableTarget position.
            alignmentGoal = climbableTarget.position;
        }
        // If our alignmentGoal isn't zero, we set the aligningToClimbable bool, and also if we're grounded
        // we alter the alignmentGoal to just be the x position if we're within a certain distance on the y.
        if (alignmentGoal != Vector2.zero)
        {
            Debug.Log("ClimbingAI: AlignmentVec: alignmentGoal != zero!");
            if (isGrounded)
            {
                Debug.Log("ClimbingAI: AlignmentVec: isGrounded");
                // If the difference of the y is less than 1.0f, this is so that if the distance is greater
                // we could take other action of movement to get there, like maybe if we need to jump? or if we can fly? I dunno.
                if (Mathf.Abs((alignmentGoal.y - transform.position.y)) <= 1.0f)
                {
                    alignmentGoal = new Vector2(alignmentGoal.x, transform.position.y);
                    Debug.Log("ClimbingAI: AlignmentVec: isGrounded: alignmentGoal: " + alignmentGoal);
                } else
                {
                    Debug.Log("ClimbingAI: AlignmentVec, the difference is more than 1.0f");
                    alignmentGoal = new Vector2(alignmentGoal.x, transform.position.y);
                }
            }
            else
            {
                // If we're not grounded then we want to figure out another way to align that won't cause us to
                // move all the way to the position of the Entry point before climbing (i.e. moving where we don't really need to go first)
                // So, we'll want to determine if we should just align to the x or y based on the proportions of the climbable
                if (climbableTarget.GetComponent<Collider2D>().bounds.size.x > climbableTarget.GetComponent<Collider2D>().bounds.size.y)
                {
                    Debug.Log("ClimbingAI: AlignmentVec: 396: climbableTarget.GetComponent<Collider2D>().bounds.size.x: " + climbableTarget.GetComponent<Collider2D>().bounds.size.x + " climbableTarget.GetComponent<Collider2D>().bounds.size.y: " + climbableTarget.GetComponent<Collider2D>().bounds.size.y);
                    // The x is longer than the y, so align just to the y.
                    alignmentGoal = new Vector2(transform.position.x, alignmentGoal.y);
                }
                else
                {
                    Debug.Log("ClimbingAI: AlignmentVec: 402: climbableTarget.GetComponent<Collider2D>().bounds.size.x: " + climbableTarget.GetComponent<Collider2D>().bounds.size.x + " climbableTarget.GetComponent<Collider2D>().bounds.size.y: " + climbableTarget.GetComponent<Collider2D>().bounds.size.y);
                    // The y is longer than the x, so align just to the x.
                    alignmentGoal = new Vector2(alignmentGoal.x, transform.position.y);
                }
                Debug.Log("ClimbingAI: AlignmentVec: notGrounded: alignmentGoal: " + alignmentGoal);
            }
            aligningToClimbable = true;
        }
        return alignmentGoal;
    }

    public void EnterClimbable()
    {
        // When we enter a climbable we want to set our goalPosition to ClimbableAlignmentVec so we can properly align.
        if (climbableTarget.gameObject.layer == climbableMaskInt)
        {
            fromClimbableToClimbable = false; // make this false since you don't need it at this point and it needs to be cleared.
            previousClimbable = null;
            if (!isGrounded)
            {
                rb.velocity = Vector2.zero;
                rb.gravityScale = 0f;
            }

            if (climbableTarget != withinClimbable)
            {
                // If we're not within the climbable we need to align to it.
                Debug.Log("ClimbingAI: EnterClimbable about to call ClimbableAlignmentVec, current goalPosition: " + goalPosition);
                goalPosition = ClimbableAlignmentVec();
                Debug.Log("ClimbingAI: 363: EnterClimbable: climbable target: " + climbableTarget.name + " ClimbableAlignmentVec GoalPosition: " + goalPosition);
            } else
            {
                Debug.Log("398");
                // Otherwise just begin climbing.
                Climb();
            }
        }
    }

    public void Climb()
    {
        // Our actual climbing function
        // First determine if the top or bottom entry points are closer to us and set our climbEndPosition accordingly
        // Then tell the climbableTarget that we're climbing. Then set our tempSpeed and goalPosition and isClimbing bool and gravityScale.
        Vector2 entryPosition;
        Vector2 climbEndPosition = transform.position;
        float posOffset = 0.3f; // A slight offset since typically the TopEntry and BottomEntry object positions aren't quite where we want to be.
        if (climbableTarget != null)
        {
            if (climbableTarget.childCount != 0)
            {
                // Check to see if the climbableTarget is taller than it is longer
                if (climbableTarget.GetComponent<Collider2D>().bounds.size.y > climbableTarget.GetComponent<Collider2D>().bounds.size.x)
                {
                    if ((climbableTarget.position.y > transform.position.y && target.position.y > climbableTarget.position.y) ||
                        (climbableTarget.position.y < transform.position.y && target.position.y > climbableTarget.position.y))
                    {
                        for (int i = 0; i < climbableTarget.childCount; i++)
                        {
                            if (climbableTarget.GetChild(i).name == "TopEntry")
                            {
                                entryPosition = climbableTarget.GetChild(i).position;
                                climbEndPosition = new Vector2(entryPosition.x, entryPosition.y - posOffset);
                            }
                        }
                    }
                    else if ((climbableTarget.position.y < transform.position.y && target.position.y < climbableTarget.position.y) ||
                        (climbableTarget.position.y > transform.position.y && target.position.y < climbableTarget.position.y))
                    {
                        for (int i = 0; i < climbableTarget.childCount; i++)
                        {
                            if (climbableTarget.GetChild(i).name == "BottomEntry")
                            {
                                entryPosition = climbableTarget.GetChild(i).position;
                                climbEndPosition = new Vector2(entryPosition.x, entryPosition.y - posOffset);
                            }
                        }
                    }
                }
                else
                {
                    // If the climbableTarget is wider than it is taller then we instead want to
                    // see which entry point is further from us (i.e. the point we're not just entering at)
                    // and set that as our climbEndPosition without an offset.
                    Transform topEntry = null;
                    Transform botEntry = null;
                    for (int i = 0; i < climbableTarget.childCount; i++)
                    {
                        if (climbableTarget.GetChild(i).name == "TopEntry")
                        {
                            topEntry = climbableTarget.GetChild(i);
                        }
                        else if (climbableTarget.GetChild(i).name == "BottomEntry")
                        {
                            botEntry = climbableTarget.GetChild(i);
                        }
                    }
                    if (topEntry != null && botEntry != null)
                    {
                        Vector2 topEntryDir = topEntry.position - transform.position;
                        float topEntryDist = topEntryDir.sqrMagnitude;
                        Vector2 botEntryDir = botEntry.position - transform.position;
                        float botEntryDist = botEntryDir.sqrMagnitude;
                        if (topEntryDist > botEntryDist)
                        {
                            climbEndPosition = new Vector2(topEntry.position.x, topEntry.position.y);
                        }
                        else
                        {
                            climbEndPosition = new Vector2(botEntry.position.x, botEntry.position.y);
                        }
                    }
                }
            }
            climbableTarget.GetComponent<Climbable>().NPCClimbing(transform);
            tempSpeed = climbSpeed;
            goalPosition = climbEndPosition;
            isClimbing = true;
            rb.gravityScale = 0;
        }
        else
        {
            SeekClimbable(target);
        }

    }

    public void EndClimb()
    {
        ClimbingOnTop();
        // Method for when we've reached the goal of our climb and wrap things up.
        // Note this is different from ExitClimbable, which is called when we leave the trigger.
        // It is also different from ClimbingReachedLimit, because we still want to check our distance
        // here to see if we need to take further action, such as finding another climbable, etc.
        isClimbing = false;
        tempSpeed = speed;
        climbableTarget = null;
        reachedEndPosition = true;
        goalPosition = Vector2.zero;
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, col2D.bounds.size.y, LayerMask.GetMask("Ground"));
        if (onTopClimbable)
        {
            Debug.Log("ClimbingAI: EndClimb: onTopClimbable True");
            rb.gravityScale = 0.0f;
            // We need to also inform the climbable we've exited it so that it makes the TopPlatform active again if it's using it as a ground
            // and also to clear some of our other variables there.
            if(currentClimbableArea != null)
            {
                Debug.Log("ClimbingAI: EndClimb: currentclimbableArea != null: " + currentClimbableArea.name);
                if (currentClimbableArea.TryGetComponent<Climbable>(out Climbable component))
                {
                    Debug.Log("ClimbingAI: 474");
                    component.NPCExit(transform);
                }
            }
        }
        else
        {
            if (isGrounded)
            {
                rb.gravityScale = defaultGravity;
            }
            else
            {
                if (withinClimbable != null)
                {
                    rb.gravityScale = 0.0f;
                }
            }
        }
        if (isGrounded)
        {
            Debug.Log("485 EndClimb");
            ClimbingReachedLimit();
            
        } else
        {
            Debug.Log("490 EndClimb");
            CheckDistanceFromTarget();
        }
        
    }

    public void ClimbingReachedLimit()
    {
        // Method for when we've reached the limit of our abilities, meaning we can either
        // no longer find any climbables, or we're close to our main target and no longer
        // need to look for climbables. End all functions.
        isClimbing = false;
        tempSpeed = speed;
        climbableTarget = null;
        goalPosition = Vector2.zero;
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, col2D.bounds.size.y, LayerMask.GetMask("Ground"));
        fromClimbableToClimbable = false;
        if(allowedClimbableAccess.Count != 0)
        {
            allowedClimbableAccess.Clear();
        }
        if (onTopClimbable)
        {
            rb.gravityScale = 0.0f;
        }
        else
        {
            if (isGrounded)
            {
                rb.gravityScale = defaultGravity;
            }
            else
            {
                if (withinClimbable != null)
                {
                    rb.gravityScale = 0.0f;
                }
            }
        }

        if(TryGetComponent<PathingAI>(out PathingAI component))
        {
            Debug.Log("ClimbingAI: ClimbingReachedLimit: Calling PAthingAI ClimbFunctionsCompleted.");
            component.ClimbFunctionsCompleted();
        }
    }

    public void ExitClimbable()
    {
        // Exiting a climable trigger object, make withinClimbableArea false.

        // Check to see if we're on top a climbable
        ClimbingOnTop();
        withinClimbableArea = false;
        currentClimbableArea = null;
        isClimbing = false;

        if (climbableTarget != null)
        {
            if (climbableTarget.gameObject.layer == climbableMaskInt)
            {
                if (withinClimbable == supportingClimbable && withinClimbable != null)
                {
                    if (withinClimbable.TryGetComponent<Climbable>(out Climbable component))
                    {
                        Debug.Log("ClimbingAI: 564: ExitClimbable telling component.NPCExit my transform.");
                        component.NPCExit(transform);
                    }
                    allowedClimbableAccess.Remove(withinClimbable);
                }
            }
        }
        withinClimbable = null;
        rb.gravityScale = defaultGravity;
    }

    #region MouseClickCode
    private void DetectObject()
    {
        // Method called by the Mouse.Click event in our Start method
        // Fires a ray from the main camera based on the Mouse.Position value
        // If it hits, gets the value of the collider it hit.
        Ray ray = Camera.main.ScreenPointToRay(mouseControl.Mouse.Position.ReadValue<Vector2>());
        RaycastHit2D hits2D = Physics2D.GetRayIntersection(ray);
        if(hits2D.collider != null)
        {
            Debug.Log("Mouse Click Ray Hit " + hits2D.collider.name);
            if(hits2D.collider.transform == transform)
            {
                SeekClimbable(PlayerManager.instance.player.transform);
                
            }
            
        }
    }

    private void OnEnable()
    {
        mouseControl.Enable();
    }

    private void OnDisable()
    {
        mouseControl.Disable();
    }
    #endregion

    #region Checks
    void CheckTargetPosition()
    {
        // Method for checking the target's current position and seeing if we need to readjust our goalposition
        // such as if the target moves below or above us while we're moving up or down.
        if(goalPosition != Vector2.zero && !reachedEndPosition && climbableTarget == withinClimbable)
        {
            if (target != null && climbableTarget != null)
            {
                // We want to check to make sure the target is within the climbable with us.
                bool changeToFollow = false;
                if (climbableTarget.TryGetComponent<Climbable>(out Climbable component))
                {
                    if(component.currentPlayerCollider != null)
                    {
                        if (target == component.currentPlayerCollider.transform)
                        {
                            changeToFollow = true;
                        }
                    }
                }
                if (changeToFollow)
                {
                    if(climbableTarget.GetComponent<Collider2D>().bounds.size.y > climbableTarget.GetComponent<Collider2D>().bounds.size.x)
                    {
                        if (goalPosition.y > transform.position.y && target.position.y < transform.position.y)
                        {
                            ChangeClimbingDirection();
                        }
                        else if (goalPosition.y < transform.position.y && target.position.y > transform.position.y)
                        {
                            ChangeClimbingDirection();
                        }
                    } else
                    {
                        if (goalPosition.x > transform.position.x && target.position.x < transform.position.x)
                        {
                            ChangeClimbingDirection();
                        }
                        else if (goalPosition.x < transform.position.x && target.position.x > transform.position.x)
                        {
                            ChangeClimbingDirection();
                        }
                    }
                    
                }
            }
        }
        
    }

    public void CheckDistanceFromTarget()
    {
        // Check the distance between our position and the target's
        // If it's greater than the distanceFromTarget2Stop, then determine if the target
        // is above us or below us, and call the appropriate FindClimbable method.
        float distance = Vector2.Distance(transform.position, target.position);
        bool limitReached = false;
        movingToWithinPoint = false; // This bool is used for when we're within a climbable based on FindClimbableUp/Down and we don't want to trigger ClimbingReachedLimit
        if (distance > distanceFromTarget2Stop)
        {
            Debug.Log("656");
            // If we're within a climbable we may first need to move to the other end of it, so we run some checks to see if the current
            // climbable we're within is between us and the target, and if it is, then we want to move to the end of it before calling another method.
            bool movedWithinClimbable = false;
            if(withinClimbable != null)
            {
                Debug.Log("661");
                if(withinClimbable.GetComponent<Collider2D>().bounds.size.y > withinClimbable.GetComponent<Collider2D>().bounds.size.x)
                {
                    if (target.position.y > transform.position.y)
                    {
                        if (withinClimbable.position.y > transform.position.y && withinClimbable.position.y < target.position.y)
                        {
                            Debug.Log("ClimbingAI: 668: Going down to get up?");
                            movedWithinClimbable = true;
                            MoveToEndOfWithinClimbable();
                        }
                    }
                    else if (target.position.y < transform.position.y)
                    {
                        if (withinClimbable.position.y < transform.position.y && withinClimbable.position.y > target.position.y)
                        {
                            Debug.Log("ClimbingAI: 697: Going up to get down?");
                            movedWithinClimbable = true;
                            MoveToEndOfWithinClimbable();
                        }
                    }
                } else
                {
                    if (target.position.x > transform.position.x)
                    {
                        if (withinClimbable.position.x > transform.position.x && withinClimbable.position.x < target.position.x)
                        {
                            movedWithinClimbable = true;
                            MoveToEndOfWithinClimbable();
                        }
                    }
                    else if (target.position.x < transform.position.x)
                    {
                        if (withinClimbable.position.x < transform.position.x && withinClimbable.position.x > target.position.x)
                        {
                            movedWithinClimbable = true;
                            MoveToEndOfWithinClimbable();
                        }
                    }
                }
                
            }
            // If we haven't moved within the climbable above, then we need to call the below methods based on target position.
            if(!movedWithinClimbable)
            {
                if (target.position.y > transform.position.y)
                {
                    climbableTarget = FindClimbableUp(target);
                }
                else
                {
                    climbableTarget = FindClimbableDown2(target);
                }
            }
        }
        else
        {
            Debug.Log("715");
            // If we've reached the target distance, then we can no longer do anything
            // and we've reached our limit. Stop the AI functionality.
            limitReached = true;
            ClimbingReachedLimit();
        }

        if (climbableTarget != null)
        {
            Debug.Log("724");
            MoveAndRequest();
        }
        else
        {
            // If we're still outside the target distance, meaning the target is stil farther than we'd like,
            // but the climbableTarget is null, we've also then reached our limit in that we can't find another
            // climbable.
            // We want though to ensure we're not just moving within a climbable and reached the end, as that
            // causes some variables to reset that we need, such as the goalPosition, so we want to use the bool to ensure the catch.
            if (!limitReached && !movingToWithinPoint) {
                Debug.Log("735");
                ClimbingReachedLimit();
            }
        }
    }

    void CheckIslandedClimbable()
    {
        // We want to check if we're stranded on a climbable, meaning that it doesn't a) expand around us
        // or b) connect to something we can move on. So, did we just climb up it with nothing around us at the top?
        float distance = transform.localScale.y;
        RaycastHit2D rayRightClimbable = Physics2D.Raycast((Vector2)transform.position + Vector2.right, Vector2.down, distance, climbableMask);
        RaycastHit2D rayLeftClimbable = Physics2D.Raycast((Vector2)transform.position + Vector2.left, Vector2.down, distance, climbableMask);
        RaycastHit2D rayRightWalk = Physics2D.Raycast((Vector2)transform.position + Vector2.right, Vector2.down, distance, walkLayers);
        RaycastHit2D rayLeftWalk = Physics2D.Raycast((Vector2)transform.position + Vector2.left, Vector2.down, distance, walkLayers);
        RaycastHit2D boxRightWalk = Physics2D.BoxCast((Vector2)transform.position + Vector2.right, transform.localScale, 0.0f, Vector2.down, distance, walkLayers);
        RaycastHit2D boxLeftWalk = Physics2D.BoxCast((Vector2)transform.position + Vector2.left, transform.localScale, 0.0f, Vector2.down, distance, walkLayers);
        if (rayRightClimbable.collider == null && rayLeftClimbable.collider == null && rayRightWalk.collider == null && rayLeftWalk.collider == null
            && boxRightWalk.collider == null && boxLeftWalk.collider == null)
        {
            islanded = true;
        } else
        {
            islanded = false;
        }
    }

    private void ClimbingOnTop()
    {
        // This function tests to see if we're on top of a climable surface or not.
        // First we need to calculate a bit from the bottom of our current position.
        Vector2 topPos = new Vector2(transform.position.x, transform.position.y + 0.3f);
        // Then we cast two boxes, one going below our player, the other going above.
        RaycastHit2D hitDown = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.down, 0.02f, climbableMask);
        RaycastHit2D hitUp = Physics2D.BoxCast(topPos, col2D.bounds.size, 0f, Vector2.up, -0.1f, climbableMask);
        // If the BoxCast going down isn't null, but the one going up is null, then we're on top of a climable surface.
        if (hitDown.collider != null && hitUp.collider == null)
        {
            supportingClimbable = hitDown.collider.transform;
            if (climbableTarget != null)
            {
                if (climbableTarget.position.y < rb.position.y)
                {
                    // NOTE: You changed "climbableTarget" to "target" in the if statement right here. Switch back if it fucks things up.
                    // This also may not be needed? Keep an eye on it.
                    if (climbingEnabled && target.position.y < rb.position.y && !withinClimbableArea)
                    {
                        Debug.Log("ClimbingAI: ClimbingOnTop: 880");
                        Transform downClimbable = FindClimbableDown2(climbableTarget);
                        if (downClimbable != null)
                        {
                            climbableTarget = downClimbable;
                        }
                        // We do this to make sure the climable we're on is the best climable down to the target
                        // and that we're not just passing over a climable on our way to another that's on the path.
                        if (climbableTarget == supportingClimbable.transform)
                        {
                            withinClimbableArea = true;
                        }
                    }
                }
            }
            onTopClimbable = true;
        }
        else if (hitDown.collider == null && hitUp.collider == null)
        {
            // Player is not on top of the climable surface.
            onTopClimbable = false;
        }
        else
        {
            // Player is not on top of the climable surface.
            onTopClimbable = false;
        }
    }

    void CheckTraffic()
    {
        // Method to move out of the way if a player or another NPC is in our way on the climbable.
        // First set up the layers we want to dodge
        int trafficInt = LayerMask.GetMask(trafficLayers);
        Vector2 pathDodge = Vector2.zero;
        RaycastHit2D hit1 = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.left, visionDistance, trafficInt);
        // We use the below bool and Raycast to see if we're about to fall out of the climbable. If we are, then we don't want to move anymore.
        bool dontFall = false;
        RaycastHit2D hit2 = Physics2D.Raycast(transform.position, Vector2.left, 1.0f, climbableMask);
        if (climbableTarget != null)
        {
            // Determine how we should dodge based on the scale properties of the climbableTarget
            if (climbableTarget.GetComponent<Collider2D>().bounds.size.x > climbableTarget.GetComponent<Collider2D>().bounds.size.y)
            {
                // If the climbableTarget is longer than it is taller.
                // And if our goal position is to the left or right of us.
                if (goalPosition.x < transform.position.x)
                {
                    // Dodge to up if there's traffic from the left, and dodge down if traffic from the right.
                    hit1 = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.left, visionDistance, trafficInt);
                    pathDodge = transform.position + new Vector3(0.0f, 0.5f);
                }
                else if (goalPosition.x > transform.position.x)
                {
                    hit1 = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.right, visionDistance, trafficInt);
                    pathDodge = transform.position + new Vector3(0.0f, -0.5f);
                }
            }
            else
            {
                // If the goal position is above us or below us.
                if(goalPosition.y > transform.position.y)
                {
                    // Dodge right if there's traffic from above and dodge left if there's traffic from below.
                    hit1 = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.up, visionDistance, trafficInt);
                    pathDodge = transform.position + new Vector3(0.5f, 0.0f);
                } else if (goalPosition.y < transform.position.y)
                {
                    hit1 = Physics2D.BoxCast(transform.position, col2D.bounds.size, 0f, Vector2.down, visionDistance, trafficInt);
                    pathDodge = transform.position + new Vector3(-0.5f, 0.0f);
                }
                // For going up and down we need to override the default left/right of the dontFall hit2
                hit2 = Physics2D.Raycast(transform.position, Vector2.up, 1.0f, climbableMask);
            }
            // If we've hit something then check to make sure it's on the climbable with us.
            if (hit1.collider != null)
            {
                // If our second raycast doesn't hit anything then we might be about to fall.
                if(hit2.collider == null)
                {
                    dontFall = true;
                }
                bool dodgeIt = false;
                // We want to check to make sure the collider of our ray is indeed on the climbable with us, whether it's the player or another NPC
                if (climbableTarget.TryGetComponent<Climbable>(out Climbable component))
                {
                    if (hit1.collider == component.currentPlayerCollider)
                    {
                        dodgeIt = true;
                    } else if (component.allowedNPCs.Contains(hit1.collider.transform))
                    {
                        dodgeIt = true;
                    }
                }
                // If we haven't already dodged the traffic and if it's on the climable with us, then start the coroutine that helps us move out of the way.
                // Also if our dontFall does not equal true, meaning we're not about to fall, then we can call the movement.
                if (!dodgedTraffic && dodgeIt && !dontFall)
                {
                    dodgedTraffic = true;
                    StartCoroutine(MoveOutofWay(pathDodge));
                }
            }
            // When we're no longer detecting traffic.
            if(hit1.collider == null)
            {
                if(dodgedTraffic)
                {
                    // We need to call this stop so that we're not stuck on a loop and get stuck in the position from the movement loop.
                    StopAllCoroutines();
                    dodgedTraffic = false;
                }
            }
        }
    }

    public IEnumerator MoveOutofWay(Vector2 destination)
    {
        // This is a quick coroutine found on the internet to help us move out of the way smoothly.
        // Called from the above CheckTraffic.
        float totalMovementTime = 5f;   // The amount of time we want the movement to take
        float currentMovementTime = 0f; // The amount of time that has passed
        while (Vector3.Distance(transform.position, destination) > 0)
        {
            currentMovementTime += Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, destination, currentMovementTime / totalMovementTime);
            yield return null;
        }
    }

    #endregion

    #region Movement
    void MoveAndRequest()
    {
        // Method to get us moving and potentially request climbing
        // Called when we're resetting due to distance from the target still being further than we'd like
        // and when we're restarting from a ClimbWait and ClimbRelease process.
        ClimbingOnTop();
        if (climbableTarget != null)
        {
            Debug.Log("921 climbableTarget: " + climbableTarget.name);
            // If we've found one, then MoveToXOnly to move to it if we're on the ground.
            if (isGrounded)
            {
                Debug.Log("ClimbableAI 940");
                if(withinClimbable != null)
                {
                    if (!allowedClimbableAccess.Contains(climbableTarget))
                    {
                        RequestClimb(climbableTarget);
                    } else
                    {
                        Climb();
                    }
                } else
                {
                    Debug.Log("ClimbableAI 952: ClimbableTarget: " + climbableTarget.name);
                    MoveToXOnly(climbableTarget.position);
                }
            }
            else if (onTopClimbable)
            {
                Debug.Log("ClimbableAI 957");
                // If we're on top of a climbable, we want to check to see if we're stuck up here
                // or if we've still got room to move around (such as if there's ground or the climbable extends around us)
                CheckIslandedClimbable();
                if (islanded)
                {
                    Debug.Log("ClimbingAI: 961: islanded, fromClimbableToClimbable gets set true.");
                    // If we're islanded, then we want to initiate other methods for getting to that next climbable.
                    // To do this we set the bool then requestclimb with that bool true so that we can eventually
                    // get to the MoveToNewClimbable method.
                    fromClimbableToClimbable = true;
                    RequestClimb(climbableTarget);
                }
                else
                {
                    MoveToXOnly(climbableTarget.position);
                }
            }
            else
            {
                CheckIslandedClimbable();
                if (islanded)
                {
                    Debug.Log("ClimbingAI: 978: islanded, fromClimbableToClimbable gets set true.");
                    // If we're islanded, then we want to initiate other methods for getting to that next climbable.
                    // To do this we set the bool then requestclimb with that bool true so that we can eventually
                    // get to the MoveToNewClimbable method.
                    fromClimbableToClimbable = true;
                    RequestClimb(climbableTarget);
                }
                else
                {
                    if(withinClimbable != null && climbableTarget != withinClimbable)
                    {
                        fromClimbableToClimbable = true;
                        RequestClimb(climbableTarget);
                    }
                    else {
                        MoveToXOnly(climbableTarget.position);
                    }
                }
            }
        }
    }

    void ChangeClimbingDirection()
    {
        // First determine if the top or bottom entry points are closer to us and set our climbEndPosition accordingly
        // Then tell the climbableTarget that we're climbing. Then set our tempSpeed and goalPosition and isClimbing bool and gravityScale.
        Vector2 climbEndPosition = transform.position;
        float posOffset = 0.3f; // A slight offset since typically the TopEntry and BottomEntry object positions aren't quite where we want to be.
        if (withinClimbable != null)
        {
            if (withinClimbable.childCount != 0)
            {
                Transform topEntry = null;
                Transform bottomEntry = null;

                for (int i = 0; i < withinClimbable.childCount; i++)
                {
                    if (withinClimbable.GetChild(i).name == "TopEntry")
                    {
                        topEntry = withinClimbable.GetChild(i);
                    }
                    else if (withinClimbable.GetChild(i).name == "BottomEntry")
                    {
                        bottomEntry = withinClimbable.GetChild(i);
                    }
                }

                if (topEntry != null && bottomEntry != null)
                {
                    Vector2 topEntryDirection = topEntry.position - target.position;
                    float topEntryDist = topEntryDirection.sqrMagnitude;
                    Vector2 botEntryDirection = bottomEntry.position - target.position;
                    float botEntryDist = botEntryDirection.sqrMagnitude;
                    if(topEntryDist < botEntryDist)
                    {
                        climbEndPosition = new Vector2(topEntry.position.x, topEntry.position.y - posOffset);
                    } else
                    {
                        climbEndPosition = new Vector2(bottomEntry.position.x, bottomEntry.position.y - posOffset);
                    }
                }
                


            }
            isClimbing = true;
            tempSpeed = climbSpeed;
            goalPosition = climbEndPosition;
        }
        else
        {
            SeekClimbable(target);
        }
    }

    void MoveToXOnly(Vector2 targetPosition)
    {
        // A method to just move us to the climbable, used only via MouseClick currently, mostly a standin?
        // TODO: Do you need this after testing?
        Vector2 xOnlyPosition = new Vector2(targetPosition.x, transform.position.y);
        if (isGrounded)
        {
            tempSpeed = speed;
        } else
        {
            tempSpeed = climbSpeed;
        }
        if (pauseClimbing)
        {
            pauseClimbing = false;
        }
        goalPosition = xOnlyPosition;
        Debug.Log("ClimbingAI: MoveToXOnly: goalPosition: " + goalPosition);
    }

    void MoveToNewClimbable(Transform newClimbable)
    {
        // If we're on top of a climbable and we're going to move to another one, we need to initiate
        // a JumpAI component to do so, || newClimbable.position.y < transform.position.y
        if (newClimbable.position.y > transform.position.y || newClimbable.position.y < transform.position.y)
        {
            if (transform.TryGetComponent<JumpAI>(out JumpAI component))
            {
                Debug.Log("ClimbingAI: MoveToNewClimbable: " + newClimbable.name);
                component.JumpFromClimbableToClimbableCheck(newClimbable);
            }
        } else
        {
            MoveToXOnly(newClimbable.position);
        }
    }

    void MoveToEndOfWithinClimbable()
    {
        Debug.Log("ClimbingAI: MoveToEndOfWithinClimbable");
        // Method used if we're within a climbable and need to move to the other end of it.
        // First check to make sure the climbable we're within allows us to access it
        if (allowedClimbableAccess.Contains(withinClimbable))
        {
            Debug.Log("ClimbingAI: MoveToEndOfWithinClimbable: withinClimbable: " + withinClimbable.name);
            // Set this book to true so that we don't call a ClimbLimitReached within CheckDistanceFromTarget
            movingToWithinPoint = true;
            Transform topEntry = null;
            Transform botEntry = null;
            // Get the top and bottom Entries.
            for (int x = 0; x < withinClimbable.childCount; x++)
            {
                if (withinClimbable.GetChild(x).name == "TopEntry")
                {
                    Debug.Log("ClimbingAI: MTEOWC: topEntry");
                    topEntry = withinClimbable.GetChild(x);
                }
                else if (withinClimbable.GetChild(x).name == "BottomEntry")
                {
                    Debug.Log("ClimbingAI: MTEOWC: botEntry");
                    botEntry = withinClimbable.GetChild(x);
                }
            }
            // If they're not null, then get their positions and reset the goalPosition to their positions.
            if (topEntry != null && botEntry != null)
            {
                // Reset our movement variables for climbing.
                tempSpeed = climbSpeed;
                rb.gravityScale = 0f;
                isClimbing = true;
                pauseClimbing = false;
                reachedEndPosition = false;
                float posOffset = 0.3f; // A slight offset since typically the TopEntry and BottomEntry object positions aren't quite where we want to be.
                Vector2 topEntryDir = topEntry.position - target.position;
                float topEntryDist = topEntryDir.sqrMagnitude;
                Vector2 botEntryDir = botEntry.position - target.position;
                float botEntryDist = botEntryDir.sqrMagnitude;
                if (topEntryDist < botEntryDist)
                {
                    if(withinClimbable.localScale.x < withinClimbable.localScale.y)
                    {
                        goalPosition = new Vector2(topEntry.position.x, topEntry.position.y - posOffset);
                    } else
                    {
                        goalPosition = topEntry.position;
                    }
                    
                }
                else
                {
                    if (withinClimbable.localScale.x < withinClimbable.localScale.y)
                    {
                        goalPosition = new Vector2(botEntry.position.x, botEntry.position.y - posOffset);
                    } else {
                        goalPosition = botEntry.position;
                    }
                }
                Debug.Log("ClimbingAI: MTEOWC: goalPosition: " + goalPosition);
            } else
            {
                // Otherwise if we don't have a bottom or top entry point, then we're not movingtowithinpoint
                // You could do more calculation here to try to determine another place to move to?
                Debug.Log("ClimbingAI: MTEOWC: 1239");
                movingToWithinPoint = false;
            }
        } else
        {
            Debug.Log("ClimbingAI: MoveToEndOfWithinClimbable: withinClimbable not in allowedClimbableAccess calling MoveAndRequest.");
            // Otherwise if we're not allowed entry then we want to reset the climbableTarget to this climbable and move and request.
            climbableTarget = withinClimbable;
            MoveAndRequest();
        }
    }

    #endregion

    #region Jumping

    public void JumpCheckFailed()
    {
        // If we wanted to jump, but the JumpAI script was unable to find a suitable
        // way of jumping, then we've reached our limits and have failed.
        Debug.Log("ClimbingAI: 1151: JumpCheckFailed calling ClimbingReachedLimit");
        ClimbingReachedLimit();
    }

    public void JumpAttempted()
    {
        jumpAttempted = true;
        Debug.Log("ClimbingAI: JumpAttempted entered.");
        if (!onTopClimbable)
        {
            rb.gravityScale = defaultGravity;
        }
        // If we attempted a jump, but are now grounded, then the jump failed.
        if (isGrounded)
        {
            SeekClimbable(target);
        }
    }
    #endregion

    #region ClimbableDetection

    public void DetermineIfBetween(Transform climbablePos)
    {
        // Currently unused, but this might be valuable in the future if there are other issues that arise
        // or if you are looking to make the code more reusable or efficient perhaps?
        if(target.position.x > transform.position.x)
        {
            if(climbablePos.position.x > transform.position.x && climbablePos.position.x < target.position.x)
            {
                // x is between
            }
        } else
        {
            if(climbablePos.position.x < transform.position.x && climbablePos.position.x > target.position.x)
            {
                // x is between
            }
        }

        if(target.position.y > transform.position.y)
        {
            if(climbablePos.position.y > transform.position.y && climbablePos.position.y < target.position.y)
            {
                // y is between
            }
        } else
        {
            if(climbablePos.position.y < transform.position.y && climbablePos.position.y > target.position.y)
            {
                // y is between
            }
        }
    }

    public Transform FindClimbableUp(Transform currentTarget)
    {
        // If we haven't yet found a way up.
        // Cast rays left and right looking for a climable we can take.
        RaycastHit2D rayRightHit = Physics2D.Raycast(transform.position, Vector2.right, Mathf.Infinity, climbableMask);
        RaycastHit2D rayLeftHit = Physics2D.Raycast(transform.position, Vector2.left, Mathf.Infinity, climbableMask);
        Collider2D collider = null;
        if (rayRightHit.collider != null && rayLeftHit.collider != null)
        {
            Debug.Log("ClimbingAI: FindClimbableUp: rayRightHit.collider: " + rayRightHit.collider.name + " rayleftHit.collider: " + rayLeftHit.collider.name);
            // If both right and left are not null.
            // Check the distance between right and left, and if left is shorter then update the target direction.
            Vector2 directionToRight = rayRightHit.collider.transform.position - transform.position;
            float dSqrToRight = directionToRight.sqrMagnitude;
            Vector2 directionToLeft = rayLeftHit.collider.transform.position - transform.position;
            float dSqrToLeft = directionToLeft.sqrMagnitude;
            if (dSqrToLeft < dSqrToRight)
            {
                collider = rayLeftHit.collider;
            } else
            {
                collider = rayRightHit.collider;
            }
        }
        else if (rayLeftHit.collider != null && rayRightHit.collider == null)
        {
            // If left is not null, meaning there's one left but not right.
            collider = rayLeftHit.collider;
        }
        else if (rayRightHit.collider != null && rayLeftHit.collider == null)
        {
            collider = rayRightHit.collider;
        }

        if (collider != null)
        {
            Debug.Log("ClimbingAI: FindClimbableUp: collider.name: " + collider.name);
            // If we found a climable, and it's not one we're currently on top of or climbing.
            if (collider.transform != supportingClimbable && collider.transform != withinClimbable)
            {
                // Then change our climable goal to the new target, and let us know we found a way up.
                if (currentTarget.position.y > transform.position.y && currentTarget.position.y > collider.transform.position.y && collider.transform.position.y > transform.position.y)
                {
                    // If our collider is between us and our currentTarget, meaning it's our ideal climbable, then return it.
                    Debug.Log("ClimbingAI: 1326 FindClimbableuP: collider.Transform: " + collider.transform.name);
                    return collider.transform;
                }
                else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < collider.transform.position.y && collider.transform.position.y < transform.position.y)
                {
                    Debug.Log("ClimbingAI: 1331 FindClimbableuP: collider.Transform: " + collider.transform.name);
                    return collider.transform;
                }
                else
                {
                    Debug.Log("Climbing AI: FindClimbableUp: Collider not between transform and currentTarget.");
                    return null;
                }

            }
            else if (collider.transform == withinClimbable)
            {
                Debug.Log("ClimbingAI: 1343 FindClimbableUP: collider.transform == withinClimbable");
                collider = null;
            } else if (collider.transform == supportingClimbable)
            {
                Debug.Log("ClimbingAI: 1347 FindClimbableUp: collider.transform == supportingClimbable");
            }
        }

        if(collider == null)
        {
            Debug.Log("ClimbingAI: FindClimbableUp: collider == null!");
            //We didn't find a climable to the left or right, so look up and down.
            RaycastHit2D hitDown = Physics2D.BoxCast(transform.position + Vector3.down, col2D.bounds.size, 0f, Vector2.down, 0.08f, climbableMask);
            RaycastHit2D hitUp = Physics2D.BoxCast(transform.position + Vector3.up, col2D.bounds.size, 0f, Vector2.up, 0.08f, climbableMask);
            if (hitDown.collider == null && hitUp.collider != null )
            {
                if(hitUp.collider.transform != withinClimbable)
                {
                    // If our climable is above us
                    return hitUp.collider.transform;
                }
            }
            else if (hitDown.collider != null && hitUp.collider == null)
            {
                if (hitDown.collider.transform != withinClimbable && hitDown.collider.transform != supportingClimbable)
                {
                    return hitDown.collider.transform;
                }
            }
            else if (hitDown.collider != null && hitUp.collider != null )
            {
                // If neither is null, then compare the distances and return which is closest.
                Vector2 directionToDown = hitDown.collider.transform.position - transform.position;
                float dSqrToDown = directionToDown.sqrMagnitude;
                Vector2 directionToUp = hitUp.collider.transform.position - transform.position;
                float dSqrToUp = directionToUp.sqrMagnitude;
                if (dSqrToDown < dSqrToUp)
                {
                    if (hitDown.collider.transform != withinClimbable && hitDown.collider.transform != supportingClimbable)
                    {

                        return hitDown.collider.transform;
                    }
                }
                else
                {
                    if (hitDown.collider.transform != withinClimbable)
                    {

                        return hitUp.collider.transform;
                    }
                }
            }
            // If the right, left, up, and down climables aren't found, then we need to check in more directions, so, we cast a circle around us.
            // Such as if we need to jump up to reach a climable.
            // Cast a circle with an array for the hit so we can get all climables around us.
            RaycastHit2D[] hit = Physics2D.CircleCastAll(transform.position, 4.0f, Vector2.zero, 1f, climbableMask);
            Debug.Log("ClimbingAI: FindClimbableUP: hitDown and hitUp null so trying CircleCastAll");
            if (hit.Length != 0)
            {
                Debug.Log("ClimbingAI: FindClimbableUP: hit.length != 0");
                //Then we'll want to first determine which is the closest on our own since CircleCast can be wonky with figuring that out.
                float shortestDist2Climbable = Mathf.Infinity; // Our float to keep track of current shortest distances.
                int closestInd = 0; // Our integer to log which is the closest indexed climables in the above array.
                                    // Cycle through the array index based on its length.
                for (int i = 0; i < hit.Length; i++)
                {
                    // Check the distance between our transform and the transform of each indexed climable.
                    //float distanceToClimbable = Vector2.Distance(transform.position, hit[i].transform.position); not as efficient as new method
                    Vector2 directionToClimbable = hit[i].collider.transform.position - transform.position;
                    float dSqrToClimbable = directionToClimbable.sqrMagnitude;
                    if(hit[i].collider != null && supportingClimbable != null && withinClimbable != null)
                    {
                        Debug.Log("hit[i].collider: " + hit[i].collider.name + " supportingClimbable: " + supportingClimbable.name + " withinClimbable: " + withinClimbable.name);

                    }
                    // Rule out the climbable we're on top of and the one we're within.
                    if(hit[i].collider.transform != supportingClimbable && hit[i].collider.transform != withinClimbable)
                    {
                        Debug.Log("ClimbingAI: FindClimbableUP: 1415");
                        if (dSqrToClimbable < shortestDist2Climbable) // Check the distance against the current shortest distance.
                        {
                            Debug.Log("ClimbingAI: FindClimbableUP: 1418");
                            // We use this bool value in case our Hit2 conditions aren't reached and we need to just use our hit result instead.
                            bool foundInHit2 = false;
                            shortestDist2Climbable = dSqrToClimbable; // Convert if new distance smaller than previous shortdistance.
                            // We then want to cast a ray from us to the target to make sure the closest we have is also between us and the target.
                            float distance2Target = Vector2.Distance(transform.position, target.position);
                            RaycastHit2D[] hit2 = Physics2D.RaycastAll(transform.position, target.position - transform.position, distance2Target, climbableMask);
                            // Get the parent so we're not comparing parts of the climbable (like entry points) instead of the main climbable we want.
                            if(hit2 != null)
                            {
                                Debug.Log("ClimbingAI: FindClimbableUP: 1428");
                                for (int x = 0; x < hit2.Length; x++)
                                {
                                    Transform parentTrans;
                                    if (hit2[x].collider.transform.parent != null)
                                    {
                                        parentTrans = hit2[x].collider.transform.parent;
                                    }
                                    else
                                    {
                                        parentTrans = hit2[x].collider.transform;
                                    }
                                    // Make sure we're not selecting the climbable we're currently in.
                                    if (parentTrans != supportingClimbable && parentTrans != withinClimbable)
                                    {
                                        // If the parentTrans is also our initial hit[i].collider.transform, basically completing the connection between our two rays
                                        if (parentTrans == hit[i].collider.transform)
                                        {
                                            foundInHit2 = true;
                                            closestInd = i; // Log which index we are so that we can use the shortest distanced index below.
                                        }
                                    }
                                }
                            }
                            // If the hit2 didn't produce anything, just use our initial closestInd findings.
                            if(!foundInHit2)
                            {
                                Debug.Log("ClimbingAI: FindClimbableUP: 1455");
                                closestInd = i;
                            }
                            
                        }
                    }
                }
                // Get the climable transform from our hit array with our closest index.
                Transform climbableTran = hit[closestInd].collider.transform;
                if (climbableTran != null && climbableTran != withinClimbable)
                {
                    Debug.Log("ClimbingAI: FindClimbableUP: climbableTran: " + climbableTran);
                    // return the ideal climbable
                    if (currentTarget.position.y > transform.position.y && currentTarget.position.y > climbableTran.position.y && climbableTran.position.y > transform.position.y)
                    {

                        return climbableTran;
                    }
                    else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < climbableTran.position.y && climbableTran.position.y < transform.position.y)
                    {

                        return climbableTran;
                    } else
                    {
                        // We might find a climbableTran that isn't between us and the current target, but is currently being climbed by the currentTarget.
                        // If so then we need to check and then return that climbableTran as our target.
                        if(currentTarget.position.y < transform.position.y && climbableTran.position.y < transform.position.y ||
                            currentTarget.position.y > transform.position.y && climbableTran.position.y > transform.position.y)
                        {
                            if (climbableTran.TryGetComponent<Climbable>(out Climbable component))
                            {
                                if (component.isBeingClimbed)
                                {
                                    return climbableTran;
                                }
                            }
                        }
                    }
                }
            }
            // If the above all fails, then we may be within a climbable and need to find another outside us. We repeat similar steps to above,
            // but with some slight alterations.
            RaycastHit2D[] hitRayAll = Physics2D.RaycastAll(transform.position, target.position - transform.position, maxClimbableDetDist, climbableMask);
            Debug.Log("ClimbingAI: FinDClimbableUP: 1498");
            if (hitRayAll.Length != 0)
            {
                Debug.Log("ClimbingAI: FindClimbableUP: 1501");
                //Then we'll want to first determine which is the closest on our own since RaycastAll can be wonky with figuring that out.
                float shortestDist2Climbable = Mathf.Infinity; // Our float to keep track of current shortest distances.
                int closestInd = 0; // Our integer to log which is the closest indexed climables in the above array.
                                    // Cycle through the array index based on its length.
                for (int i = 0; i < hitRayAll.Length; i++)
                {
                    // Check the distance between our transform and the transform of each indexed climable.
                    Vector2 directionToClimbable = hitRayAll[i].collider.transform.position - transform.position;
                    float dSqrToClimbable = directionToClimbable.sqrMagnitude;
                    // Rule out the climbable we're on top of and the one we're within.
                    if (dSqrToClimbable < shortestDist2Climbable) // Check the distance against the current shortest distance.
                    {
                        shortestDist2Climbable = dSqrToClimbable; // Convert if new distance smaller than previous shortdistance.
                        closestInd = i; // Log which index we are so that we can use the shortest distanced index below.
                    }
                }
                // Get the climable transform from our hit array with our closest index.
                Transform climbableTran = hitRayAll[closestInd].collider.transform;
                if (climbableTran != null && climbableTran != withinClimbable)
                {
                    // return the ideal climbable
                    if (currentTarget.position.y > transform.position.y && currentTarget.position.y > climbableTran.position.y && climbableTran.position.y > transform.position.y)
                    {
                        return climbableTran;
                    }
                    else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < climbableTran.position.y && climbableTran.position.y < transform.position.y)
                    {
                        return climbableTran;
                    }
                    else
                    {
                        // We might find a climbableTran that isn't between us and the current target, but is currently being climbed by the currentTarget.
                        // If so then we need to check and then return that climbableTran as our target.
                        if (currentTarget.position.y < transform.position.y && climbableTran.position.y < transform.position.y ||
                            currentTarget.position.y > transform.position.y && climbableTran.position.y > transform.position.y)
                        {
                            if (climbableTran.TryGetComponent<Climbable>(out Climbable component))
                            {
                                if (component.isBeingClimbed)
                                {
                                    return climbableTran;
                                }
                            }
                        }
                    }
                } else if(climbableTran != null && climbableTran == withinClimbable)
                {
                    // If we're within the climbable, then call MoveToEndOfWithinClimbable so we can go to the other end.
                    MoveToEndOfWithinClimbable();
                    return null;
                }
            }

        } else
        {
            Debug.Log("ClimbingAI: FindClimbableUP: collider.transform: " + collider.transform.name);
            return collider.transform;
        }

        return null;
    }

    public Transform FindClimbableDown2(Transform currentTarget)
    {
        if(withinClimbable == null)
        {
            Debug.Log("ClimbingAI: FindClimbableDown2: For: " + currentTarget);
            // Cast rays left and right looking for a climable we can take.
            Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y + -GetComponent<Collider2D>().bounds.extents.y + -1.0f);
            Vector2 boxCastSize = new Vector2(2.0f, 2.0f);
            RaycastHit2D rayRightHit = Physics2D.BoxCast(rayOrigin, boxCastSize, 0.0f, Vector2.right, Mathf.Infinity, climbableMask);
            RaycastHit2D rayLeftHit = Physics2D.BoxCast(rayOrigin, boxCastSize, 0.0f, Vector2.left, Mathf.Infinity, climbableMask);
            Collider2D collider = null;
            if (rayRightHit.collider != null && rayLeftHit.collider != null)
            {
                // Determine which one is between us and the currentTarget and choose whichever is.
                if (currentTarget.position.y < transform.position.y && currentTarget.position.y < rayRightHit.transform.position.y && rayRightHit.transform.position.y < transform.position.y)
                {
                    collider = rayRightHit.collider;
                }
                else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < rayLeftHit.transform.position.y && rayLeftHit.transform.position.y < transform.position.y)
                {
                    collider = rayLeftHit.collider;
                }
                else
                {
                    // If both right and left are not null.
                    // Check the distance between right and left, and if left is shorter then update the target direction.
                    Vector2 directionToRight = rayRightHit.collider.transform.position - transform.position;
                    float dSqrToRight = directionToRight.sqrMagnitude;
                    Vector2 directionToLeft = rayLeftHit.collider.transform.position - transform.position;
                    float dSqrToLeft = directionToLeft.sqrMagnitude;
                    if (dSqrToLeft < dSqrToRight)
                    {
                        collider = rayLeftHit.collider;
                    }
                    else
                    {
                        collider = rayRightHit.collider;
                    }
                }

            }
            else if (rayLeftHit.collider != null && rayRightHit.collider == null)
            {
                // If left is not null, meaning there's one left but not right.
                collider = rayLeftHit.collider;
            }
            else if (rayRightHit.collider != null && rayLeftHit.collider == null)
            {
                collider = rayRightHit.collider;
            }

            if (collider != null)
            {
                // If we found a climable, and it's not one we're currently on top of or climbing.
                if (collider.transform != supportingClimbable && collider.transform != withinClimbable)
                {
                    // Then change our climable goal to the new target, and let us know we found a way down.
                    if (currentTarget.position.y < transform.position.y && currentTarget.position.y < collider.transform.position.y && collider.transform.position.y < transform.position.y)
                    {
                        // If our collider is between us and our currentTarget, meaning it's our ideal climbable, then return it.
                        Debug.Log("ClimbingAI: FindClimbabledown2: 1522 collider.transform: " + collider.transform.name);
                        return collider.transform;
                    }
                    else
                    {
                        Debug.Log("Climbing AI: FindClimbableDown2: Collider not between transform and currentTarget.");
                        collider = null;
                    }

                }
                else if (collider.transform == withinClimbable)
                {
                    collider = null;
                }
            }

            if (collider == null)
            {
                //We didn't find a climable to the left or right, so look up and down.
                RaycastHit2D hitDown = Physics2D.BoxCast(transform.position + Vector3.down, col2D.bounds.size, 0f, Vector2.down, 0.08f, climbableMask);
                if (hitDown.collider != null)
                {
                    if (hitDown.collider.transform != withinClimbable && hitDown.collider.transform != supportingClimbable)
                    {
                        Debug.Log("ClimbingAI: FindClimbabledown2: 1546 hitDown.collider.transform: " + hitDown.collider.transform.name);
                        return hitDown.collider.transform;
                    }
                }
                // If the right, left, up, and down climables aren't found, then we need to check in more directions, so, we cast a circle around us.
                // Such as if we need to jump up to reach a climable.
                // Cast a circle with an array for the hit so we can get all climables around us.
                RaycastHit2D[] hit = Physics2D.CircleCastAll(transform.position, 4.0f, Vector2.zero, 1f, climbableMask);

                if (hit.Length != 0)
                {
                    //Then we'll want to first determine which is the closest on our own since CircleCast can be wonky with figuring that out.
                    float shortestDist2Climbable = Mathf.Infinity; // Our float to keep track of current shortest distances.
                    int closestInd = 0; // Our integer to log which is the closest indexed climables in the above array.
                                        // Cycle through the array index based on its length.
                    for (int i = 0; i < hit.Length; i++)
                    {
                        // Check the distance between our transform and the transform of each indexed climable.
                        //float distanceToClimbable = Vector2.Distance(transform.position, hit[i].transform.position); not as efficient as new method
                        Vector2 directionToClimbable = hit[i].collider.transform.position - transform.position;
                        float dSqrToClimbable = directionToClimbable.sqrMagnitude;
                        // Rule out the climbable we're on top of and the one we're within.
                        if (hit[i].collider.transform != supportingClimbable && hit[i].collider.transform != withinClimbable)
                        {
                            if (dSqrToClimbable < shortestDist2Climbable) // Check the distance against the current shortest distance.
                            {
                                // We use this bool value in case our Hit2 conditions aren't reached and we need to just use our hit result instead.
                                bool foundInHit2 = false;
                                shortestDist2Climbable = dSqrToClimbable; // Convert if new distance smaller than previous shortdistance.
                                                                          // We then want to cast a ray from us to the target to make sure the closest we have is also between us and the target.
                                float distance2Target = Vector2.Distance(transform.position, target.position);
                                RaycastHit2D[] hit2 = Physics2D.RaycastAll(transform.position, target.position - transform.position, distance2Target, climbableMask);
                                // Get the parent so we're not comparing parts of the climbable (like entry points) instead of the main climbable we want.
                                if (hit2 != null)
                                {
                                    for (int x = 0; x < hit2.Length; x++)
                                    {
                                        Transform parentTrans;
                                        if (hit2[x].collider.transform.parent != null)
                                        {
                                            parentTrans = hit2[x].collider.transform.parent;
                                        }
                                        else
                                        {
                                            parentTrans = hit2[x].collider.transform;
                                        }
                                        // Make sure we're not selecting the climbable we're currently in.
                                        if (parentTrans != supportingClimbable && parentTrans != withinClimbable)
                                        {
                                            // If the parentTrans is also our initial hit[i].collider.transform, basically completing the connection between our two rays
                                            if (parentTrans == hit[i].collider.transform)
                                            {
                                                foundInHit2 = true;
                                                closestInd = i; // Log which index we are so that we can use the shortest distanced index below.
                                            }
                                        }
                                    }
                                }
                                // If the hit2 didn't produce anything, just use our initial closestInd findings.
                                if (!foundInHit2)
                                {
                                    closestInd = i;
                                }

                            }
                        }
                    }
                    // Get the climable transform from our hit array with our closest index.
                    Transform climbableTran = hit[closestInd].collider.transform;
                    if (climbableTran != null && climbableTran != withinClimbable)
                    {
                        // return the ideal climbable
                        if (currentTarget.position.y > transform.position.y && currentTarget.position.y > climbableTran.position.y && climbableTran.position.y > transform.position.y)
                        {
                            Debug.Log("ClimbingAI: FindClimbabledown2: 1620 climbableTran: " + climbableTran.name);
                            return climbableTran;
                        }
                        else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < climbableTran.position.y && climbableTran.position.y < transform.position.y)
                        {
                            Debug.Log("ClimbingAI: FindClimbabledown2: 1625 climbableTran: " + climbableTran.name);
                            return climbableTran;
                        }
                        else
                        {
                            // We might find a climbableTran that isn't between us and the current target, but is currently being climbed by the currentTarget.
                            // If so then we need to check and then return that climbableTran as our target.
                            if (currentTarget.position.y < transform.position.y && climbableTran.position.y < transform.position.y ||
                                currentTarget.position.y > transform.position.y && climbableTran.position.y > transform.position.y)
                            {
                                if (climbableTran.TryGetComponent<Climbable>(out Climbable component))
                                {
                                    if (component.isBeingClimbed)
                                    {
                                        Debug.Log("ClimbingAI: FindClimbabledown2: 1639 climbableTran: " + climbableTran.name);
                                        return climbableTran;
                                    }
                                }
                            }
                        }
                    }
                }
                // If the above all fails, then we may be within a climbable and need to find another outside us. We repeat similar steps to above,
                // but with some slight alterations.
                RaycastHit2D[] hitRayAll = Physics2D.RaycastAll(transform.position, target.position - transform.position, maxClimbableDetDist, climbableMask);

                if (hitRayAll.Length != 0)
                {
                    //Then we'll want to first determine which is the closest on our own since RaycastAll can be wonky with figuring that out.
                    float shortestDist2Climbable = Mathf.Infinity; // Our float to keep track of current shortest distances.
                    int closestInd = 0; // Our integer to log which is the closest indexed climables in the above array.
                                        // Cycle through the array index based on its length.
                    for (int i = 0; i < hitRayAll.Length; i++)
                    {
                        // Check the distance between our transform and the transform of each indexed climable.
                        Vector2 directionToClimbable = hitRayAll[i].collider.transform.position - transform.position;
                        float dSqrToClimbable = directionToClimbable.sqrMagnitude;
                        // Rule out the climbable we're on top of and the one we're within.
                        if (dSqrToClimbable < shortestDist2Climbable) // Check the distance against the current shortest distance.
                        {
                            shortestDist2Climbable = dSqrToClimbable; // Convert if new distance smaller than previous shortdistance.
                            closestInd = i; // Log which index we are so that we can use the shortest distanced index below.
                        }
                    }
                    // Get the climable transform from our hit array with our closest index.
                    Transform climbableTran = hitRayAll[closestInd].collider.transform;
                    if (climbableTran != null && climbableTran != withinClimbable)
                    {
                        // return the ideal climbable
                        if (currentTarget.position.y > transform.position.y && currentTarget.position.y > climbableTran.position.y && climbableTran.position.y > transform.position.y)
                        {
                            Debug.Log("ClimbingAI: FindClimbabledown2: 1676 climbableTran: " + climbableTran.name);
                            return climbableTran;
                        }
                        else if (currentTarget.position.y < transform.position.y && currentTarget.position.y < climbableTran.position.y && climbableTran.position.y < transform.position.y)
                        {
                            Debug.Log("ClimbingAI: FindClimbabledown2: 1681 climbableTran: " + climbableTran.name);
                            return climbableTran;
                        }
                        else
                        {
                            // We might find a climbableTran that isn't between us and the current target, but is currently being climbed by the currentTarget.
                            // If so then we need to check and then return that climbableTran as our target.
                            if (currentTarget.position.y < transform.position.y && climbableTran.position.y < transform.position.y ||
                                currentTarget.position.y > transform.position.y && climbableTran.position.y > transform.position.y)
                            {
                                if (climbableTran.TryGetComponent<Climbable>(out Climbable component))
                                {
                                    if (component.isBeingClimbed)
                                    {
                                        Debug.Log("ClimbingAI: FindClimbabledown2: 1695 climbableTran: " + climbableTran.name);
                                        return climbableTran;
                                    }
                                }
                            }
                        }
                    }
                    else if (climbableTran != null && climbableTran == withinClimbable)
                    {
                        // If we're within the climbable, then call MoveToEndOfWithinClimbable so we can go to the other end.
                        MoveToEndOfWithinClimbable();
                        return null;
                    }
                }

            }
        } else
        {
            Debug.Log("ClimbingAI: FindClimbableDown2 calling FindClimbableDown");
            return FindClimbableDown(currentTarget);
        }
        
        return null;
    }

    public Transform FindClimbableDown(Transform currentTarget)
    {
        float distance = Vector2.Distance(transform.position, currentTarget.position);
        RaycastHit2D[] hit = Physics2D.CircleCastAll(transform.position, 8.0f, currentTarget.position, distance, climbableMask);
        if (hit.Length != 0)
        {
            //Then we'll want to first determine which is the closest on our own since CircleCast can be wonky with figuring that out.
            // We want to find the climbable that's closest to our transform and closest to the target's transform by factoring together the average
            // distances between us and the climbable and the climbable to the target, going for the one that's closest to both.
            List<float> averageDist = new List<float>(); // Our list to temporarily store the averages of distances.
            List<int> indexes = new List<int>(); // our list to temporarily track the indexes below.
            int closestInd = 0; // Our integer to log which is the closest indexed climables in the above array.
                                // Cycle through the array index based on its length.
            float clo = Mathf.Infinity;
            int cloInd = 0;
            for (int i = 0; i < hit.Length; i++)
            {
                // We want to ignore the climbable we're within, as this was giving us trouble when detecting the next climbable on our way down.
                bool ignoreThisOne = false;

                if(!onTopClimbable && withinClimbable != null)
                {
                    if(hit[i].transform == withinClimbable)
                    {
                        Debug.Log("ClimbingAI: FindClimbableDown: Ignoring: " + hit[i].transform);
                        ignoreThisOne = true;
                    }
                }
                if (!ignoreThisOne)
                {
                    // Check the distance between our follower transform and the transform of each indexed climable.
                    float distanceToClimbable = Vector2.Distance(transform.position, hit[i].transform.position);
                    if(distanceToClimbable < clo)
                    {
                        clo = distanceToClimbable;
                        cloInd = i;
                    }
                    float distanceToTarget = Vector2.Distance(currentTarget.position, hit[i].transform.position);
                    float average = (distanceToClimbable + distanceToTarget) / 2; // We do the average by adding the sum of the distances then dividing by their count
                    averageDist.Add(average); // Add the average to our list.
                    indexes.Add(i); // Add the current index to our list of indexes
                    float closest = averageDist.Min(); // Find the shortest distance by using the Min method from Linq
                                                       // If the value of the list item is the closest distance then make its index the closestInd so we can use it below.
                    // We do this because otherwise, if we skip the climbable we're within, then our indexes won't match
                    // so, we need to keep track of the actual indexes so we can refer to the correct hit[closestInd] below with the proper index from the Raycast above.
                    closestInd = indexes[averageDist.IndexOf(closest)];
                }
            }
            Debug.Log("ClimbingAI: FindClimbableDown: CircleCastAll index 0: " + hit[0].collider.name + " cloInd: " + hit[cloInd].collider.name + " closestInd: " + hit[closestInd].collider.name);
            if (closestInd < hit.Length)
            {
                Debug.Log("ClimbingAI: closestInd: " + closestInd);
                // Get the climable transform from our hit array with our closest index.
                Transform climbableTran = hit[closestInd].transform;
                Debug.Log("ClimbingAI: ClimbableTran: " + climbableTran.name);
                if (climbableTran != null)
                {
                    // Set our temporary target
                    if (currentTarget.position.y < transform.position.y && currentTarget.position.y <= climbableTran.position.y && climbableTran.position.y < transform.position.y)
                    {
                        Debug.Log("ClimbingAI: Climbablebetween us and Target");
                        return climbableTran;
                    } else
                    {
                        Debug.Log("ClimbingAI: secondary option check to see if the climbable is being climbed by our current target.");
                        // We might find a climbableTran that isn't between us and the current target, but is currently being climbed by the currentTarget.
                        // If so then we need to check and then return that climbableTran as our target.
                        if (currentTarget.position.y < transform.position.y && climbableTran.position.y < transform.position.y ||
                            currentTarget.position.y > transform.position.y && climbableTran.position.y > transform.position.y)
                        {
                            if (climbableTran.TryGetComponent<Climbable>(out Climbable component))
                            {
                                Debug.Log("ClimbingAI: asking climbableTran if it's being climbed");
                                if (component.isBeingClimbed)
                                {
                                    Debug.Log("ClimbingAI: ClimbableTran is being climbed.");
                                    return climbableTran;
                                } else
                                {
                                    Debug.Log("ClimbingAI: ClimbableTran is not being climbed.");
                                }
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    #endregion

    #region ClimbableCommunication

    public void RequestClimb(Transform climbTarget)
    {
        // Method to request that we be able to climb the Climbable.
        if (climbTarget.gameObject.layer == climbableMaskInt)
        {
            if(climbTarget.TryGetComponent<Climbable>(out Climbable component)){
                Debug.Log("ClimbingAI: RequestClimb: " + climbTarget.name);
                component.NPCRequestsClimb(transform);
            }
        }
    }

    public void AllowedClimb(Transform targetClimbable)
    {
        // Method that's activated by a Climbable allowing us to climb it.
        if (targetClimbable == climbableTarget)
        {
            Debug.Log("ClimbingAI: AllowedClimb: " + targetClimbable.name);
            reachedEndPosition = false;
            if (!allowedClimbableAccess.Contains(targetClimbable))
            {
                allowedClimbableAccess.Add(targetClimbable);
            }
            // If we're allowed to climb and we're moving from one climbable to another
            // We want to call that specific method, otherwise, run to the Enterclimbable
            if (fromClimbableToClimbable)
            {
                if (jumpFromClimbable2Climbable)
                {
                    MoveToNewClimbable(targetClimbable);
                }
            } else
            {
                if (currentClimbableArea != null)
                {
                    if (currentClimbableArea == targetClimbable)
                    {
                        EnterClimbable();
                    } else
                    {
                        if(withinClimbable != null)
                        {
                            if(withinClimbable != targetClimbable)
                            {
                                MoveAndRequest();
                            } else
                            {
                                Climb();
                            }
                        }
                    }
                }
            }
        }
    }

    public void DeniedClimb(Transform targetClimbable)
    {
        // Method activated by Climbable for when we're denied access to climb it.
        if(targetClimbable == climbableTarget)
        {
            allowedClimbableAccess.Remove(targetClimbable);
            // If we've been denied, we also want to stop our current progress towards climbing completely,
            // so call ClimbingReachedLimit as that's currently our most final method to call.
            Debug.Log("ClimbingAI: 1867: DeniedClimb calling ClimbingReachedLimit");
            ClimbingReachedLimit();
            if(TryGetComponent<PathingAI>(out PathingAI component))
            {
                component.DeniedClimb();
            }
        }
    }

    public void ClimbWait(Transform occupiedClimable)
    {
        // Used when player is on a climable and if we need to climb that same climable,
        // wait for player to pass.
        if (climbableTarget == occupiedClimable)
        {
            // If our waiting bool is selected, then we pause before moving into the climbable
            if (waitForOthersClimbing)
            {
                pauseClimbing = true;
                rb.velocity = Vector2.zero;
                rb.gravityScale = 0;
            }
            else
            {
                // Otherwise, if the bool is false, then we just move on to the other method.
                if (!allowedClimbableAccess.Contains(climbableTarget))
                {
                    MoveAndRequest();
                }
            }
        }
    }

    public void ClimbRelease(Transform vacantClimable)
    {
        // This releases us from the above ClimbWait's function.
        // Check to see if we're obeying the wait option or not, then determine next steps.
        if (waitForOthersClimbing)
        {
            if (climbableTarget == vacantClimable)
            {
                pauseClimbing = false;
                if (!allowedClimbableAccess.Contains(climbableTarget))
                {
                    MoveAndRequest();
                }
            }
            else if (climbableTarget == null)
            {
                CheckDistanceFromTarget();
            }
        }
    }

    #endregion

    #region PathingAI Communication

    public void StartedFollowingTarget(Transform newTarget)
    {
        if (!withinClimbable)
        {
            ClimbingReachedLimit();
        } else
        {
            target = newTarget;
            EndClimb();
        }
    }

    #endregion

    #region Triggers and Collisions

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (enabled)
        {
            // Check to see if the collision.transform is our climbableTarget, and if not, 
            // check to see if its parent is our climbableTarget, then set that as the currentClimableArea
            // otherwise, just use the current collision transform.
            Transform colTrans = collision.transform;
            if (colTrans.parent != null)
            {
                colTrans = colTrans.parent.transform;
            }

            if (colTrans.gameObject.layer == climbableMaskInt)
            {
                // We double check to make sure we're colliding with the parent before moving forward
                // this is to prevent us from colliding with a TopEntry or BottomEntry and registering as being within the climbable space (thus floating in air above it)
                if (colTrans == collision.transform)
                {
                    if (colTrans != currentClimbableArea)
                    {
                        currentClimbableArea = colTrans;
                    }

                    if (currentClimbableArea != null)
                    {
                        // We are within a climbable area, so set that as true.
                        withinClimbableArea = true;

                        // If the collision.transform is below us, then we're onTopClimbable and we want to stop our velocity.
                        if (colTrans.position.y < transform.position.y)
                        {
                            onTopClimbable = true;

                            if (rb.velocity.y < -3.0)
                            {
                                // If we're falling when we enter into the climable then we zero our velocity to stop.
                                if (colTrans != climbableTarget)
                                {
                                    if (climbableTarget != null)
                                    {
                                        if (climbableTarget.position.y > rb.position.y)
                                        {
                                            rb.velocity = Vector2.zero;
                                        }
                                    }
                                }
                            }
                        }

                        // I'm not sure the reason for this or if it's still needed. I guess to see if we fell back down?
                        if (previousClimbable == currentClimbableArea && currentClimbableArea.position.y < transform.position.y)
                        {
                            rb.velocity = Vector2.zero;
                        }

                        // If by this point our climbableTarget is the currentclimbableArea, then we want to send a request for access.
                        if (climbableTarget == currentClimbableArea)
                        {
                            //Debug.Log("climbableTarget == currentclimbableArea: allowedClimbableAccess: " + allowedClimbableAccess.ToString());
                            if (!allowedClimbableAccess.Contains(climbableTarget))
                            {
                                RequestClimb(climbableTarget);
                            }
                            else
                            {
                                //Debug.Log("else: isClimbing: " + isClimbing + " withinClimbable: " + withinClimbable + " currentClimbableArea: " + currentClimbableArea + " climbableTarget: " + climbableTarget);
                                if (!isClimbing && currentClimbableArea != withinClimbable && climbableTarget != withinClimbable)
                                {
                                    Debug.Log("ClimbingAI: OnTriggerEnter2D: calling EnterClimbable.");
                                    EnterClimbable();
                                }
                            }
                        }
                    }
                }

            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (enabled)
        {
            if (collision.gameObject.layer == climbableMaskInt)
            {
                if (collision.transform.position.y < rb.position.y)
                {
                    onTopClimbable = false;
                }

                if (climbingEnabled)
                {
                    if (previousClimbable != collision.transform)
                    {
                        previousClimbable = collision.transform;
                        ExitClimbable();
                    }
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (enabled)
        {
            // Layer 6 = ground
            if (collision.collider.gameObject.layer == 6)
            {
                // Colliding with the ground layer. isGrounded && 
                if (!withinClimbableArea && !onTopClimbable)
                {
                    if (supportingClimbable != null)
                    {
                        supportingClimbable = null;
                    }
                    // Added this: "&& withinClimbable != null" and moved "withinClimbable = null" inside this if statement,
                    // I did this to try to prevent us from prematurely calling EndClimb when we're moving towards a climbableTarget
                    // and we fall from one ground level to another ground level (thus creating a collisionenter event that would trigger this)
                    if (isGrounded && climbableTarget != null && withinClimbable != null)
                    {
                        withinClimbable = null;
                        EndClimb();
                    }
                }
                else
                {
                    if (collision.collider.ClosestPoint(rb.position).y > rb.position.y)
                    {
                        Vector2 reorientDirection;
                        if (withinClimbable != null)
                        {
                            if (withinClimbable.GetComponent<Collider2D>().bounds.size.x > withinClimbable.GetComponent<Collider2D>().bounds.size.y)
                            {
                                // If the scale is longer than taller, make sure we reorient to the y axis.
                                reorientDirection = new Vector2(rb.position.x, withinClimbable.position.y);
                            }
                            else
                            {
                                // Otherwise if the scale is taller rather than longer, reorient to x axis.
                                reorientDirection = new Vector2(withinClimbable.position.x, rb.position.y);
                            }
                            rb.MovePosition(reorientDirection);
                        }
                    } else
                    {
                        // If we attempt a jump, but have missed and landed on the ground, then we want to reset things.
                        // Basically you just need a way to know if things screwed up and you're back on the ground again.
                        if (jumpAttempted)
                        {
                            jumpAttempted = false;
                            withinClimbable = null;
                            supportingClimbable = null;
                            EndClimb();
                        }
                    }
                }
            }

            if (collision.gameObject.layer == climbableMaskInt)
            {
                if (collision.collider.transform.position.y < rb.position.y)
                {
                    onTopClimbable = true;
                }
            }
        }
        
    }

    /*private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == 8)
        {
            onTopClimbable = false;
        }
    }*/

    #endregion
}
