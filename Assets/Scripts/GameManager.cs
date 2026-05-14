using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField, Tooltip("L'interface utilisateur pour afficher le score")]
    private GameObject interfaceJeu;

    [SerializeField, Tooltip("L'interface utilisateur pour afficher le menu du jeu")]
    private GameObject menu;

    [SerializeField, Tooltip("GameObject indiquant la victoire")]
    private GameObject interfacePartieGagne;

    [SerializeField, Tooltip("GameObject indiquant la perte de la partie")]
    private GameObject interfacePartiePerdue;

    [SerializeField, Tooltip("Text indiquant la perte de la partie")]
    private TextMeshProUGUI textPartiePerdue;

    [SerializeField, Tooltip("Text des infos du jeu")]
    private TextMeshProUGUI interfaceJeuInfo;

    [SerializeField, Tooltip("Text affichant le record du nombre de manches réussis")]
    private TextMeshProUGUI recordManchesText;


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

    [SerializeField, Tooltip("Référence au joueur")]
    private GameObject joueur;

    private readonly List<GameObject> ciblesActuelles = new();
    private int mancheCourante;
    private int recordManches = 0;
    private bool partieEnCours;

    private void Update()
    {
        chrono.Update();
        MettreAJourUI();
    }

    public void DemarrerJeu()
    {
        Debug.Assert(listesDeManches.Count > 0, "GameManager DemarrerJeu alors que la liste de manches est vide");
        Debug.Assert(!partieEnCours, "GameManager DemarrerJeu alors partieEnCours est true");

        partieEnCours = true;
        mancheCourante = 0;

        menu.SetActive(false);
        interfaceJeu.SetActive(true);
        interfacePartiePerdue.SetActive(false);
        interfacePartieGagne.SetActive(false);

        chrono.SetTempsEcouleCallback(PerdrePartieTemps);

        LancerManche(listesDeManches[0]);

        MettreAJourUI();
    }

    private void PerdrePartieTemps()
    {
        interfacePartiePerdue.SetActive(true);
        TerminerPartie();
    }

    private void PerdreEnnemiTouche()
    {
        interfacePartiePerdue.SetActive(true);
        TerminerPartie();
    }

    private void GagnerPartie()
    {
        interfacePartieGagne.SetActive(true);
        TerminerPartie();
    }

    private void TerminerPartie()
    {
        Debug.Assert(partieEnCours, "TerminerPartie appelé alors qu'aucune partie n'est en cours");

        partieEnCours = false;

        chrono.Arreter();

        DetruireCiblesActuelles();

        recordManches = Mathf.Max(mancheCourante, recordManches);

        if (mancheCourante >= listesDeManches.Count) {
            Debug.Log("Félicitations, vous avez terminé toutes les manches !");
        } else {
            Debug.Log("Temps écoulé ! Vous avez atteint la manche " + (mancheCourante + 1));
        }
        menu.SetActive(true);
        interfaceJeu.SetActive(false);
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
            Cible cible = instance.GetComponent<Cible>();

            if(instance.TryGetComponent(out CollisionCallback collisionCallback)) {
                collisionCallback.RegisterCallback(joueur, PerdreEnnemiTouche);
            }

            cible.onDetruit.AddListener(OnCibleDetruite);
            ciblesActuelles.Add(instance);
        }

        MettreAJourUI();
        chrono.Demarrer(manche.tempsManche);
    }

    private void OnCibleDetruite(Cible cible)
    {
        ciblesActuelles.Remove(cible.gameObject);

        MettreAJourUI();

        if (ciblesActuelles.Count <= 0)
        {
            mancheCourante++;
            if(mancheCourante >= listesDeManches.Count) {
                GagnerPartie();
            } else {
                LancerManche(listesDeManches[mancheCourante]);
            }
        }
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
        recordManchesText.text = $"Record de manches réussies: {recordManches}";

        textPartiePerdue.text = $"Partie perdue!\nVous avez atteint la manche {mancheCourante + 1}";
    }
}
