using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Représente le chronomètre du jeu.
/// </summary>
public class Chrono
{
    private TextMeshProUGUI afficheurTemps;

    private float tempsRestant = 0;
    public float TempsRestant { get => tempsRestant; set => tempsRestant = value; }

    private UnityEvent onTempsEcoule = new();

    public void Update()
    {
        if (TempsRestant > 0.0f) {
            TempsRestant -= Time.deltaTime;
            if (TempsRestant < 0.0f) {
                TempsRestant = 0.0f;
                onTempsEcoule?.Invoke();
            }
        }
    }

    public string GetFormatString()
    {
        int minutes = Mathf.FloorToInt(tempsRestant / 60.0f);
        int secondes = Mathf.FloorToInt(tempsRestant % 60.0f);
        return string.Format("{0:00}:{1:00}", minutes, secondes);
    }

    public void Demarrer(float temps)
    {
        TempsRestant = temps;
    }

    public void SetTempsEcouleCallback(UnityAction callback)
    {
        onTempsEcoule.RemoveListener(callback);
        onTempsEcoule.AddListener(callback);
    }

    public void Arreter()
    {
        TempsRestant = 0f;
    }
}
