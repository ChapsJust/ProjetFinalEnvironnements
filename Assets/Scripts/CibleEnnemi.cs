using UnityEngine;

public class CibleEnnemi : Cible
{
    [SerializeField, Tooltip("Points donnes en frappant l'ennemi")]
    private int points = 1;

    protected override void OnCibleFrappee(Marteau marteau)
    {
        Debug.Log("Ennemi frappe !");
    }
}
