using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public Camera cam;
    public GameObject MarkerObject;
    public LayerMask victimMask;
    public LayerMask obstacleMask;
    public Renderer playerRenderer;
    public Animator animator;
    public GameObject abilityPanel;
    public float walkSpeed = 8f;
    public float killRange = 1.5f;
    public int victimLayerID;
    public float killImpulse = 10f;
    public float killCD = 0.5f;
    
    public Text scoreText;
    private int score = 0;

    float killAgainTime = 0f;
    Vector3 autoReturnPosition;

    public LayerMask bodiesMask;
    public float dragSpeed = 5f;
    public float dragRadius = 1.5f;
    bool dragging = false;
    VictimController dragVictim;

    [Header("Leaping")]
    public float leapDamage = 50f;
    public float leapRange = 3f;
    public float leapSpeed = 20f;
    public float attackPoint = 0.5f;
    float attackCooldownTime = 0;

    #region abilityvars
    [Header("Sleep spell")]
    public float sleepCastRange = 8f;
    public float sleepCastTime = 0.5f;
    public float sleepRadius = 5f;
    public float sleepDuration = 30f;
    public GameObject castRangeIndicator;
    public GameObject castRadiusIndicator;
    public GameObject sleepParticleEffect;
    Material castRadiusMaterial;
    public LayerMask groundLayerID;

    [Header("Hook spell")]
    public float hookDamage = 100f;
    public float hookCastRange = 10f;
    public float hookCastTime = 0.2f;
    public float hookAutoTargetRange = 2f;
    GameObject hookLineObject;
    public GameObject hookLinePrefab;

    [Header("Web spell")]
    public float webCastRange = 10f;
    public float webCastTime = 1f;
    public float webCastWidth = 5f;
    public GameObject castLineIndicator;
    public GameObject webPrefab;
    Material castLineMaterial;
    #endregion

    List<IconImageEffects> abilityButtons = new List<IconImageEffects>();
    NavMeshAgent agent;
    Transform target;
    GameObject targetMarker;
    FieldOfView fow;
    enum Action {none, attack, sleep, hook, web};
    Action currentAction = Action.none;
    bool targetAcquired = false;
    bool leaping = false;
    bool attacking = false;
    bool autoPilot = false;
    bool autoReturning = false;
    bool groundSelected = false;
    bool aimingRadiusCast = false;
    bool aimingLineCast = false;
    bool validCastTarget = false;

    void Start () {
        agent = this.GetComponent<NavMeshAgent>();
        fow = this.GetComponent<FieldOfView>();
        castRadiusMaterial = castRadiusIndicator.GetComponent<LineRenderer>().material;
        castLineMaterial = castLineIndicator.GetComponent<LineRenderer>().material;
        abilityButtons.AddRange(abilityPanel.GetComponentsInChildren<IconImageEffects>());
    }

    void Update()
    {
        if (targetAcquired && !attacking) {
            if (target != null) {
                // Within leap range and not already leaping?
                if ((agent.remainingDistance < leapRange) && !leaping) {
                    // Check line-of-sight
                    Ray ray = new Ray(transform.position, target.position - transform.position);
                    RaycastHit hit;
                    Physics.Raycast(ray, out hit);
                    if (hit.transform == target) {
                        StartLeap();
                    }
                }

                // Every 1/5th of a second, update target position
                if (Time.frameCount % 5 == 0) {
                    agent.SetDestination(target.position);
                }

                // If target is hit, destroy
                if (leaping) {
                    if ((target.position - transform.position).magnitude < killRange) {
                        VictimController targetController = target.GetComponent<VictimController>();

                        // Calculate killforce
                        if (targetController.alive) {
                            //float yAngle = Mathf.Deg2Rad * transform.rotation.eulerAngles.y;
                            //Vector3 killForce = new Vector3(Mathf.Sin(yAngle), 0, Mathf.Cos(yAngle));
                            //killForce *= killImpulse;
                            targetController.TakeDamage(leapDamage);
                        }

                        targetAcquired = false;
                        agent.ResetPath();
                        Destroy(targetMarker);
                        EndLeap(true);
                        killAgainTime = Time.time + killCD;
                    }
                }
            }
            else {
                targetAcquired = false;
                agent.ResetPath();
                EndLeap(false);
            }
        }

        // try current action
        if (Input.GetMouseButtonDown(0)) {
            TryAction(currentAction);
        }

        // move/attack action
        if (Input.GetMouseButtonDown(1)) {
            autoPilot = false;
            SetTargetFromMouseInput();
        }

        if (Input.GetKeyDown("a")) {
            attackButton();
        }

        if (Input.GetKeyDown("s")) {
            stopButton();
        }

        if (Input.GetKeyDown("d")) {
            dragButton();
        }

        if (Input.GetKeyDown("q")) {
            sleepButton();
        }
        
        if (Input.GetKeyDown("w")) {
            hookButton();
        }
        
        if (Input.GetKeyDown("e")) {
            webButton();
        }
        
        // check for valid target in radius and linecast cases
        if (aimingRadiusCast) {
            validCastTarget = UpdateSpellRadiusIndicator(sleepCastRange);
        }
        else if (aimingLineCast) {
            validCastTarget = UpdateSpellLineIndicator(webCastRange, webCastWidth);
        }

        // auto-attack state
        if (autoPilot) {
            if ((Time.frameCount % 5 == 0) && Time.time > killAgainTime) {
                if (!targetAcquired && groundSelected && !autoReturning) {
                    agent.SetDestination(autoReturnPosition);
                    autoReturning = true;
                }
                CheckVision();
            }
        }

        //if (alive) {
            animator.SetFloat("velocity", agent.velocity.magnitude);
        //}
    }

    #region attackfunctions
    private void StartLeap () {
        agent.speed = leapSpeed;
        agent.angularSpeed = 1080;
        agent.acceleration = 100;
        leaping = true;
        playerRenderer.material.color = Color.magenta;
        animator.SetBool("leaping", true);
    }

    private void EndLeap (bool targetHit) {
        agent.speed = walkSpeed;
        agent.angularSpeed = 360;
        agent.acceleration = 25;
        leaping = false;
        playerRenderer.material.color = Color.white;
        if (targetHit) {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            RotateToFocus(target);
            animator.SetBool("attack", true);
            attacking = true;
            StartCoroutine(AttackForTime(attackPoint));
        }
        animator.SetBool("leaping", false);
    }

    IEnumerator AttackForTime (float time) {
        yield return new WaitForSeconds(time);
        animator.SetBool("attack", false);
        agent.isStopped = false;
        attacking = false;
    }
    #endregion

    #region dragging
    private void TryDrag() {
        Collider[] victimsInRange = Physics.OverlapSphere(transform.position, dragRadius, victimMask | bodiesMask);
        bool targetInRange = false;
        float closestVictimSqrDistance = float.MaxValue;
        Transform closestVictim = null;

        // Determine closest target in dragrange
        foreach (Collider victim in victimsInRange) {
            float distanceToVictim = (victim.transform.position - transform.position).sqrMagnitude;

            if (distanceToVictim < closestVictimSqrDistance) {
                closestVictimSqrDistance = distanceToVictim;
                closestVictim = victim.transform;
                targetInRange = true;
            }
        }

        if (targetInRange) { 
            VictimController controller = closestVictim.GetComponent<VictimController>();
            controller.GetDragged(transform);
            StartDrag(controller);
        }
    }

    private void StartDrag (VictimController targetController) {
        dragging = true;
        hookLineObject = Instantiate(hookLinePrefab, transform);
        hookLineObject.GetComponent<HookLineScript>().player = transform;
        hookLineObject.GetComponent<HookLineScript>().target = targetController.transform;
        dragVictim = targetController;
        targetAcquired = false;

        agent.speed = dragSpeed;
        agent.angularSpeed = 180;
        agent.acceleration = 15;
        leaping = false;
        playerRenderer.material.color = Color.cyan;

        abilityButtons[5].SetAltSprite(true);
        abilityButtons[5].SetHighlight(true);
    }

    private void StopDrag () {
        if (dragging) {
            dragVictim.StopDrag();

            agent.speed = walkSpeed;
            agent.angularSpeed = 360;
            agent.acceleration = 25;
            dragging = false;
            Destroy(hookLineObject);
            playerRenderer.material.color = Color.white;

            abilityButtons[5].SetAltSprite(false);
            abilityButtons[5].SetHighlight(false);
        }
    }
    #endregion

    #region abilitycasts
    private void CastSleep(Vector3 targetLocation, float radius) {
        // Create Cloud
        GameObject sleepCloud = Instantiate(sleepParticleEffect, targetLocation, Quaternion.Euler(-90, 0, 0));
        sleepCloud.GetComponent<SleepCloudScript>().radius = radius;
        sleepCloud.GetComponent<SleepCloudScript>().sleepDuration = sleepDuration;
        StartCoroutine(CastForTime(sleepCastTime));
    }

    public void TryHook(float castRange) {
        // Cast ray from camera
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Check if ray hit something
        if (Physics.Raycast(ray, out hit, 10000f, groundLayerID)) {
            
            Collider[] victimsInRange = Physics.OverlapSphere(hit.point, hookAutoTargetRange, victimMask | bodiesMask);
            bool targetInRange = false;
            float closestVictimSqrDistance = float.MaxValue;
            Transform closestVictim = null;

            // Determine closest target in hookrange
            foreach (Collider victim in victimsInRange) {
                float distanceToMouse = (victim.transform.position - hit.point).sqrMagnitude;
                if (distanceToMouse < closestVictimSqrDistance) {
                    Vector3 vectorToVictim = victim.transform.position - transform.position;
                    Debug.DrawRay(transform.position, vectorToVictim, Color.red, 2f);
                    if (!Physics.Raycast(transform.position, vectorToVictim, vectorToVictim.magnitude, obstacleMask)) {
                        vectorToVictim.y = 0;
                        if (vectorToVictim.magnitude < castRange) {
                            closestVictimSqrDistance = distanceToMouse;
                            closestVictim = victim.transform;
                            targetInRange = true;
                        }
                    }
                }

            }

            if (targetInRange) {
                StopDrag();
                VictimController controller = closestVictim.GetComponent<VictimController>();
                CastHook(controller);
            }
        }
    }

    public void CastHook(VictimController controller) {
        Destroy(targetMarker);
        if (controller.alive) {
            controller.TakeDamage(hookDamage);
        }
        controller.GetDragged(transform);
        StartDrag(controller);
        hookLineObject.GetComponent<HookLineScript>().splatter = true;
        StartCoroutine(CastForTime(hookCastTime));
    }

    private void CastWeb() {
        LineRenderer line = castLineIndicator.GetComponent<LineRenderer>();
        Transform lineTransform = castLineIndicator.transform;
        Vector3 anchor1 = line.GetPosition(0);
        Vector3 anchor2 = line.GetPosition(1);
        Vector3 offset = (anchor1 + anchor2) * 0.5f;
        float distance = Vector3.Distance(anchor1, anchor2);

        GameObject web = Instantiate(webPrefab, lineTransform.position, lineTransform.rotation);
        web.transform.Translate(offset);
        web.transform.localScale = new Vector3 (distance, 1, 1);
    }

    IEnumerator CastForTime (float time) {
        agent.isStopped = true;
        animator.SetBool("casting", true);
        yield return new WaitForSeconds(time);
        animator.SetBool("casting", false);
        agent.isStopped = false;
    }
    #endregion

    #region rangeindicators
    private void DrawRangeIndicator(float castRadius, float spellRadius, bool radiusTarget, bool lineTarget) {
        castRangeIndicator.DrawCircle(castRadius, 0.2f);
        if (radiusTarget) {
            castRadiusIndicator.DrawCircle(spellRadius, 0.2f);
        }
        if (lineTarget) {
            castLineIndicator.DrawLine(spellRadius, 0.2f);
        }
    }

    private bool UpdateSpellRadiusIndicator (float castRadius) {
        // Cast ray from camera
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Color newColor = Color.green;

        // Check if ray hit something
        if (Physics.Raycast(ray, out hit, 10000f, groundLayerID)) {
            Vector3 targetPoint = hit.point;
            Vector3 casterPoint = transform.position;
            targetPoint.y = casterPoint.y;
            Vector3 relativeTarget = targetPoint - casterPoint;
            newColor.a = 0.5f;
            castRadiusMaterial.color = newColor;

            // check max radius
            if (relativeTarget.magnitude > castRadius) {
                relativeTarget = relativeTarget.normalized * castRadius;
                targetPoint = casterPoint + relativeTarget;
            }
            
            // Check if there is a wall between caster and target
            Ray casterRay = new Ray(casterPoint, relativeTarget);
            RaycastHit wallHit;
            if (!Physics.Raycast(casterPoint, relativeTarget, out wallHit, relativeTarget.magnitude, obstacleMask)) {
                castRadiusIndicator.transform.position = targetPoint;
            }
            else {
                castRadiusIndicator.transform.position = wallHit.point;
            }
            return true;
        }
        else {
            castRadiusIndicator.transform.position = transform.position;
            // Target not valid, return false
            newColor = Color.red;
            newColor.a = 0.5f;
            castRadiusMaterial.color = newColor;
            return false;
        }
    }
    
    private bool UpdateSpellLineIndicator (float castRadius, float width) {
        // Cast ray from camera
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Color newColor = Color.green;

        // Check if ray hit something
        if (Physics.Raycast(ray, out hit, 10000f, groundLayerID)) {
            Vector3 targetPoint = hit.point;
            Vector3 casterPoint = transform.position;
            targetPoint.y = casterPoint.y;

            Vector3 relativeTarget = targetPoint - casterPoint;
            if (relativeTarget.magnitude > castRadius) {
                relativeTarget = relativeTarget.normalized * castRadius;
                targetPoint = casterPoint + relativeTarget;
            }

            castLineIndicator.transform.position = targetPoint;
            bool validTarget = castLineIndicator.UpdateLine(transform.position, width, obstacleMask);

            if (validTarget) {
                Ray casterRay = new Ray(casterPoint, relativeTarget);
                RaycastHit wallHit;

                // Check if there is a wall between caster and target
                if (!Physics.Raycast(casterPoint, relativeTarget, out wallHit, relativeTarget.magnitude, obstacleMask)) {
                    newColor.a = 0.5f;
                    castLineMaterial.color = newColor;
                    return true;
                }
            }
            
        }
        else {
            castLineIndicator.transform.position = transform.position;
        }
        // Target not valid, return false
        newColor = Color.red;
        newColor.a = 0.5f;
        castLineMaterial.color = newColor;
        return false;
    }

    private void RemoveRangeIndicators() {
        castRangeIndicator.GetComponent<LineRenderer>().enabled = false;
        castRadiusIndicator.GetComponent<LineRenderer>().enabled = false;
        castLineIndicator.GetComponent<LineRenderer>().enabled = false;
        aimingLineCast = false;
        aimingRadiusCast = false;
    }
    #endregion

    #region playerfunctions
    private void SetTargetFromMouseInput () {
        // Cast ray from camera
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Check if ray hit something
        if (Physics.Raycast(ray, out hit)) {
            if (hit.transform.gameObject.layer == victimLayerID) {
                // Enemy target
                SetTarget(hit.transform);
                groundSelected = false;
            }
            else {
                // Ground target
                targetAcquired = false;
                groundSelected = true;
                autoReturnPosition = hit.point;
                Destroy(targetMarker);
                if (leaping) {
                    EndLeap(false);
                }
            }
            agent.SetDestination(hit.point);
        }
    }

    private void CheckVision () {
        bool victimSeen = false;
        float closestVictimSqrDistance = float.MaxValue;
        Transform closestVictim = null;
        
        // Determine closest target in vision
        if (fow.visibleTargets.Count > 0) {
            foreach (Transform victim in fow.visibleTargets) {
                float distanceToVictim = (victim.position - transform.position).sqrMagnitude;

                if (distanceToVictim < closestVictimSqrDistance) {
                    closestVictimSqrDistance = distanceToVictim;
                    closestVictim = victim;
                    victimSeen = true;
                }
            }
            
            if (victimSeen) {
                SetTarget(closestVictim);
            }
        }
    }
    
    private void SetTarget(Transform targetTransform) {
        // set target
        target = targetTransform;
        // Make or move marker
        if (targetAcquired) {
            targetMarker.transform.parent = target;
            targetMarker.transform.position = new Vector3(target.position.x, targetMarker.transform.position.y, target.position.z);
        }
        else {
            targetMarker = Instantiate(MarkerObject, target, false);
        }
        targetAcquired = true;
        autoReturning = false;
        StopDrag();
    }

    private void RotateToFocus (Transform focus) {
        Vector3 direction = (focus.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
    #endregion

    #region ability methods

    private void TryAction (Action action) {
        switch (action) {
            case Action.none: {
                    break;
                }
            case Action.attack: {
                    autoPilot = true;
                    SetTargetFromMouseInput();
                    break;
                }
            case Action.sleep: {
                    if (!attacking && validCastTarget) {
                        CastSleep(castRadiusIndicator.transform.position, sleepRadius);
                    }
                    break;
                }
            case Action.hook: {
                    if (!attacking) {
                        TryHook(hookCastRange);
                    }
                    break;
                }
            case Action.web: {
                    if (!attacking && validCastTarget) {
                        CastWeb();
                    }
                    break;
                }
        }
        ResetAction();
    }

    public void ResetAction() {
        RemoveRangeIndicators();
        currentAction = Action.none;
        foreach (IconImageEffects button in abilityButtons) {
            button.SetHighlight(false);
        }
    }

    public void attackButton () {
        ResetAction();
        currentAction = Action.attack;
        abilityButtons[3].SetHighlight(true);
    }
    
    public void stopButton() {
        ResetAction();
        autoPilot = false;
        agent.ResetPath();
    }

    public void dragButton() {
        ResetAction();
        if (!dragging) {
            TryDrag();
        }
        else {
            StopDrag();
        }
    }

    public void sleepButton() {
        ResetAction();
        DrawRangeIndicator(sleepCastRange, sleepRadius, true, false);
        aimingRadiusCast = true;
        currentAction = Action.sleep;
        abilityButtons[0].SetHighlight(true);
    }

    public void hookButton() {
        ResetAction();
        DrawRangeIndicator(hookCastRange, 0f, false, false);
        currentAction = Action.hook;
        abilityButtons[1].SetHighlight(true);
    }

    public void webButton() {
        ResetAction();
        DrawRangeIndicator(webCastRange, webCastWidth, false, true);
        aimingLineCast = true;
        currentAction = Action.web;
        abilityButtons[2].SetHighlight(true);
    }
    #endregion
}