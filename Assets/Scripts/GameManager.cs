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

    [Header("═══ APPARITION 360° ═══")]
    [SerializeField, Tooltip("Si vrai, les ennemis apparaissent à un angle aléatoire autour du joueur (les positions de la manche sont ignorées)")]
    private bool spawnAleatoireAutourJoueur = true;

    [SerializeField, Tooltip("Distance d'apparition autour du joueur (mètres)")]
    private float spawnRadius = 9f;

    [SerializeField, Tooltip("Variation aléatoire ajoutée au rayon (mètres) - plus c'est grand, plus la profondeur varie et les ennemis arrivent à des moments différents")]
    private float spawnRadiusJitter = 4f;

    [SerializeField, Tooltip("Distance minimum absolue entre le joueur et le spawn (mètres)")]
    private float minSpawnDistance = 5f;

    [SerializeField, Tooltip("Angle minimum entre deux ennemis qui apparaissent en même temps (degrés)")]
    private float spawnAngleMin = 30f;

    [SerializeField, Range(30f, 360f), Tooltip("Arc d'apparition autour de la direction du joueur (360 = tout autour, 180 = hémisphère avant, 120 = cône frontal serré)")]
    private float spawnArcDegrees = 120f;

    [SerializeField, Tooltip("Décalage vertical appliqué après le snap au sol (ex: 0.1 pour éviter de clipper)")]
    private float spawnYOffset = 0.05f;

    [SerializeField, Tooltip("Layers considérés comme du sol pour le snap au spawn")]
    private LayerMask spawnGroundLayers = ~0;

    private readonly List<GameObject> ciblesActuelles = new();
    private int mancheCourante;
    private int recordManches = 0;
    private bool partieEnCours;

    private const string CLE_RECORD = "RecordManches";

    private void Awake()
    {
        // Charge le record sauvegardé entre les sessions.
        recordManches = PlayerPrefs.GetInt(CLE_RECORD, 0);
    }

    private void Update()
    {
        chrono.Update();
        MettreAJourUI();
    }

    public void DemarrerJeu()
    {
        if (listesDeManches.Count <= 0) {
            Debug.LogWarning("GameManager: aucune manche configurée, démarrage annulé.");
            return;
        }
        if (partieEnCours) {
            // Double-clic sur "Démarrer" - on ignore au lieu de planter.
            return;
        }

        partieEnCours = true;
        mancheCourante = 0;

        if (menu != null) menu.SetActive(false);
        if (interfaceJeu != null) interfaceJeu.SetActive(true);
        if (interfacePartiePerdue != null) interfacePartiePerdue.SetActive(false);
        if (interfacePartieGagne != null) interfacePartieGagne.SetActive(false);

        chrono.SetTempsEcouleCallback(PerdrePartieTemps);

        LancerManche(listesDeManches[0]);

        MettreAJourUI();
    }

    private void PerdrePartieTemps()
    {
        if (interfacePartiePerdue != null) interfacePartiePerdue.SetActive(true);
        TerminerPartie();
    }

    private void PerdreEnnemiTouche()
    {
        if (!partieEnCours) return; // évite double-déclenchement (ennemi qui touche pendant l'écran de fin)
        if (interfacePartiePerdue != null) interfacePartiePerdue.SetActive(true);
        TerminerPartie();
    }

    private void GagnerPartie()
    {
        if (interfacePartieGagne != null) interfacePartieGagne.SetActive(true);
        TerminerPartie();
    }

    private void TerminerPartie()
    {
        if (!partieEnCours) return;

        partieEnCours = false;

        chrono.Arreter();

        DetruireCiblesActuelles();

        // Met à jour et sauvegarde le record entre les sessions.
        if (mancheCourante > recordManches) {
            recordManches = mancheCourante;
            PlayerPrefs.SetInt(CLE_RECORD, recordManches);
            PlayerPrefs.Save();
        }

        if (mancheCourante >= listesDeManches.Count) {
            Debug.Log("Félicitations, vous avez terminé toutes les manches !");
        } else {
            Debug.Log("Temps écoulé ! Vous avez atteint la manche " + (mancheCourante + 1));
        }
        if (menu != null) menu.SetActive(true);
        if (interfaceJeu != null) interfaceJeu.SetActive(false);
        MettreAJourUI();
    }

    private void LancerManche(MancheDeJeu manche)
    {
        DetruireCiblesActuelles();

        // On garde les angles déjà utilisés dans cette vague pour éviter
        // que deux ennemis apparaissent collés l'un sur l'autre.
        List<float> anglesUtilises = new();

        // Direction de référence: là où le joueur regarde. Prend la camera (tête VR)
        // en priorité, sinon le forward du rig. Les ennemis apparaissent dans un arc
        // centré sur cette direction.
        float baseYaw = CalculerYawDuJoueur();

        foreach (MancheDeJeu.CibleInfo cibleInfo in manche.cibles)
        {
            Debug.Assert(cibleInfo.prefab, "Prefab de cible manquant dans la manche");

            Vector3 position = cibleInfo.position;
            Quaternion rotation = cibleInfo.rotation;

            // On ne randomise au sol que les ennemis qui marchent (EnemyHammerTarget).
            // Les cibles statiques (CibleRonde, etc.) gardent la position configurée
            // dans la manche pour rester en l'air là où elles ont été placées.
            bool ennemiQuiMarche = cibleInfo.prefab.GetComponent<EnemyHammerTarget>() != null;

            if (spawnAleatoireAutourJoueur && joueur != null && ennemiQuiMarche)
            {
                float angle = TrouverAngleLibre(anglesUtilises, baseYaw);
                anglesUtilises.Add(angle);

                float rayon = spawnRadius + Random.Range(-spawnRadiusJitter, spawnRadiusJitter);
                rayon = Mathf.Max(rayon, minSpawnDistance);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * rayon;
                position = joueur.transform.position + offset;

                // Snap au sol: raycast depuis 10m au dessus pour ne jamais
                // spawner sous la map ni dans les airs.
                Vector3 rayStart = position + Vector3.up * 10f;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 50f, spawnGroundLayers, QueryTriggerInteraction.Ignore))
                {
                    position.y = groundHit.point.y + spawnYOffset;
                }
                else
                {
                    // Pas de sol trouvé: on retombe sur la position du joueur pour ne pas tomber dans le vide.
                    position.y = joueur.transform.position.y + spawnYOffset;
                }

                // Oriente l'ennemi face au joueur.
                Vector3 lookDir = joueur.transform.position - position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    rotation = Quaternion.LookRotation(lookDir);
            }

            GameObject instance = Instantiate(cibleInfo.prefab, position, rotation);
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

    private float CalculerYawDuJoueur()
    {
        // Priorité à la tête (Camera.main = caméra du casque VR) pour suivre
        // ce que le joueur regarde vraiment, sinon fallback sur le rig.
        Vector3 forward;
        if (Camera.main != null)
            forward = Camera.main.transform.forward;
        else if (joueur != null)
            forward = joueur.transform.forward;
        else
            forward = Vector3.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        return Quaternion.LookRotation(forward).eulerAngles.y;
    }

    private float TrouverAngleLibre(List<float> anglesUtilises, float baseYaw)
    {
        // L'angle final est tiré dans l'arc [baseYaw - half, baseYaw + half].
        // Si spawnArcDegrees = 360, c'est l'équivalent d'un tirage tout autour.
        float halfArc = spawnArcDegrees * 0.5f;

        // Cherche un angle qui ne soit pas trop proche des autres.
        // Abandonne après 20 essais et accepte un angle aléatoire pour ne pas boucler.
        for (int i = 0; i < 20; i++)
        {
            float angle = baseYaw + Random.Range(-halfArc, halfArc);
            bool conflit = false;
            foreach (float autre in anglesUtilises)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angle, autre));
                if (diff < spawnAngleMin)
                {
                    conflit = true;
                    break;
                }
            }
            if (!conflit) return angle;
        }
        return baseYaw + Random.Range(-halfArc, halfArc);
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
        if (interfaceJeuInfo != null) {
            interfaceJeuInfo.text =
                $"temps restant: {chrono.GetFormatString()}\n" +
                $"Manche {mancheCourante + 1}/{listesDeManches.Count}\n" +
                $"Cibles: {ciblesActuelles.Count}";
        }
        if (recordManchesText != null)
            recordManchesText.text = $"Record de manches réussies: {recordManches}";

        if (textPartiePerdue != null)
            textPartiePerdue.text = $"Partie perdue!\nVous avez atteint la manche {mancheCourante + 1}";
    }
}
