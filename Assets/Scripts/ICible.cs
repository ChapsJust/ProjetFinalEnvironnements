using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Représente une cible pouvant recevoir un coup de marteau.
/// </summary>
public class ICible : MonoBehaviour
{
    [SerializeField]
    public UnityEvent<ICible> onDetruit = new UnityEvent<ICible>();

    /// <summary>
    /// Reçoit les dégâts du marteau.
    /// </summary>
    /// <param name="info">Toutes les infos de l'impact</param>
    public virtual void ReceiveHit(HitInfo info)
    {

    }

    protected bool EstDetruit { get; private set; }

    protected void NotifierDetruit()
    {
        if (EstDetruit)
            return;

        EstDetruit = true;
        onDetruit?.Invoke(this);
    }

    /// <summary>
    /// Données complètes d'un impact. Passées par valeur (struct) pour éviter les allocs GC.
    /// </summary>
    public readonly struct HitInfo
    {
        public readonly Vector3 point;          // Point d'impact world space
        public readonly Vector3 direction;      // Direction du marteau au moment de l'impact (normalisée)
        public readonly float impactSpeed;      // Vitesse du marteau (m/s)
        public readonly float knockbackForce;   // Force de knockback en Newtons
        public readonly bool lethal;            // Si le hit peut tuer/détruire
        public readonly GameObject source;      // Le marteau lui-même

        public HitInfo(Vector3 point, Vector3 direction, float impactSpeed,
                        float knockbackForce, bool lethal, GameObject source)
        {
            this.point = point;
            this.direction = direction;
            this.impactSpeed = impactSpeed;
            this.knockbackForce = knockbackForce;
            this.lethal = lethal;
            this.source = source;
        }
    }

}
