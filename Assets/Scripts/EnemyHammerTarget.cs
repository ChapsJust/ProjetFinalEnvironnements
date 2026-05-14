using UnityEngine;

/// <summary>
/// Exemple d'ennemi qui réagit aux coups de marteau.
/// À adapter selon ton type d'ennemi (humanoïde, créature, etc.)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyHammerTarget : Cible
{
    [Header("═══ SUIVI ═══")]
    [Tooltip("Vitesse de déplacement vers le joueur")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Transform target;
    [Tooltip("Distance à laquelle l'ennemi arrête de marcher (pour ne pas pousser le joueur)")]
    [SerializeField] private float stopDistance = 0.6f;
    [Tooltip("Délai avant que l'ennemi commence à bouger (effet d'apparition)")]
    [SerializeField] private float warmupDelay = 1.0f;

    [Header("═══ KNOCKBACK ═══")]
    [Tooltip("Multiplicateur de la force reçue du marteau")]
    [SerializeField] private float knockbackMultiplier = 1f;
    [Tooltip("Composante verticale ajoutée au knockback (effet 'envoie en l'air')")]
    [SerializeField] private float upwardForce = 4f;

    [Header("═══ MORT ═══")]
    [Tooltip("Vitesse minimum du marteau pour tuer")]
    [SerializeField] private float minSpeedToKill = .05f;
    [Tooltip("Délai avant destruction du GameObject (laisse le ragdoll voler)")]
    [SerializeField] private float destroyDelay = 5f;

    [Header("═══ EFFETS ═══")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private AudioClip hitSound;

    [Header("═══ APPARITION ═══")]
    [Tooltip("Son fort joué quand l'ennemi apparaît")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 0.9f;
    [Tooltip("Effet visuel optionnel au spawn (particules)")]
    [SerializeField] private GameObject spawnVFXPrefab;
    [Tooltip("Durée de l'animation de grossissement à l'apparition (secondes)")]
    [SerializeField] private float spawnScaleDuration = 0.5f;

    [Header("═══ PAS ═══")]
    [Tooltip("Son discret joué à intervalle régulier pendant la marche")]
    [SerializeField] private AudioClip footstepSound;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.15f;
    [Tooltip("Délai entre deux bruits de pas (secondes)")]
    [SerializeField] private float footstepInterval = 0.45f;

    [Header("═══ ANTI-BLOCAGE ═══")]
    [Tooltip("Intervalle entre deux vérifications de blocage (secondes)")]
    [SerializeField] private float stuckCheckInterval = 2.5f;
    [Tooltip("Distance minimum à parcourir entre deux checks pour ne pas être considéré bloqué")]
    [SerializeField] private float stuckMoveThreshold = 0.15f;
    [Tooltip("Si l'ennemi descend sous ce Y, il est respawn (tombé hors map)")]
    [SerializeField] private float yMinimum = -10f;
    [Tooltip("Distance à laquelle l'ennemi réapparaît autour du joueur quand bloqué (m)")]
    [SerializeField] private float respawnRadius = 6f;

    private Rigidbody rb;
    private bool isDead;
    private float spawnTime;
    private Vector3 baseScale;
    private float footstepTimer;
    private Vector3 lastStuckCheckPos;
    private float lastStuckCheckTime;
    private float lastDistanceToTarget = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        target = TrouverJoueur();
        spawnTime = Time.time;

        // Démarre tout petit pour l'animation d'apparition.
        baseScale = transform.localScale;
        if (spawnScaleDuration > 0f)
            transform.localScale = Vector3.zero;
    }

    void Start()
    {
        // Son d'apparition + VFX.
        PlaySound(spawnSound, transform.position, spawnVolume);
        if (spawnVFXPrefab != null)
        {
            GameObject vfx = Instantiate(spawnVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // Initialise l'anti-blocage.
        lastStuckCheckPos = transform.position;
        lastStuckCheckTime = Time.time;
    }

    void Update()
    {
        AnimerApparition();
        GererBruitDePas();
    }

    private void AnimerApparition()
    {
        if (spawnScaleDuration <= 0f) return;

        float t = (Time.time - spawnTime) / spawnScaleDuration;
        if (t >= 1f)
        {
            transform.localScale = baseScale;
            // Désactive l'animation une fois finie (évite de recalculer chaque frame).
            spawnScaleDuration = 0f;
            return;
        }
        transform.localScale = Vector3.Lerp(Vector3.zero, baseScale, t);
    }

    private void GererBruitDePas()
    {
        if (isDead || target == null) return;
        // Pas de bruit de pas pendant l'apparition (le son de spawn suffit).
        if (Time.time - spawnTime < warmupDelay) return;
        if (footstepSound == null) return;

        // Pas de bruit si on est arrêté contre le joueur.
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.magnitude <= stopDistance) return;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            footstepTimer = footstepInterval;
            PlaySound(footstepSound, transform.position, footstepVolume);
        }
    }

    Transform TrouverJoueur()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.transform;

        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    void FixedUpdate()
    {
        GererDeplacement();
        VerifierBlocage();
    }

    private void VerifierBlocage()
    {
        if (isDead) return;

        // 1) Tombé hors carte (knockback trop fort, trou, etc.) -> respawn
        if (transform.position.y < yMinimum)
        {
            Respawner();
            return;
        }

        if (Time.time - lastStuckCheckTime < stuckCheckInterval) return;

        bool procheDuJoueur = false;
        float distNow = 0f;
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            distNow = dir.magnitude;
            procheDuJoueur = distNow <= stopDistance + 0.3f;
        }

        // Pendant le warmup on ne juge pas bloqué.
        bool enWarmup = Time.time - spawnTime < warmupDelay + 0.5f;

        // Premier check: on initialise simplement la distance de référence.
        if (lastDistanceToTarget < 0f || target == null)
        {
            lastDistanceToTarget = distNow;
            lastStuckCheckTime = Time.time;
            lastStuckCheckPos = transform.position;
            return;
        }

        // 2) "Bloqué" = n'a pas progressé vers le joueur depuis le dernier check.
        //    Couvre 3 cas:
        //    - immobile contre un mur
        //    - ragdoll qui roule loin du joueur (vers le vide, etc.)
        //    - ragdoll qui tourne en rond
        float progres = lastDistanceToTarget - distNow; // positif = se rapproche
        bool pasDeProgres = progres < stuckMoveThreshold;

        if (pasDeProgres && !procheDuJoueur && !enWarmup)
        {
            Respawner();
            return;
        }

        lastDistanceToTarget = distNow;
        lastStuckCheckTime = Time.time;
        lastStuckCheckPos = transform.position;
    }

    private void Respawner()
    {
        if (isDead || target == null) return;

        // Nouvelle position aléatoire autour du joueur.
        float angle = Random.Range(0f, 360f);
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * respawnRadius;
        Vector3 newPos = target.position + offset;

        // Snap au sol via raycast vers le bas pour ne pas réapparaître dans les airs ni sous la map.
        Vector3 rayStart = newPos + Vector3.up * 10f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            newPos.y = hit.point.y + 0.05f;
        else
            newPos.y = target.position.y;

        // Téléporte le rigidbody et reset sa vitesse pour repartir propre.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = newPos;
        transform.position = newPos;

        // Oriente face au joueur.
        Vector3 lookDir = target.position - newPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // Reset des timers (warmup + anti-blocage) comme s'il venait de spawner.
        spawnTime = Time.time;
        lastStuckCheckPos = newPos;
        lastStuckCheckTime = Time.time;
        lastDistanceToTarget = -1f; // forcera une réinitialisation au prochain check

        // Petit feedback audio (plus discret que le vrai spawn).
        PlaySound(spawnSound, newPos, spawnVolume * 0.7f);
    }

    private void GererDeplacement()
    {
        if (isDead || target == null)
            return;

        // Petit délai d'apparition pour laisser le joueur réagir.
        if (Time.time - spawnTime < warmupDelay)
            return;

        Vector3 direction = target.position - rb.position;
        direction.y = 0f;

        // Arrête de marcher quand on est assez proche, pour ne pas
        // pousser physiquement le joueur. Le contact suffit à le toucher.
        if (direction.magnitude <= stopDistance)
            return;

        Vector3 nextPosition = rb.position + moveSpeed * Time.fixedDeltaTime * direction.normalized;
        rb.MovePosition(nextPosition);
    }

    public override void ReceiveHit(HitInfo info)
    {
        Debug.Log("Enemy received hit at " + info.point + " with speed " + info.impactSpeed);
        if (isDead) return;


        bool shouldDie = info.impactSpeed >= minSpeedToKill && info.lethal;

        ApplyKnockback(info);
        if (shouldDie)
            Die(info);
    }

    private void ApplyKnockback(HitInfo info)
    {
        // Force = direction de l'impact + composante vers le haut
        Vector3 force = info.knockbackForce * knockbackMultiplier * info.direction.normalized
                      + 0.02f * info.knockbackForce * upwardForce * Vector3.up;

        rb.AddForceAtPosition(force, info.point, ForceMode.Impulse);
    }

    private void Die(HitInfo info)
    {
        isDead = true;
        PlaySound(hitSound, info.point);
        SpawnDieEffect(info.point);
        NotifierDetruit();

        // IMPORTANT: désactive le CollisionCallback avant que le ragdoll
        // ne commence à rouler, sinon le cadavre qui touche le joueur
        // déclenche encore une défaite alors qu'on vient de gagner.
        DesactiverCallbackDeCollision();

        // Désactive l'IA / NavMeshAgent / scripts d'attaque s'il y en a
        DisableEnemyBehaviour();

        // Active le ragdoll si présent, sinon force physique sur le rigidbody principal
        EnableRagdoll(info);

        Destroy(gameObject, destroyDelay);
    }

    private void DesactiverCallbackDeCollision()
    {
        CollisionCallback cc = GetComponent<CollisionCallback>();
        if (cc != null)
            cc.enabled = false;
    }

    private void DisableEnemyBehaviour()
    {
        // Désactive tout MonoBehaviour qui ne soit pas ce script et le rigidbody
        // Ajuste selon tes scripts d'IA
        var agents = GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>();
        foreach (var a in agents) a.enabled = false;

        var animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;
    }

    private void EnableRagdoll(HitInfo info)
    {
        // Si ton ennemi a un ragdoll setup, active tous les rigidbodies enfants
        var ragdollBodies = GetComponentsInChildren<Rigidbody>();
        foreach (var body in ragdollBodies)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }

        // Force massive sur le rigidbody principal
        Vector3 launchForce = 2f * info.knockbackForce * knockbackMultiplier * info.direction.normalized
                            + 5f * upwardForce * Vector3.up;
        rb.AddForceAtPosition(launchForce, info.point, ForceMode.Impulse);

        // Spin chaotique pour effet dramatique
        rb.AddTorque(Random.insideUnitSphere * info.knockbackForce, ForceMode.Impulse);
    }

    private void SpawnDieEffect(Vector3 point)
    {
        if (hitVFXPrefab != null)
        {
            var vfx = Instantiate(hitVFXPrefab, point, Quaternion.identity);
            Destroy(vfx, 3f);
        }
    }

    private void PlaySound(AudioClip clip, Vector3 point, float volume = 1f)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, point, Mathf.Clamp01(volume));
    }
}