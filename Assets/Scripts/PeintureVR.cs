using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PeintureVR : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Transform originRayonDroit;
    [SerializeField] private Transform originRayonGauche;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference actionGachetteDroite;
    [SerializeField] private InputActionReference actionGachetteGauche;

    [Header("Paramètres")]
    [SerializeField] private float porteeRayon = 5f;

    private Color couleurActive = Color.red;

    void OnEnable()
    {
        actionGachetteDroite.action.performed += OnGachetteDroite;
        actionGachetteGauche.action.performed += OnGachetteGauche;
    }

    void OnDisable()
    {
        actionGachetteDroite.action.performed -= OnGachetteDroite;
        actionGachetteGauche.action.performed -= OnGachetteGauche;
    }

    private void OnGachetteDroite(InputAction.CallbackContext context)
    {
        Ray rayon = new Ray(originRayonDroit.position, originRayonDroit.forward);
        RaycastHit hit;

        if (Physics.Raycast(rayon, out hit, porteeRayon)) {
            if (hit.collider.CompareTag("Canvas")) {
                PlacerCube(hit.point, hit.normal);
            }
        }
    }

    private void OnGachetteGauche(InputAction.CallbackContext context)
    {
        Ray rayon = new Ray(originRayonGauche.position, originRayonGauche.forward);
        RaycastHit hit;

        if (Physics.Raycast(rayon, out hit, porteeRayon)) {
            if (hit.collider.CompareTag("CubePeint")) {
                Destroy(hit.collider.gameObject);
            }
        }
    }

    private void PlacerCube(Vector3 point, Vector3 normale)
    {
        Vector3 position = point + normale * 0.08f;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, -normale);

        GameObject cube = Instantiate(cubePrefab, position, rotation);
        cube.GetComponent<Renderer>().material.color = couleurActive;
        cube.tag = "CubePeint";
    }

    public void ChangerCouleur(Color nouvelleCouleur)
    {
        couleurActive = nouvelleCouleur;
    }

    void Update()
    {
        Debug.DrawRay(originRayonDroit.position,
                      originRayonDroit.forward * porteeRayon,
                      couleurActive);
    }
}