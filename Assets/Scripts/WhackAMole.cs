using UnityEngine;
using UnityEngine.Events;

public class WhackAMole : MonoBehaviour
{
    [SerializeField, Tooltip("Les taupes du jeu")]
    private Mole[] moles;

    [SerializeField, Tooltip("Durée du jeu en secondes")]
    private float dureeJeu = 30f;

    private float tempsEntreApparitions = 1.5f;

    private float delaieApparition = 1f;

    private bool enJeu = false;

    [SerializeField, Tooltip("Le chronomètre du jeu")]
    private Chrono chrono;

    [SerializeField, Tooltip("Le pointage")]
    private Pointage pointage;

    [SerializeField, Tooltip("Le pointage")]
    private UnityEvent onTerminerPartie;

    [SerializeField, Tooltip("Le meilleur pointage")]
    private PointageRecord pointageRecord;

    void Update()
    {
        if (enJeu) {
            delaieApparition -= Time.deltaTime;
            if (delaieApparition <= 0) {
                if (ApparaitreTaupe()) {
                    delaieApparition = tempsEntreApparitions;
                }
            }
        }
    }

    void Awake()
    {
        foreach (var m in moles) m.Jeu = this;
        Debug.Log("instance chrono : " + chrono);
        chrono.SetTempsEcouleCallback(TerminerPartie);
    }

    public void SetOnTerminerPartie(UnityAction action)
    {
        onTerminerPartie.AddListener(action);
    }

    bool ApparaitreTaupe()
    {
        int index = Random.Range(0, moles.Length);

        Mole mole = moles[index];
        bool actif = mole.Actif;
        if (!actif)
            mole.Activer();
        return !actif;
    }

    void TerminerPartie()
    {
        enJeu = false;
        gameObject.SetActive(false);
        Debug.Log("Partie terminée ! Pointage final : " + pointage.Points);
        onTerminerPartie?.Invoke();
        if (pointageRecord.Points < pointage.Points)
            pointageRecord.Points = pointage.Points;
    }

    public void DemarrerPartie()
    {
        gameObject.SetActive(true);
        pointage.Points = 0;
        enJeu = true;
        chrono.Demarrer(dureeJeu);
    }

    public void OnFrapperTaupe(Mole taupe)
    {
        pointage.Points += 1;
    }
}
