using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SelecteurCouleur : MonoBehaviour
{
    [SerializeField] private Color couleur = Color.red;
    [SerializeField] private PeintureVR gestionnaire;
    [SerializeField] private InputActionReference actionBoutonA;

    private XRSimpleInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable()
    {
        actionBoutonA.action.performed += OnBoutonA;
    }

    void OnDisable()
    {
        actionBoutonA.action.performed -= OnBoutonA;
    }

    private void OnBoutonA(InputAction.CallbackContext context)
    {
        // On change la couleur seulement si ce cube est pointé (hovered)
        if (interactable.isHovered) {
            gestionnaire.ChangerCouleur(couleur);
            Debug.Log($"Couleur changée : {couleur}");
        }
    }
}