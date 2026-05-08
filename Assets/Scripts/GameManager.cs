using System.Collections.Generic;
using UnityEngine;


public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public GameManager Instance {
        get => instance;
    }

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le score")]
    private GameObject interfaceJeu;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le menu du jeu")]
    private GameObject menu;

    [System.Serializable]
    private class CombinaisonDeCibles
    {
        public List<Cible> cibles;
    }

    [SerializeField, Tooltip("Liste des combinaisons possibles de cibles")]
    private List<CombinaisonDeCibles> listesDeCombinaisons;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Construction dupliqué du singleton GameManager");
        }

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
