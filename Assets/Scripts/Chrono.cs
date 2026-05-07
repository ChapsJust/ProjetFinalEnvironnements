using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Events;

/// <summary>
/// Représente le chronomètre du jeu.
/// </summary>
public class Chrono: MonoBehaviour
{
    [SerializeField, Tooltip("Afficheur du temps")]
    private TextMeshProUGUI afficheurTemps;

    [SerializeField, Tooltip("Durée restante du chronomètre en secondes")]
    private float tempsRestant = 0;

    [SerializeField, Tooltip("Événement lorsque le temps est écoulé")]
    UnityEvent onTempsEcoule;

    private void Update()
    {
        if (tempsRestant > 0.0f) {
            tempsRestant -= Time.deltaTime;
            if (tempsRestant < 0.0f) {
                tempsRestant = 0.0f;
                onTempsEcoule?.Invoke();
            }
            MettreAJourAffichage();
        }
    }

    /// <summary>
    /// Met à jour l'affichage du temps restant dans le format mm:ss.
    /// </summary>
    private void MettreAJourAffichage()
    {
        int minutes = Mathf.FloorToInt(tempsRestant / 60.0f);
        int secondes = Mathf.FloorToInt(tempsRestant % 60.0f);
        afficheurTemps.text = string.Format("{0:00}:{1:00}", minutes, secondes);
    }

    public void Demarrer(float temps)
    {
        tempsRestant = temps;
    }

    public void SetTempsEcouleCallback(UnityAction callback)
    {
        onTempsEcoule.AddListener(callback);
    }
}
