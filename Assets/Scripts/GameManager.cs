using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public static GameManager Instance => instance;

    [Header("UI")]
    [SerializeField, Tooltip("L'interface utilisateur pour afficher le score")]
    private GameObject interfaceJeu;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le menu du jeu")]
    private GameObject menu;

    [SerializeField, Tooltip("Text des infos du jeu")]
    private TextMeshProUGUI interfaceJeuInfo;

    private Chrono chrono = new();

    [System.Serializable]
    private struct MancheDeJeu
    {
        [System.Serializable]
        public struct CibleInfo
        {
            public GameObject prefab;
            public Vector3 position;
            public Quaternion rotation;
        }

        public List<CibleInfo> cibles;
        public float tempsManche;
    }

    [SerializeField, Tooltip("Liste des manches possibles")]
    private List<MancheDeJeu> listesDeManches = new();

    [SerializeField]
    private readonly List<GameObject> ciblesActuelles = new();
    private int mancheCourante;
    private bool partieEnCours;

    private void Awake()
    {
        Debug.Assert(instance != null && instance != this, "Il ne doit y avoir qu'une seule instance de GameManager dans la scène");
    }

    private void Update()
    {
        MettreAJourUI();
    }

    public void DemarrerJeu()
    {
        partieEnCours = true;
        mancheCourante = 0;

        menu.SetActive(false);
        interfaceJeu.SetActive(true);

        chrono.SetTempsEcouleCallback(TerminerPartie);

        LancerManche(listesDeManches[0]);

        MettreAJourUI();
    }

    private void LancerManche(MancheDeJeu manche)
    {
        DetruireCiblesActuelles();

        foreach (MancheDeJeu.CibleInfo cibleInfo in manche.cibles)
        {
            Debug.Assert(cibleInfo.prefab, "Prefab de cible manquant dans la manche");
            Debug.Assert(cibleInfo.position != null, "Position de cible manquant dans la manche");
            Debug.Assert(cibleInfo.rotation != null, "Rotation de cible manquant dans la manche");
            GameObject instance = Instantiate(cibleInfo.prefab, cibleInfo.position, cibleInfo.rotation);
            ICible cible = instance.GetComponent<ICible>();

            cible.onDetruit.AddListener(OnCibleDetruite);
            ciblesActuelles.Add(instance);
        }

        MettreAJourUI();
        chrono.Demarrer(manche.tempsManche);
    }

    private void OnCibleDetruite(ICible cible)
    {
        ciblesActuelles.Remove(cible.gameObject);

        MettreAJourUI();

        if (ciblesActuelles.Count <= 0)
        {
            mancheCourante++;
            LancerManche(listesDeManches[mancheCourante]);
        }
    }

    private void TerminerPartie()
    {
        Debug.Assert(partieEnCours && menu.activeSelf, "TerminerPartie appelé alors qu'aucune partie n'est en cours");

        partieEnCours = false;

        chrono.Arreter();

        DetruireCiblesActuelles();

        menu.SetActive(true);
        interfaceJeu.SetActive(false);
        MettreAJourUI();
    }

    private void DetruireCiblesActuelles()
    {
        for (int i = 0; i < ciblesActuelles.Count; i++)
        {
            if (ciblesActuelles[i] != null)
                Destroy(ciblesActuelles[i]);
        }

        ciblesActuelles.Clear();
    }

    private void MettreAJourUI()
    {
        interfaceJeuInfo.text =
            $"temps restant: {chrono.GetFormatString()}\n" +
            $"Manche {mancheCourante + 1}/{listesDeManches.Count}\n" +
            $"Cibles: {ciblesActuelles.Count}";
    }
}
