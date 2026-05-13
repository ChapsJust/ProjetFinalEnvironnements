using UnityEngine;

public class CibleRonde : Cible
{
    [Header("═══ MORT ═══")]
    [Tooltip("Vitesse minimum du marteau pour tuer")]
    [SerializeField] private float minSpeedToKill = .05f;

    [Header("═══ EFFETS ═══")]
    [SerializeField] private AudioClip hitSound;

    public override void ReceiveHit(HitInfo info)
    {
        Debug.Log("CibleRonde reçue un hit avec une vitesse de " + info.impactSpeed + " m/s");
        bool shouldDie = info.impactSpeed >= minSpeedToKill && info.lethal;

        if (shouldDie)
            Die(info);
    }

    private void Die(HitInfo info)
    {
        PlaySound(hitSound, info.point);
        NotifierDetruit();

        Destroy(gameObject);
    }

    private void PlaySound(AudioClip clip, Vector3 point)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, point);
    }
}
