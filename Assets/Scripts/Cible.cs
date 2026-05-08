using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Cible : MonoBehaviour
{
    private GameManager jeu;

    [SerializeField, Tooltip("Le son de quand on détruit la cible'")]
    private AudioClip sonMort;

    private AudioSource audioSource;

    private Collider collision;

    void Awake()
    {
        collision = GetComponent<Collider>();
    }

    // collision
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out Marteau marteau)) {
            Debug.Log("Cible détruite !");
            if(sonMort) marteau.JouerSon(sonMort);
            jeu.OnFrapperCible(this);
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
    }
}
