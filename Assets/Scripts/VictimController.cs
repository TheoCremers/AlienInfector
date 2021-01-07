using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VictimController : HealthSystem
{
    public int safetyLayerID;
    public LayerMask obstacleMask;
    public Renderer victimRenderer;
    public Animator animator;

    [Header("Idle behaviour")]
    private Transform victimCollection;
    private Transform AOICollection;
    public float randomWalkRange = 1f;
    public float defaultSpeed = 4f;
    public Vector2 randomWalkWaitRange = new Vector2 (1f, 5f);
    public Vector2 walkSpeedRange = new Vector2 (4f, 8f);

    [Header("Alterting")]
    public LayerMask victimMask;
    public float alertRadius = 5f;
    public int warningNoise = 50;
    public int deathNoise = 200;
    public float alertCD = 1f;
    float nextAlertTime = 0f;
    
    [Header("Fleeing properties")]
    public float fleeLength = 1f;
    public int fleeRays = 7;
    public float fleeConeAngle = 180f;
    public float fleeRecalculateTime = 0.1f;
    Vector3 fleeDirection;

    [Header("Fighting properties")]
    public float attackSpeed = 1.5f;
    public float attackDamage = 5f;
    private float attackCDTime = 0f;
    
    NavMeshAgent agent;
    FieldOfView fow;
    WebScript holdingWeb;
    Transform dragTarget;
    float minDragDistance = 1.5f;
    float dragSpeed = 1f;
    float arenaSize = 12.5f;
    float pathRecalculateTime;
    float sleepTime;
    float deathAnimationTime = 0.5f;

    public enum State {Idle, Walking, Fleeing, Dead, Webbed, Dragged, Sleeping};
    State currentState;
    //int state = 0;
    //int fight = 0;
    int flee = 0;

    // Start is called before the first frame update
    protected override void Start()
    {
        base.Start();
        OnDeath += GetKilled;
        agent = this.GetComponent<NavMeshAgent>();
        fow = this.GetComponent<FieldOfView>();
        victimCollection = transform.parent.transform;
        AOICollection = GameObject.Find("AreasOfInterest").transform;
        pathRecalculateTime = Time.time + Random.Range(randomWalkWaitRange.x, randomWalkWaitRange.y);
        victimRenderer.material.color = Color.green;
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState) {
            case State.Idle:
                if (Time.time > pathRecalculateTime) {
                    int walktype = Random.Range(0, 100);
                    if (walktype < 88) {
                        TargetRandomLocationFromWall();
                    }
                    else if (walktype < 96) {
                        TargetRandomAOIOnMap();
                    }
                    else {
                        TargetRandomLocationOnMap();
                    }
                }

                CheckVision();
                UpdateFleeStatus();
                break;
            case State.Walking:
                if (agent.remainingDistance <= agent.stoppingDistance) {
                    currentState = 0;
                    pathRecalculateTime = Time.time + Random.Range(randomWalkWaitRange.x, randomWalkWaitRange.y);
                }
                CheckVision();
                UpdateFleeStatus();
                break;
            case State.Fleeing:
                PeriodicallyAlertOthers();
                if (Time.time > pathRecalculateTime) {
                    CheckVision();
                    SmartFleeInDirection(fleeDirection);
                    pathRecalculateTime = Time.time + fleeRecalculateTime;
                }
                break;
            case State.Dead:
                break;
            case State.Webbed:
                PeriodicallyAlertOthers();
                if (holdingWeb.webActive) {
                    if (Time.time > attackCDTime) {
                        holdingWeb.TakeDamage(attackDamage);
                        attackCDTime = Time.time + attackSpeed;
                    }
                }
                else {
                    currentState = State.Fleeing;
                    agent.isStopped = false;
                    animator.SetBool("struggling", false);
                }
                break;
            case State.Dragged:
                PeriodicallyAlertOthers();
                RotateToFocus(dragTarget, true);
                Vector3 vectorToTarget = dragTarget.position - transform.position;
                vectorToTarget.y = 0f;
                float distanceToTarget = vectorToTarget.magnitude;
                if (distanceToTarget > minDragDistance) {
                    if (vectorToTarget.magnitude < dragSpeed + minDragDistance) {
                        float distanceSubtraction = (distanceToTarget - minDragDistance) / distanceToTarget;
                        transform.Translate(vectorToTarget * distanceSubtraction, Space.World);
                    }
                    else {
                        transform.Translate(vectorToTarget.normalized * dragSpeed, Space.World);
                    }
                }
                break;
            case State.Sleeping:
                if (Time.time > sleepTime) {
                    currentState = 0;
                    pathRecalculateTime = Time.time + Random.Range(randomWalkWaitRange.x, randomWalkWaitRange.y);
                    agent.isStopped = false;
                    animator.SetBool("asleep", false);
                }
                break;
            default:
                break;
        }

        if (alive) {
            animator.SetFloat("velocity", agent.velocity.magnitude);
        }
    }

    void OnTriggerEnter (Collider collider) {
        // Escape arena
        if (collider.gameObject.layer == safetyLayerID) {
            if (currentState == State.Fleeing || currentState == State.Dead) {
                Destroy(gameObject);
            }
        }
        
        // Get webbed
        if (collider.CompareTag("Web")) {
            if (currentState == State.Idle || currentState == State.Walking) {
                GetWebbed(collider.GetComponent<WebScript>());
                victimRenderer.material.color = Color.red;
                fleeDirection = fow.DirFromAngle(transform.eulerAngles.y + 180, true);
            }
            else if (currentState == State.Fleeing) {
                GetWebbed(collider.GetComponent<WebScript>());
            }
        }
    }

    private void UpdateFleeStatus () {
        if (flee > 0) {
            flee -= 5;
        }
        if (flee < 5000) {
            victimRenderer.material.color = new Color((float) flee / 5000f, 1f, 0f);
        }
        else if (flee > 10000) {
            currentState = State.Fleeing;
            pathRecalculateTime = Time.time;
            agent.speed = 8f;
            victimRenderer.material.color = Color.red;

            AlertOthers(transform.position, warningNoise); // lastKnowPlayerLocation
        }
        else {
            victimRenderer.material.color = new Color(1f, (1f - (float) (flee - 5000) / 5000f), 0f);
        }
    }

    private void CheckVision() {
        bool playerSeen = false;
        Vector3 playerAverageLocation = Vector3.zero;
        float totalIntimidation = 0f;
        bool bodySeen = false;
        Vector3 bodyAverageLocation = Vector3.zero;
        int numberOfBodies = 0;

        if (fow.visibleTargets.Count > 0) {
            foreach (Transform target in fow.visibleTargets) {
                int targetLayer = target.gameObject.layer;
                if (targetLayer == 8) {
                    playerSeen = true;
                    playerAverageLocation += target.position; //* target.intimidation;
                    totalIntimidation += 1; // target.intimidation;
                }
                else if (targetLayer == 13) {
                    bodySeen = true;
                    bodyAverageLocation += target.position;
                    numberOfBodies += 1;
                }
            }

            if (bodySeen) {
                bodyAverageLocation /= (float) numberOfBodies;
                BodySeen(bodyAverageLocation, numberOfBodies);
            }
            if (playerSeen) {
                playerAverageLocation /= totalIntimidation;
                PlayerSeen(playerAverageLocation, totalIntimidation);
            }
        }
    }

    public void GetKilled() {
        currentState = State.Dead;
        victimRenderer.material.color = Color.black;
        agent.isStopped = true;
        agent.enabled = false;
        this.gameObject.layer = 13;
        //this.GetComponent<Collider>().enabled = false;
        //Rigidbody body = this.gameObject.AddComponent<Rigidbody>();
        //body.mass = 2f;
        AlertOthers(transform.position, deathNoise);
        animator.SetFloat("velocity", 0);
        animator.SetBool("struggling", false);
        animator.SetBool("asleep", false);
        animator.SetBool("death", true);
        /*if (!animator.GetBool("asleep") && !animator.GetBool("pinned")) {
            animator.SetTrigger("fall");
        }*/
        StartCoroutine(StopAnimatorAfterDeath());
    }

    IEnumerator StopAnimatorAfterDeath() {
        yield return new WaitForSeconds(deathAnimationTime);
        animator.enabled = false;
    }

    public void GetWebbed(WebScript webObject) {
        if (webObject.webActive) {
            currentState = State.Webbed;
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            holdingWeb = webObject;
            animator.SetBool("struggling", true);
        }
    }

    public void GetDragged(Transform targetTransform) {
        if (alive && flee < 10000) {
            flee = 15000;
            UpdateFleeStatus();
        }
        currentState = State.Dragged;
        dragTarget = targetTransform;
    }
    
    public void StopDrag() {
        if (alive) {
            currentState = State.Fleeing;
            fleeDirection = (transform.position - dragTarget.position).normalized;
        }
        else {
            currentState = State.Dead;
        }
    }
    
    public void GetSleeped(float sleepDuration) {
        if (currentState != State.Webbed) {
            currentState = State.Sleeping;
            agent.velocity = Vector3.zero;
            agent.destination = transform.position;
            agent.isStopped = true;
            sleepTime = Time.time + sleepDuration;
            victimRenderer.material.color = Color.yellow;
            flee = 5000;
            animator.SetBool("asleep", true);
        }
    }

    private void TargetRandomLocationFromWall () {
        float randomRadianAngle = Random.Range(0.0f, Mathf.PI * 2);
        Vector3 randomDirection = new Vector3(Mathf.Cos(randomRadianAngle), 0, Mathf.Sin(randomRadianAngle));
        RaycastHit hit;

        // TODO: cant this go on forever?
        while (Physics.Raycast(transform.position, randomDirection, out hit, randomWalkRange, obstacleMask)) {
            randomRadianAngle = Random.Range(0.0f, Mathf.PI * 2);
            randomDirection = new Vector3(Mathf.Cos(randomRadianAngle), 0, Mathf.Sin(randomRadianAngle));
        }
        agent.speed = defaultSpeed;
        agent.SetDestination(transform.position + randomDirection * randomWalkRange);
        pathRecalculateTime = Time.time + Random.Range(randomWalkWaitRange.x, randomWalkWaitRange.y);
    }

    private void TargetRandomVictimOnMap() {
        int randomVictimID = Random.Range(0, victimCollection.childCount);
        Vector3 targetLocation = victimCollection.GetChild(randomVictimID).position;

        agent.speed = Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        agent.SetDestination(targetLocation);
        currentState = State.Walking;
    }

    private void TargetRandomLocationOnMap() {
        Vector3 targetLocation = new Vector3(Random.Range(-20f,20f), 0f, Random.Range(-20f, 20f));

        agent.speed = Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        agent.SetDestination(targetLocation);
        currentState = State.Walking;
    }

    private void TargetRandomAOIOnMap () {
        int randomID = Random.Range(0, AOICollection.childCount);
        Transform targetTransform = AOICollection.GetChild(randomID);
        float sizeX = targetTransform.localScale.x / 2f;
        float sizeZ = targetTransform.localScale.z / 2f;
        Vector3 targetLocation = targetTransform.position + Vector3.right * Random.Range(-sizeX,sizeX) + Vector3.forward * Random.Range(-sizeZ, sizeZ);

        agent.speed = Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        agent.SetDestination(targetLocation);
        currentState = State.Walking;
    }

    private void SimpleFleeInDirection (Vector3 Direction) {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, Direction, out hit, obstacleMask)) {
            agent.SetDestination(hit.point);
        }
        else {
            EscapeArena(Direction);
        }
    }

    private void SmartFleeInDirection (Vector3 Direction) {
        Vector3[] directions = new Vector3[fleeRays];

        // construct rays to cast
        directions[0] = Quaternion.Euler(0, -fleeConeAngle / 2f, 0) * Direction;
        float angleIncrement = fleeConeAngle / (fleeRays - 1);
        for (int i = 1; i < fleeRays; i++) {
            directions[i] = Quaternion.Euler(0, angleIncrement, 0) * directions[i-1];
        }

        // Set probabilities of each direction to be chosen
        int[] probabilityShare = new int[fleeRays];
        for (int i = 0; i < (fleeRays - 1) / 2 + 1; i++) {
            probabilityShare[i] = i;
            probabilityShare[fleeRays-i-1] = i;
        }

        // Cast rays and see if obstacles are hit
        for (int i = 0; i < fleeRays; i++) {
            Debug.DrawRay(transform.position, directions[i]*fleeLength, Color.red, fleeRecalculateTime);
            if (Physics.Raycast(transform.position, directions[i], fleeLength, obstacleMask)) {
                probabilityShare[i] = 0;
            }
        }
        
        // Find the total probability size
        int totalShare = 0;
        foreach (int i in probabilityShare) {
            totalShare += i;
        }

        //  Randomly select a direction to flee
        int j = -1;
        if (totalShare > 0) {
            int choice = Random.Range(0, totalShare) + 1;
            
            while (choice > 0) {
                j++;
                choice -= probabilityShare[j];
            }
            agent.SetDestination(transform.position + directions[j] * fleeLength);
        }
        else {
            // Cornered, WIP
            j = Random.Range(0, 2);
            j *= fleeRays-1;
            SimpleFleeInDirection(directions[j]);
        }
        fleeDirection = directions[j];
    }
    
    private void FleeArenaFromPoint(Vector3 point) {
        // Determine direction to flee
        Vector3 direction = transform.position - point;
        direction.y = 0;
        EscapeArena(direction);
    }

    private void EscapeArena(Vector3 escapeDirection) {
        // Calculate xfactor to hit the arena border
        float xfactor;
        if (Mathf.Sign(escapeDirection.x) > 0) {
            xfactor = (arenaSize - transform.position.x) / escapeDirection.x;
        }
        else if (Mathf.Sign(escapeDirection.x) < 0) {
            xfactor = (-arenaSize - transform.position.x) / escapeDirection.x;
        }
        else {
            xfactor = float.MaxValue;
        }
        // Calculate zfactor to hit the arena border
        float zfactor;
        if (Mathf.Sign(escapeDirection.z) > 0) {
            zfactor = (arenaSize - transform.position.z) / escapeDirection.z;
        }
        else if (Mathf.Sign(escapeDirection.z) < 0) {
            zfactor = (-arenaSize - transform.position.z) / escapeDirection.z;
        }
        else {
            zfactor = float.MaxValue;
        }

        Vector3 escapeTarget = transform.position + (escapeDirection * Mathf.Min(xfactor, zfactor));
        agent.SetDestination(escapeTarget);
    }

    private void PeriodicallyAlertOthers() {
        if (alive) {
            if (Time.time > nextAlertTime) {
                AlertOthers(transform.position, warningNoise);
                nextAlertTime = Time.time + alertCD;
            }
        }
    }

    public void AlertOthers (Vector3 pointOfFear, int noise) {
        Collider[] victimsInRange = Physics.OverlapSphere(transform.position, alertRadius, victimMask);
        
        foreach (Collider victim in victimsInRange) {
            Vector3 dirToTarget = victim.transform.position - transform.position;
            float dstToTarget = dirToTarget.magnitude;
            dirToTarget = dirToTarget.normalized;
            if (Physics.Raycast(transform.position, dirToTarget, dstToTarget, obstacleMask)) {
                noise /= 2;
            }
            victim.GetComponent<VictimController>().HearScream(pointOfFear, noise);
        }
    }

    public void PlayerSeen (Vector3 playerPosition, float intimidation) {
        if (currentState == State.Idle || currentState == State.Walking) { // Not yet fleeing
            flee += (int) (150 * intimidation);
            agent.ResetPath();
        }
        fleeDirection = transform.position - playerPosition;
        fleeDirection = fleeDirection.normalized;
    }

    public void BodySeen (Vector3 bodyPosition, int numberOfBodies) {
        if (currentState == State.Idle || currentState == State.Walking) {
            flee += 100 * numberOfBodies;
            agent.ResetPath();
            fleeDirection = transform.position - bodyPosition;
            fleeDirection = fleeDirection.normalized;
            pathRecalculateTime = Time.time + fleeRecalculateTime;
        }
    }

    public void HearScream (Vector3 position, int noise) {
        if (currentState == State.Idle || currentState == State.Walking) {
            flee += noise;
            fleeDirection = transform.position - position;
            fleeDirection = fleeDirection.normalized;
            agent.ResetPath();
            pathRecalculateTime = Time.time + fleeRecalculateTime;

            // Rotate to face origin position
            Vector3 direction = position - transform.position;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void RotateToFocus (Transform focus, bool flip) {
        Vector3 direction = (focus.position - transform.position).normalized;
        if (flip) {
            direction = -direction;
        }
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}
