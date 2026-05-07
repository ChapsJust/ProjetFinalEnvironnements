using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField, Tooltip("Le jeu de Whack-A-Mole à gérer")]
    private WhackAMole whackAMole;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le score")]
    private GameObject interfaceJeu;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le menu du jeu")]
    private GameObject menu;

    void Awake()
    {
        instance = this;
        whackAMole.SetOnTerminerPartie(OnTerminerPartie);
    }

    public void DemarrerJeu()
    {
        menu.SetActive(false);
        interfaceJeu.SetActive(true);
        whackAMole.DemarrerPartie();
    }

    private void OnTerminerPartie()
    {
        menu.SetActive(true);
        interfaceJeu.SetActive(false);
    }
}
