using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Cible : MonoBehaviour
{
    protected GameManager jeu;

    [SerializeField, Tooltip("Le son de quand on détruit la cible'")]
    private AudioClip sonMort;

    private Collider collision;

    protected virtual void Awake()
    {
        collision = GetComponent<Collider>();
    }

    protected virtual void Start()
    {
        jeu = GameManager.instance;
        if (jeu == null)
        {
            jeu = FindObjectOfType<GameManager>();
        }
    }

    // collision
    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.TryGetComponent(out Marteau marteau))
            return;

        Debug.Log("Cible détruite !");
        if (sonMort) marteau.JouerSon(sonMort);

        OnCibleFrappee(marteau);

        jeu.OnFrapperCible(this);

        gameObject.SetActive(false);
    }

    protected virtual void OnCibleFrappee(Marteau marteau)
    {
    }

    private void Update()
    {
    }
}
