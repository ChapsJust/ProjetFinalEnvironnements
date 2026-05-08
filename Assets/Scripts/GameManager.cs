using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le score")]
    private GameObject interfaceJeu;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le menu du jeu")]
    private GameObject menu;

    void Awake()
    {
        instance = this;
    }

    public void DemarrerJeu()
    {
        menu.SetActive(false);
        interfaceJeu.SetActive(true);
    }

    private void OnTerminerPartie()
    {
        menu.SetActive(true);
        interfaceJeu.SetActive(false);
    }

    public void OnFrapperCible(Cible cible)
    {
    }
}
