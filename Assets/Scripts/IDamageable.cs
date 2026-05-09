using UnityEngine;

namespace ThorVR.Combat
{
    /// <summary>
    /// Implémenter cette interface sur tout ce qui peut subir des dégâts du marteau.
    /// Ennemis, objets destructibles, props physiques, etc.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Reçoit les dégâts du marteau.
        /// </summary>
        /// <param name="info">Toutes les infos de l'impact</param>
        void ReceiveHit(HitInfo info);
    }

    /// <summary>
    /// Données complètes d'un impact. Passées par valeur (struct) pour éviter les allocs GC.
    /// </summary>
    public readonly struct HitInfo
    {
        public readonly Vector3 point;          // Point d'impact world space
        public readonly Vector3 direction;      // Direction du marteau au moment de l'impact (normalisée)
        public readonly float impactSpeed;      // Vitesse du marteau (m/s)
        public readonly float damage;           // Dégâts bruts (utilisable si tu ajoutes des HP plus tard)
        public readonly float knockbackForce;   // Force de knockback en Newtons
        public readonly bool instantKill;       // True si l'ennemi doit mourir instantanément
        public readonly GameObject source;      // Le marteau lui-même

        public HitInfo(Vector3 point, Vector3 direction, float impactSpeed,
                       float damage, float knockbackForce, bool instantKill, GameObject source)
        {
            this.point = point;
            this.direction = direction;
            this.impactSpeed = impactSpeed;
            this.damage = damage;
            this.knockbackForce = knockbackForce;
            this.instantKill = instantKill;
            this.source = source;
        }
    }
}