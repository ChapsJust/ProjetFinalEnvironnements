using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Mjolnir-style hammer pour XR Interaction Toolkit 3.x
///
/// FONCTIONNALITÉS
/// ───────────────
/// • Lancer avec spin et boost de vélocité
/// • Retour automatique après délai ou rappel manuel (bouton)
/// • Vol kinematic frame-by-frame (impossible de bypasser la main)
/// • Orientation progressive du manche vers la main
/// • Combat : kill instantané ou knockback selon vitesse d'impact
/// • Tracking de vélocité indépendant de XRI pour des dégâts précis
/// • Trail visuel auto-généré
/// • Haptiques, audio, gestion d'urgence si hors limites
///
/// SETUP REQUIS
/// ────────────
/// • XRGrabInteractable : Movement Type = Velocity Tracking, Throw On Detach = ON,
///   Smooth Position/Rotation = OFF
/// • Ennemis : implémenter IDamageable (voir EnemyHammerTarget.cs)
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class MarteauThor : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    #region States
    // ════════════════════════════════════════════════════════════════════════

    public enum HammerState
    {
        Held,           // Tenu par le joueur
        Thrown,         // En vol libre après lancer
        Returning,      // Retour vers la main (kinematic)
        AwaitingCatch   // Flotte près de la main, prêt à être saisi
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Inspector — Throw
    // ════════════════════════════════════════════════════════════════════════

    [Header("═══ LANCER ═══")]
    [Tooltip("Multiplicateur appliqué à la vélocité XRI au release")]
    [SerializeField] private float throwSpeedMultiplier = 2f;

    [Tooltip("Spin visuel autour du manche au lancer (degrés/s)")]
    [SerializeField] private float throwSpinSpeed = 720f;

    [Tooltip("Vitesse minimum pour considérer un impact comme 'kill' (m/s)")]
    [SerializeField] private float minSpeedForKill = .03f;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Inspector — Return
    // ════════════════════════════════════════════════════════════════════════

    [Header("═══ RETOUR AUTOMATIQUE ═══")]
    [Tooltip("Secondes avant retour automatique (0 = désactivé)")]
    [SerializeField] private float autoReturnDelay = 3f;

    [Tooltip("Distance hors scène déclenchant un reset d'urgence (m)")]
    [SerializeField] private float emergencyResetDistance = 50f;

    [Header("═══ VOL DE RETOUR ═══")]
    [Tooltip("Vitesse de déplacement du marteau pendant le retour (m/s)")]
    [SerializeField] private float returnSpeed = 12f;

    [Tooltip("Douceur du démarrage (0 = instantané, 0.4 = très progressif)")]
    [SerializeField] private float returnStartSmoothing = 0.25f;

    [Tooltip("Distance à laquelle le marteau passe en mode AwaitingCatch (m)")]
    [SerializeField] private float catchDistance = 0.35f;

    [Header("═══ ORIENTATION RETOUR ═══")]
    [Tooltip("Axe local du manche pointant vers la main (Forward = Z+)")]
    [SerializeField] private Vector3 handleLocalAxis = Vector3.forward;

    [Tooltip("Vitesse d'alignement du manche vers la main")]
    [SerializeField] private float alignmentSpeed = 6f;

    [Tooltip("Spin Mjolnir en vol de retour (degrés/s)")]
    [SerializeField] private float returnSpinSpeed = 540f;

    [Tooltip("Distance à laquelle le spin commence à s'estomper (m)")]
    [SerializeField] private float spinFadeDistance = 2f;

    [Header("═══ ATTENTE DE RATTRAPAGE ═══")]
    [Tooltip("Secondes pendant lesquelles le marteau flotte avant de retomber")]
    [SerializeField] private float floatingDuration = 2.5f;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Inspector — Combat
    // ════════════════════════════════════════════════════════════════════════

    [Header("═══ COMBAT ═══")]
    [Tooltip("Force de knockback de base (multipliée par la vitesse d'impact)")]
    [SerializeField] private float baseKnockbackForce = 5f;

    [Tooltip("Si true, tue toujours instantanément peu importe la vitesse")]
    [SerializeField] private bool alwaysInstantKill = false;

    [Tooltip("Layers contenant les ennemis (optimisation - évite les checks inutiles)")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Tooltip("Cooldown anti-double-impact sur le même ennemi (secondes)")]
    [SerializeField] private float hitCooldown = 0.15f;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Inspector — Input & Feedback
    // ════════════════════════════════════════════════════════════════════════

    [Header("═══ RAPPEL MANUEL ═══")]
    [SerializeField] private InputActionReference recallAction;

    [Header("═══ HAPTIQUES ═══")]
    [SerializeField] private float grabHapticAmplitude = 1f;
    [SerializeField] private float grabHapticDuration = 0.1f;
    [SerializeField] private float returnHapticAmplitude = 0.6f;
    [SerializeField] private float returnHapticDuration = 0.15f;
    [SerializeField] private float impactHapticAmplitude = 0.8f;
    [SerializeField] private float impactHapticDuration = 0.2f;

    [Header("═══ AUDIO ═══")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip returnSound;
    [SerializeField] private AudioClip killSound;

    [Header("═══ TRAIL VISUEL ═══")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private Color trailStartColor = new(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color trailEndColor = new(0.1f, 0.3f, 1f, 0f);
    [SerializeField] private float trailWidth = 0.09f;
    [SerializeField] private float trailDuration = 0.55f;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Private State
    // ════════════════════════════════════════════════════════════════════════

    private XRGrabInteractable grab;
    private Rigidbody rb;
    private Collider[] colliders;
    private TrailRenderer trail;
    private bool originalGravity;

    private HammerState state = HammerState.Held;
    private IXRSelectInteractor lastHand;
    private float thrownTimer;
    private float floatingTimer;
    private float currentReturnSpeed;
    private bool recallRequested;

    // Combat tracking
    private Vector3 previousPosition;
    private Vector3 currentVelocity;
    private readonly System.Collections.Generic.Dictionary<int, float> recentHits = new();

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        originalGravity = rb.useGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BuildTrail();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        if (recallAction != null)
        {
            recallAction.action.Enable();
            recallAction.action.performed += OnRecallInput;
        }
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);

        if (recallAction != null)
            recallAction.action.performed -= OnRecallInput;
    }

    void Update()
    {
        // Process recall input
        if (recallRequested)
        {
            recallRequested = false;
            if (state == HammerState.Thrown) StartReturn();
        }

        switch (state)
        {
            case HammerState.Thrown:
                UpdateThrown();
                break;

            case HammerState.Returning:
                UpdateReturning();
                break;

            case HammerState.AwaitingCatch:
                UpdateAwaitingCatch();
                break;
        }

        // Track velocity manually for combat (XRI's velocity isn't reliable mid-flight)
        TrackVelocity();
        CleanupHitCooldowns();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Grab Events
    // ════════════════════════════════════════════════════════════════════════

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        StopReturnPhysics();

        state = HammerState.Held;
        lastHand = args.interactorObject;
        recallRequested = false;

        SetTrail(false);

        SendHaptic(args.interactorObject, grabHapticAmplitude, grabHapticDuration);
        PlaySound(throwSound);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (state != HammerState.Held) return;

        state = HammerState.Thrown;
        thrownTimer = 0f;
        lastHand = args.interactorObject;

        SetTrail(true);
        PlaySound(throwSound);
        StartCoroutine(BoostThrowVelocity());
    }

    private IEnumerator BoostThrowVelocity()
    {
        // XRI 3.x applies throw velocity in deferred FixedUpdates
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        rb.isKinematic = false;
        rb.useGravity = originalGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity *= throwSpeedMultiplier;

        // Spin around handle axis
        Vector3 worldAxis = transform.TransformDirection(handleLocalAxis.normalized);
        rb.angularVelocity = worldAxis * (throwSpinSpeed * Mathf.Deg2Rad);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Thrown State
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateThrown()
    {
        thrownTimer += Time.deltaTime;

        // Emergency reset if hammer goes too far
        if (lastHand != null &&
            Vector3.Distance(transform.position, lastHand.transform.position) > emergencyResetDistance)
        {
            Debug.LogWarning("[MarteauThor] Out of bounds → emergency reset");
            EmergencyReset();
            return;
        }

        // Auto-return after delay
        if (autoReturnDelay > 0f && thrownTimer >= autoReturnDelay)
            StartReturn();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Return State
    // ════════════════════════════════════════════════════════════════════════

    private void OnRecallInput(InputAction.CallbackContext ctx) => recallRequested = true;

    private void StartReturn()
    {
        if (lastHand == null) return;

        state = HammerState.Returning;
        currentReturnSpeed = 0f;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SetColliders(false);
        SetGrabActive(false);

        PlaySound(returnSound);
    }

    private void UpdateReturning()
    {
        if (lastHand?.transform == null) { CancelReturn(); return; }

        Vector3 target = lastHand.transform.position;
        float distance = Vector3.Distance(transform.position, target);

        if (distance <= catchDistance)
        {
            ArriveInHand();
            return;
        }

        // Smooth acceleration
        currentReturnSpeed = Mathf.Lerp(
            currentReturnSpeed, returnSpeed,
            Time.deltaTime / Mathf.Max(returnStartSmoothing, 0.001f)
        );

        // Frame-clamped step → impossible to overshoot
        float step = Mathf.Min(currentReturnSpeed * Time.deltaTime,
                               distance - catchDistance * 0.5f);
        Vector3 direction = (target - transform.position).normalized;
        transform.position += direction * step;

        // Align handle toward hand
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.FromToRotation(
                transform.TransformDirection(handleLocalAxis.normalized),
                direction
            ) * transform.rotation;

            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot,
                alignmentSpeed * Time.deltaTime
            );
        }

        // Mjolnir spin that fades on approach
        float spinFactor = Mathf.Clamp01(distance / Mathf.Max(spinFadeDistance, 0.01f));
        if (spinFactor > 0.01f)
            transform.Rotate(
                handleLocalAxis.normalized,
                returnSpinSpeed * spinFactor * Time.deltaTime,
                Space.Self
            );
    }

    private void ArriveInHand()
    {
        state = HammerState.AwaitingCatch;
        floatingTimer = 0f;

        // ⚠ CRITICAL: exit kinematic BEFORE re-enabling grab
        // XRI needs a dynamic rigidbody to initialize its velocity tracker properly
        rb.isKinematic = false;
        rb.useGravity = false;          // No gravity while floating
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SetTrail(false);
        SetColliders(true);
        SetGrabActive(true);

        SendHaptic(lastHand, returnHapticAmplitude, returnHapticDuration);
        PlaySound(returnSound);
    }

    private void UpdateAwaitingCatch()
    {
        floatingTimer += Time.deltaTime;

        // ⚠ CRITICAL: stop fighting XRI once the player grabbed
        if (grab.isSelected) return;

        if (lastHand?.transform != null)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                lastHand.transform.position,
                Time.deltaTime * 25f
            );
        }

        if (floatingTimer >= floatingDuration)
        {
            rb.useGravity = originalGravity;
            CancelReturn();
        }
    }

    private void CancelReturn()
    {
        StopReturnPhysics();
        SetTrail(false);
        SetColliders(true);
        SetGrabActive(true);

        state = HammerState.Thrown;
        thrownTimer = 0f;
    }

    private void EmergencyReset()
    {
        if (lastHand?.transform != null)
            transform.position = lastHand.transform.position;
        CancelReturn();
    }

    private void StopReturnPhysics()
    {
        rb.isKinematic = false;
        rb.useGravity = originalGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Combat
    // ════════════════════════════════════════════════════════════════════════

    private void TrackVelocity()
    {
        // Manual velocity tracking — works in all states (Held, Thrown, etc.)
        // because rb.linearVelocity is unreliable when grabbed by XRI
        if (Time.deltaTime > 0f)
            currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (state == HammerState.AwaitingCatch) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        // Layer check (perf optimization)
        GameObject hitObject = collision.gameObject;
        if ((enemyLayers.value & (1 << hitObject.layer)) == 0)
        {
            // Not an enemy — just play impact effects
            PlayImpactEffects(impactSpeed);
            return;
        }

        // Damage cooldown per object (prevents multi-hit on same enemy)
        int instanceId = hitObject.GetInstanceID();
        if (recentHits.TryGetValue(instanceId, out float lastHit) &&
            Time.time - lastHit < hitCooldown)
            return;
        recentHits[instanceId] = Time.time;

        // Find Cible on the hit object or its parents
        Cible target = hitObject.GetComponentInParent<Cible>();
        if (target == null)
        {
            PlayImpactEffects(impactSpeed);
            return;
        }

        // Build hit info and deal damage
        ContactPoint contact = collision.GetContact(0);
        Vector3 impactDir = currentVelocity.sqrMagnitude > 0.01f
            ? currentVelocity.normalized
            : -contact.normal;

        bool kill = alwaysInstantKill || impactSpeed >= minSpeedForKill || state == HammerState.Thrown;

        Cible.HitInfo info = new(
            point: contact.point,
            direction: impactDir,
            impactSpeed: impactSpeed,
            knockbackForce: baseKnockbackForce * Mathf.Max(impactSpeed, 1f),
            lethal: kill,
            source: gameObject
        );

        target.ReceiveHit(info);
        PlayImpactEffects(impactSpeed);

        if (kill && killSound != null)
            PlaySound(killSound);
    }

    private void PlayImpactEffects(float impactSpeed)
    {
        PlaySound(impactSound);
        float hapticStrength = impactHapticAmplitude * Mathf.Clamp01(impactSpeed / 10f);
        SendHaptic(lastHand, hapticStrength, impactHapticDuration);
    }

    private void CleanupHitCooldowns()
    {
        // Periodically remove old entries to prevent dictionary growth
        if (recentHits.Count == 0 || Time.frameCount % 120 != 0) return;

        var toRemove = new System.Collections.Generic.List<int>();
        foreach (var kvp in recentHits)
            if (Time.time - kvp.Value > hitCooldown * 2f)
                toRemove.Add(kvp.Key);
        foreach (var id in toRemove)
            recentHits.Remove(id);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Helpers
    // ════════════════════════════════════════════════════════════════════════

    private void SetGrabActive(bool active) => grab.enabled = active;

    private void SetColliders(bool active)
    {
        foreach (var col in colliders)
            col.enabled = active;
    }

    private void SetTrail(bool active)
    {
        if (trail == null) return;
        trail.emitting = active;
        if (!active) trail.Clear();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void SendHaptic(IXRSelectInteractor interactor, float amplitude, float duration)
    {
        if (interactor is XRBaseInputInteractor ctrl)
            ctrl.SendHapticImpulse(amplitude, duration);
    }

    private void BuildTrail()
    {
        if (!enableTrail) return;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = trailDuration;
        trail.startWidth = trailWidth;
        trail.endWidth = 0f;
        trail.startColor = trailStartColor;
        trail.endColor = trailEndColor;
        trail.minVertexDistance = 0.02f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;

        // Cross-pipeline shader fallback
        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit");

        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            trail.material = mat;
        }
    }

    #endregion
}