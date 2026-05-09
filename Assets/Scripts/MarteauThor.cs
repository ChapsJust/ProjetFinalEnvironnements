using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Mjolnir-style hammer — XR Interaction Toolkit 3.x
///
/// FIXES v3:
///   [1] Trail plus long (time + width augmentés, configurable inspector)
///   [2] Grab désactivé pendant Retour → plus de lag grab+recall simultané
///       Grab réactivé uniquement quand le marteau flotte près de la main (AttenteRattrapage)
///   [3] Bypass corrigé : retour kinematic MovePosition frame-by-frame
///       Le marteau ne peut plus dépasser la main quelle que soit la vitesse
///   [4] Orientation : alignement commence DÈS le début du retour, pas seulement à 1.5m
///       Spin Mjolnir s'estompe progressivement en approchant → arrive toujours droit
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class MarteauThor : MonoBehaviour
{
    #region ── Enums ────────────────────────────────────────────────────────────
    private enum EtatMarteau { EnMain, Lance, Retour, AttenteRattrapage }
    #endregion

    #region ── Inspector ────────────────────────────────────────────────────────

    [Header("═══ LANCER ═══")]
    [Tooltip("Multiplicateur appliqué à la vélocité XRI au moment du release")]
    [SerializeField] private float multiplicateurVitesse = 2f;
    [Tooltip("Spin visuel autour du manche au lancer (degrés/s)")]
    [SerializeField] private float spinLancer = 720f;

    [Header("═══ RETOUR AUTOMATIQUE ═══")]
    [Tooltip("Secondes avant retour automatique")]
    [SerializeField] private float delaiRetourAuto = 3f;
    [Tooltip("Distance hors scène déclenchant un reset d'urgence (m)")]
    [SerializeField] private float distanceSecurite = 50f;

    [Header("═══ VOL DE RETOUR ═══")]
    [Tooltip("Vitesse de déplacement du marteau pendant le retour (m/s)")]
    [SerializeField] private float vitesseRetour = 12f;
    [Tooltip("Douceur du démarrage (0 = instantané, 0.4 = très progressif)")]
    [SerializeField] private float smoothDemarrageRetour = 0.25f;
    [Tooltip("Distance à laquelle le marteau passe en mode AttenteRattrapage (m)")]
    [SerializeField] private float distanceRattrapage = 0.35f;

    [Header("═══ ORIENTATION RETOUR ═══")]
    [Tooltip("Axe local du manche pointant vers la main. Forward = Z+, Up = Y+")]
    [SerializeField] private Vector3 axePoigneeLocal = Vector3.forward;
    [Tooltip("Vitesse d'alignement du manche vers la main (plus grand = plus vif)")]
    [SerializeField] private float vitesseAlignement = 6f;
    [Tooltip("Spin Mjolnir en vol de retour (degrés/s), s'estompe en approchant")]
    [SerializeField] private float vitesseRotationRetour = 540f;
    [Tooltip("Distance à laquelle le spin commence à s'estomper (m)")]
    [SerializeField] private float distanceArretSpin = 2f;

    [Header("═══ ATTENTE DE RATTRAPAGE ═══")]
    [Tooltip("Secondes pendant lesquelles le marteau flotte avant de retomber")]
    [SerializeField] private float delaiAvantChute = 2.5f;

    [Header("═══ RAPPEL MANUEL ═══")]
    [Tooltip("Action Input (bouton A/X ou autre) pour rappeler le marteau")]
    [SerializeField] private InputActionReference actionRappel;

    [Header("═══ HAPTIQUES ═══")]
    [SerializeField] private float amplitudeGrab = 1f;
    [SerializeField] private float dureeGrab = 0.1f;
    [SerializeField] private float amplitudeRetour = 0.6f;
    [SerializeField] private float dureeRetour = 0.15f;
    [SerializeField] private float amplitudeImpact = 0.8f;
    [SerializeField] private float dureeImpact = 0.2f;

    [Header("═══ AUDIO ═══")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonLancer;
    [SerializeField] private AudioClip sonImpact;
    [SerializeField] private AudioClip sonRetour;

    [Header("═══ TRAIL VISUEL ═══")]
    [Tooltip("Crée un TrailRenderer automatiquement, rien à assigner dans l'inspector")]
    [SerializeField] private bool activerTrail = true;
    [SerializeField] private Color couleurTrailDebut = new Color(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color couleurTrailFin = new Color(0.1f, 0.3f, 1f, 0f);
    [Tooltip("Largeur du trail au départ")]
    [SerializeField] private float largeurTrail = 0.09f;
    [Tooltip("Durée de vie des segments en secondes. Plus grand = trail plus long")]
    [SerializeField] private float dureeTrail = 0.55f;

    #endregion

    #region ── Private State ────────────────────────────────────────────────────

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Collider[] colliders;
    private TrailRenderer trail;
    private bool graviteOriginale;

    private EtatMarteau etat = EtatMarteau.EnMain;
    private IXRSelectInteractor derniereMain;
    private float tempsDepuisLancer;
    private float tempsAttenteRattrapage;
    private float vitesseRetourCourante;   // pour le smooth démarrage
    private bool rappelRequis;

    #endregion

    #region ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        graviteOriginale = rb.useGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ConstruireTrail();
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabEntered);
        grabInteractable.selectExited.AddListener(OnGrabExited);

        if (actionRappel != null)
        {
            actionRappel.action.Enable();
            actionRappel.action.performed += OnRappelPerformed;
        }
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabEntered);
        grabInteractable.selectExited.RemoveListener(OnGrabExited);

        if (actionRappel != null)
            actionRappel.action.performed -= OnRappelPerformed;
    }

    void Update()
    {
        // ── Rappel manuel ─────────────────────────────────────────────────────
        if (rappelRequis)
        {
            rappelRequis = false;
            if (etat == EtatMarteau.Lance) CommencerRetour();
        }

        // ── Machine d'états ───────────────────────────────────────────────────
        switch (etat)
        {
            case EtatMarteau.Lance:
                tempsDepuisLancer += Time.deltaTime;

                if (derniereMain != null &&
                    Vector3.Distance(transform.position, derniereMain.transform.position) > distanceSecurite)
                {
                    Debug.LogWarning("[MarteauThor] Hors limites → reset d'urgence");
                    ResetUrgence();
                    return;
                }

                if (tempsDepuisLancer >= delaiRetourAuto)
                    CommencerRetour();
                break;

            case EtatMarteau.Retour:
                if (derniereMain?.transform == null) { AbandonnerRetour(); return; }
                MettreAJourRetour();
                break;

            case EtatMarteau.AttenteRattrapage:
                MettreAJourAttenteRattrapage();
                break;
        }
    }

    #endregion

    #region ── Grab Events ──────────────────────────────────────────────────────

    private void OnGrabEntered(SelectEnterEventArgs args)
    {
        // Pas besoin de StopperPhysiqueRetour ici si on vient d'AttenteRattrapage,
        // mais on l'appelle quand même au cas où le grab arrive dans un autre état
        StopperPhysiqueRetour();

        etat = EtatMarteau.EnMain;
        derniereMain = args.interactorObject;
        rappelRequis = false;

        SetTrail(false);

        EnvoyerHaptique(args.interactorObject, amplitudeGrab, dureeGrab);
        JouerSon(sonLancer);
    }

    private void OnGrabExited(SelectExitEventArgs args)
    {
        if (etat != EtatMarteau.EnMain) return;

        etat = EtatMarteau.Lance;
        tempsDepuisLancer = 0f;
        derniereMain = args.interactorObject;

        SetTrail(true);
        JouerSon(sonLancer);
        StartCoroutine(BoosterVitesseLancer());
    }

    #endregion

    #region ── Lancer ───────────────────────────────────────────────────────────

    private IEnumerator BoosterVitesseLancer()
    {
        // Attendre 2 FixedUpdate : XRI 3.x applique la throw velocity en différé
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        rb.isKinematic = false;
        rb.useGravity = graviteOriginale;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity *= multiplicateurVitesse;

        // Spin visuel autour du manche
        Vector3 axeMonde = transform.TransformDirection(axePoigneeLocal.normalized);
        rb.angularVelocity = axeMonde * (spinLancer * Mathf.Deg2Rad);
    }

    #endregion

    #region ── Retour ───────────────────────────────────────────────────────────

    private void OnRappelPerformed(InputAction.CallbackContext ctx) => rappelRequis = true;

    private void CommencerRetour()
    {
        if (derniereMain == null) return;

        etat = EtatMarteau.Retour;
        vitesseRetourCourante = 0f;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SetColliders(false);
        SetGrabActif(false);

        JouerSon(sonRetour);
    }

    private void MettreAJourRetour()
    {
        Vector3 cible = derniereMain.transform.position;
        float distance = Vector3.Distance(transform.position, cible);

        // ── Arrivée ──────────────────────────────────────────────────────────
        if (distance <= distanceRattrapage)
        {
            ArriverDansMain();
            return;
        }

        // ── Déplacement kinematic frame-by-frame (FIX [3]) ───────────────────
        // Accélération progressive au démarrage
        vitesseRetourCourante = Mathf.Lerp(
            vitesseRetourCourante, vitesseRetour,
            Time.deltaTime / Mathf.Max(smoothDemarrageRetour, 0.001f)
        );

        // Déplacement max cette frame : plafonné pour ne jamais dépasser la cible
        float pas = Mathf.Min(vitesseRetourCourante * Time.deltaTime,
                                      distance - distanceRattrapage * 0.5f);
        Vector3 direction = (cible - transform.position).normalized;
        transform.position += direction * pas;

        // ── Orientation (FIX [4] : dès le début, pas seulement à 1.5m) ───────
        if (direction != Vector3.zero)
        {
            Quaternion rotCible = Quaternion.FromToRotation(
                transform.TransformDirection(axePoigneeLocal.normalized),
                direction
            ) * transform.rotation;

            transform.rotation = Quaternion.Slerp(
                transform.rotation, rotCible,
                vitesseAlignement * Time.deltaTime
            );
        }

        // ── Spin Mjolnir qui s'estompe en approchant ─────────────────────────
        float spinFactor = Mathf.Clamp01(distance / Mathf.Max(distanceArretSpin, 0.01f));
        if (spinFactor > 0.01f)
            transform.Rotate(
                axePoigneeLocal.normalized,
                vitesseRotationRetour * spinFactor * Time.deltaTime,
                Space.Self
            );
    }

    private void ArriverDansMain()
    {
        etat = EtatMarteau.AttenteRattrapage;
        tempsAttenteRattrapage = 0f;

        // ⚡ FIX : sortir de kinematic AVANT de réactiver le grab
        // XRI a besoin d'un rigidbody dynamique pour initialiser son velocity tracker
        rb.isKinematic = false;
        rb.useGravity = false;              // pas de gravité pendant l'attente, mais physique active
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SetTrail(false);
        SetColliders(true);
        SetGrabActif(true);

        EnvoyerHaptique(derniereMain, amplitudeRetour, dureeRetour);
        JouerSon(sonRetour);
    }

    private void MettreAJourAttenteRattrapage()
    {
        tempsAttenteRattrapage += Time.deltaTime;

        // ⚡ FIX : si déjà grabbé, on arrête tout — XRI gère le transform maintenant
        if (grabInteractable.isSelected)
            return;

        // Floating près de la main SEULEMENT si pas encore grabbé
        if (derniereMain?.transform != null)
        {
            // Utiliser MovePosition au lieu de transform.position direct pour ne pas
            // perturber le tracking de XRI si on passe en select pendant le Lerp
            Vector3 nouvelle = Vector3.Lerp(
                transform.position,
                derniereMain.transform.position,
                Time.deltaTime * 25f
            );
            transform.position = nouvelle;
        }

        if (tempsAttenteRattrapage >= delaiAvantChute)
        {
            rb.useGravity = graviteOriginale;  // gravité revient pour la chute
            AbandonnerRetour();
        }
    }

    private void AbandonnerRetour()
    {
        StopperPhysiqueRetour();
        SetTrail(false);
        SetColliders(true);
        SetGrabActif(true);

        etat = EtatMarteau.Lance;
        tempsDepuisLancer = 0f;
    }

    private void ResetUrgence()
    {
        if (derniereMain?.transform != null)
            transform.position = derniereMain.transform.position;
        AbandonnerRetour();
    }

    private void StopperPhysiqueRetour()
    {
        rb.isKinematic = false;
        rb.useGravity = graviteOriginale;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    #endregion

    #region ── Impact ───────────────────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (etat != EtatMarteau.Lance) return;

        float force = collision.impulse.magnitude;
        if (force < 1f) return;

        JouerSon(sonImpact);
        EnvoyerHaptique(derniereMain, amplitudeImpact * Mathf.Clamp01(force / 10f), dureeImpact);
    }

    #endregion

    #region ── Helpers ──────────────────────────────────────────────────────────

    /// Active/désactive le XRGrabInteractable.
    /// Désactivé pendant le Retour → pas de grab accidentel ni de lag.
    /// Réactivé en AttenteRattrapage → le joueur peut saisir normalement.
    private void SetGrabActif(bool actif) => grabInteractable.enabled = actif;

    private void SetColliders(bool actif)
    {
        foreach (var col in colliders)
            col.enabled = actif;
    }

    private void SetTrail(bool actif)
    {
        if (trail == null) return;
        trail.emitting = actif;
        if (!actif) trail.Clear();
    }

    private void JouerSon(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void EnvoyerHaptique(IXRSelectInteractor interactor, float amplitude, float duree)
    {
        if (interactor is XRBaseInputInteractor ctrl)
            ctrl.SendHapticImpulse(amplitude, duree);
    }

    private void ConstruireTrail()
    {
        if (!activerTrail) return;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = dureeTrail;           // FIX [1] : 0.55s vs 0.18s avant
        trail.startWidth = largeurTrail;         // FIX [1] : 0.09 vs 0.06 avant
        trail.endWidth = 0f;
        trail.startColor = couleurTrailDebut;
        trail.endColor = couleurTrailFin;
        trail.minVertexDistance = 0.02f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;

        // Shader compatible Built-in / URP / HDRP — tente les 3 dans l'ordre
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