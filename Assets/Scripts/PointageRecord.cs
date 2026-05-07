using TMPro;
using UnityEngine;

public class PointageRecord : MonoBehaviour
{

    [SerializeField, Tooltip("Nombre de points")]
    private int points = 0;
    public int Points {
        get => points;
        set {
            points = value;
            MettreAJourAffichage();
        }
    }

    [SerializeField, Tooltip("Afficheur du pointage record")]
    private TextMeshProUGUI afficheur;

    /// <summary>
    /// Met à jour l'affichage du pointage record.
    /// </summary>
    private void MettreAJourAffichage()
    {
        afficheur.text = $"record : {points} points";
    }
}
