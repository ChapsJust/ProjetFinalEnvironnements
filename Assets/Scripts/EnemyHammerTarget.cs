using UnityEngine;

/// <summary>
/// Exemple d'ennemi qui réagit aux coups de marteau.
/// À adapter selon ton type d'ennemi (humanoïde, créature, etc.)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyHammerTarget : Cible
{
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

    private Rigidbody rb;
    private bool isDead;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        Vector3 force = info.direction.normalized * info.knockbackForce * knockbackMultiplier
                      + Vector3.up * upwardForce * info.knockbackForce * 0.02f;

        rb.AddForceAtPosition(force, info.point, ForceMode.Impulse);
    }

    private void Die(HitInfo info)
    {
        isDead = true;
        PlaySound(hitSound, info.point);
        SpawnDieEffect(info.point);
        NotifierDetruit();

        // Désactive l'IA / NavMeshAgent / scripts d'attaque s'il y en a
        DisableEnemyBehaviour();

        // Active le ragdoll si présent, sinon force physique sur le rigidbody principal
        EnableRagdoll(info);

        Destroy(gameObject, destroyDelay);
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
        Vector3 launchForce = info.direction.normalized * info.knockbackForce * knockbackMultiplier * 2f
                            + Vector3.up * upwardForce * 5f;
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

    private void PlaySound(AudioClip clip, Vector3 point)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, point);
    }
}