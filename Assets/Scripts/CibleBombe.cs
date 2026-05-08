using UnityEngine;

public class CibleBombe : Cible
{
    [SerializeField, Tooltip("Degats infliges par la bombe")]
    private int degats = 1;

    protected override void OnCibleFrappee(Marteau marteau)
    {
        Debug.Log("Bombe frappee !");
    }
}
