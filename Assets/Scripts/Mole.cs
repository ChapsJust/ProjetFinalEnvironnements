using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Mole : MonoBehaviour
{
    private WhackAMole jeu;

    public WhackAMole Jeu {
        get => jeu;
        set => jeu = value;
    }

    [SerializeField, Tooltip("Le son de quand on frappe la taupe")]
    private AudioClip sonFrappe;

    private AudioSource audioSource;

    [SerializeField, Tooltip("Le temps que la taupe reste active")]
    private float tempsActivation = 2f;

    private float momentActivation;

    public bool Actif => gameObject.activeSelf;

    /// <summary>
    /// Collider de la taupe, utilisé pour détecter les coups du marteau
    /// </summary>
    private Collider collision;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        collision = GetComponent<Collider>();
    }

    // collision
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out Marteau marteau)) {
            Debug.Log("Taupe frappée !");
            marteau.JouerSon(sonFrappe);
            jeu.OnFrapperTaupe(this);
            gameObject.SetActive(false);
        }
    }

    public void Activer()
    {
        momentActivation = Time.time;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if(Actif && Time.time - momentActivation >= tempsActivation) {
            gameObject.SetActive(false);
        }
    }
}
