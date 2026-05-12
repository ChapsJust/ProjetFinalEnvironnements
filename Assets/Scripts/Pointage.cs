using UnityEngine;
using TMPro;

public class Pointage : MonoBehaviour
{
    [SerializeField, Tooltip("Nombre de points")]
    private int points = 0;
    public int Points
    {
        get => points;
        set
        {
            points = value;
            MettreAJourAffichage();
        }
    }

    [SerializeField, Tooltip("Afficheur des points")]
    private TextMeshProUGUI afficheur;

    private void Awake()
    {
        MettreAJourAffichage();
    }

    public void AjouterPoints(int pointsAjoutes)
    {
        Points += pointsAjoutes;
    }

    public void Reinitialiser()
    {
        Points = 0;
    }

    /// <summary>
    /// Met � jour l'affichage des points.
    /// </summary>
    private void MettreAJourAffichage()
    {
        afficheur.text = $"{points} points";
    }
}
