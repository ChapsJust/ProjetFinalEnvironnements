using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// MarteauThor — Script de contrôle du marteau de Thor avec lancer, retour automatique, et gestion des impacts en combat.
/// - Lancer : boost de vitesse à la release, spin visuel, et son de lancer.
/// - Retour : après un délai ou sur input, le marteau revient à la main en
/// (Aide avec Chat GPT)
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class MarteauThor : MonoBehaviour
{
    // 
    #region States
    //

    public enum HammerState
    {
        Held,           // Tenu par le joueur
        Thrown,         // En vol libre après lancer
        Returning,      // Retour vers la main (kinematic)
        AwaitingCatch   // Flotte près de la main, prêt à être saisi
    }

    #endregion

    //
    #region Inspector — Throw
    // 

    [Header(" LANCER ")]
    [Tooltip("Multiplicateur appliqué à la vélocité XRI au release")]
    [SerializeField] private float throwSpeedMultiplier = 2f;

    [Tooltip("Spin visuel autour du manche au lancer (degrés/s)")]
    [SerializeField] private float throwSpinSpeed = 720f;

    [Tooltip("Vitesse minimum pour considérer un impact comme 'kill' (m/s)")]
    [SerializeField] private float minSpeedForKill = 1.5f;

    [Tooltip("Vitesse minimum pour jouer le son d'impact (m/s) - évite le spam sur le sol")]
    [SerializeField] private float minSpeedForImpactSfx = 0.6f;

    [Tooltip("Tag du joueur - le marteau ignorera les collisions physiques avec lui")]
    [SerializeField] private string playerTag = "Player";

    #endregion

    // 
    #region Inspector — Return
    // 

    [Header(" RETOUR AUTOMATIQUE ")]
    [Tooltip("Secondes avant retour automatique (0 = désactivé)")]
    [SerializeField] private float autoReturnDelay = 3f;

    [Tooltip("Distance hors scène déclenchant un reset d'urgence (m)")]
    [SerializeField] private float emergencyResetDistance = 50f;

    [Header(" VOL DE RETOUR ")]
    [Tooltip("Vitesse de déplacement du marteau pendant le retour (m/s)")]
    [SerializeField] private float returnSpeed = 12f;

    [Tooltip("Douceur du démarrage (0 = instantané, 0.4 = très progressif)")]
    [SerializeField] private float returnStartSmoothing = 0.25f;

    [Tooltip("Distance à laquelle le marteau passe en mode AwaitingCatch (m)")]
    [SerializeField] private float catchDistance = 0.35f;

    [Header(" ORIENTATION RETOUR ")]
    [Tooltip("Axe local du manche pointant vers la main (Forward = Z+)")]
    [SerializeField] private Vector3 handleLocalAxis = Vector3.forward;

    [Tooltip("Vitesse d'alignement du manche vers la main")]
    [SerializeField] private float alignmentSpeed = 6f;

    [Tooltip("Spin Mjolnir en vol de retour (degrés/s)")]
    [SerializeField] private float returnSpinSpeed = 540f;

    [Tooltip("Distance à laquelle le spin commence à s'estomper (m)")]
    [SerializeField] private float spinFadeDistance = 2f;

    [Header(" ATTENTE DE RATTRAPAGE ")]
    [Tooltip("Secondes pendant lesquelles le marteau flotte avant de retomber")]
    [SerializeField] private float floatingDuration = 2.5f;

    #endregion

    // 
    #region Inspector — Combat
    // 

    [Header(" COMBAT ")]
    [Tooltip("Force de knockback de base (multipliée par la vitesse d'impact)")]
    [SerializeField] private float baseKnockbackForce = 5f;

    [Tooltip("Si true, tue toujours instantanément peu importe la vitesse")]
    [SerializeField] private bool alwaysInstantKill = false;

    [Tooltip("Layers contenant les ennemis (optimisation - évite les checks inutiles)")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Tooltip("Cooldown anti-double-impact sur le même ennemi (secondes)")]
    [SerializeField] private float hitCooldown = 0.15f;

    #endregion

    // 
    #region Inspector — Input & Feedback
    // 

    [Header(" RAPPEL MANUEL ")]
    [SerializeField] private InputActionReference recallAction;

    [Header(" HAPTIQUES ")]
    [SerializeField] private float grabHapticAmplitude = 1f;
    [SerializeField] private float grabHapticDuration = 0.1f;
    [SerializeField] private float returnHapticAmplitude = 0.6f;
    [SerializeField] private float returnHapticDuration = 0.15f;
    [SerializeField] private float impactHapticAmplitude = 0.8f;
    [SerializeField] private float impactHapticDuration = 0.2f;

    [Header(" AUDIO ")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip returnSound;
    [SerializeField] private AudioClip killSound;

    [Header(" TRAIL VISUEL ")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private Color trailStartColor = new(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color trailEndColor = new(0.1f, 0.3f, 1f, 0f);
    [SerializeField] private float trailWidth = 0.09f;
    [SerializeField] private float trailDuration = 0.55f;

    #endregion

    // 
    #region Private State
    // 

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

    // 
    #region Unity Lifecycle
    // 

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        originalGravity = rb.useGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BuildTrail();
    }

    void Start()
    {
        // Ignore les collisions entre le marteau et le joueur pour éviter que le marteau ne soit stoppé ou dévié par le corps du joueur, surtout pendant le retour.
        IgnorePlayerCollisions();
    }

    /// <summary>
    /// Configure Physics.IgnoreCollision entre tous les colliders du marteau et du joueur.
    /// </summary>
    private void IgnorePlayerCollisions()
    {
        if (string.IsNullOrEmpty(playerTag)) return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
        foreach (Collider hammerCol in colliders)
        {
            if (hammerCol == null) continue;
            foreach (Collider playerCol in playerColliders)
            {
                if (playerCol == null) continue;
                Physics.IgnoreCollision(hammerCol, playerCol, true);
            }
        }
    }

    void OnEnable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        if (recallAction != null)
        {
            recallAction.action.Enable();
            recallAction.action.performed -= OnRecallInput;
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
        if (recallRequested)
        {
            recallRequested = false;
            if (state == HammerState.Thrown) StartReturn();
        }
        // State machine principale
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

    // 
    #region Grab Events
    // 

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

    // Applique le boost de vitesse et le spin après le release. Doit être différé de quelques frames pour que XRI applique d'abord sa vélocité.
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

    //
    #region Thrown State
    // 

    // Pendant que le marteau est en vol libre après le lancer, on vérifie s'il sort trop loin (reset d'urgence) et on déclenche le retour automatique après un délai.
    private void UpdateThrown()
    {
        thrownTimer += Time.deltaTime;

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

    // 
    #region Return State
    // 

    private void OnRecallInput(InputAction.CallbackContext ctx) => recallRequested = true;

    private void StartReturn()
    {
        if (lastHand == null) return;

        state = HammerState.Returning;
        currentReturnSpeed = 0f;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Pendant le retour, on désactive les collisions physiques et la possibilité de saisir pour éviter les interférences avec le mouvement contrôlé par script.
        SetColliders(true);
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

        currentReturnSpeed = Mathf.Lerp(
            currentReturnSpeed, returnSpeed,
            Time.deltaTime / Mathf.Max(returnStartSmoothing, 0.001f)
        );

        float step = Mathf.Min(currentReturnSpeed * Time.deltaTime,
                               distance - catchDistance * 0.5f);
        Vector3 direction = (target - transform.position).normalized;
        transform.position += direction * step;

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

        rb.isKinematic = false;
        rb.useGravity = false;
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

    //
    #region Combat
    //

    // Pour le combat, on track la vélocité à la main car celle de XRI n'est pas fiable en vol. On utilise cette vélocité pour calculer les dégâts et les effets d'impact.
    private void TrackVelocity()
    {
        if (Time.deltaTime > 0f)
            currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position;
    }

    // Lorsqu'on entre en collision, on vérifie si c'est un ennemi, on applique les dégâts et les knockback en fonction de la vélocité d'impact, et on joue les effets correspondants.
    void OnCollisionEnter(Collision collision)
    {
        if (state == HammerState.AwaitingCatch) return;

        float impactSpeed = Mathf.Max(
            collision.relativeVelocity.magnitude,
            currentVelocity.magnitude
        );

        GameObject hitObject = collision.gameObject;
        if ((enemyLayers.value & (1 << hitObject.layer)) == 0)
        {
            PlayImpactEffects(impactSpeed);
            return;
        }

        int instanceId = hitObject.GetInstanceID();
        if (recentHits.TryGetValue(instanceId, out float lastHit) &&
            Time.time - lastHit < hitCooldown)
            return;
        recentHits[instanceId] = Time.time;

        Cible target = hitObject.GetComponentInParent<Cible>();
        if (target == null)
        {
            PlayImpactEffects(impactSpeed);
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        Vector3 impactDir = currentVelocity.sqrMagnitude > 0.01f
            ? currentVelocity.normalized
            : -contact.normal;

        // Détermine si l'impact est suffisamment fort pour être un "kill". Si alwaysInstantKill est true, tous les impacts sont des kills
        bool kill = alwaysInstantKill || impactSpeed >= minSpeedForKill;

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
        if (impactSpeed < minSpeedForImpactSfx) return;

        PlaySound(impactSound);
        float hapticStrength = impactHapticAmplitude * Mathf.Clamp01(impactSpeed / 10f);
        SendHaptic(lastHand, hapticStrength, impactHapticDuration);
    }

    /// <summary>
    /// Nettoie périodiquement les entrées de recentHits pour éviter que la liste ne grossisse indéfiniment. Les entrées sont supprimées si elles sont plus anciennes que 2 fois le hitCooldown.
    /// Aide chat gpt
    /// </summary>
    private void CleanupHitCooldowns()
    {
        if (recentHits.Count == 0 || Time.frameCount % 120 != 0) return;

        var toRemove = new System.Collections.Generic.List<int>();
        foreach (var kvp in recentHits)
            if (Time.time - kvp.Value > hitCooldown * 2f)
                toRemove.Add(kvp.Key);
        foreach (var id in toRemove)
            recentHits.Remove(id);
    }

    #endregion

    // 
    #region Helpers
    // 

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

    // Configure le TrailRenderer pour créer une traînée visuelle derrière le marteau pendant le vol. Utilise un shader compatible avec les pipelines de rendu courants.
    // (AIDE CHAT GPT)
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