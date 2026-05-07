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

    /// <summary>
    /// Met à jour l'affichage des points.
    /// </summary>
    private void MettreAJourAffichage()
    {
        afficheur.text = $"{points} points";
    }
}
