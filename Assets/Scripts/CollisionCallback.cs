using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public class CollisionCallback : MonoBehaviour
{
    // Note: Unity ne sérialise pas les Dictionary, donc pas de [SerializeField] ici.
    private Dictionary<GameObject, UnityEvent> callbacks = new();

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision d�tect�e avec {collision.gameObject.name} sur {gameObject.name}");
        GameObject obj = collision.gameObject;

        if (callbacks.ContainsKey(obj))
            callbacks[obj]?.Invoke();
    }

    public void RegisterCallback(GameObject other, Action callback)
    {
        if (!callbacks.ContainsKey(other))
            callbacks[other] = new UnityEvent();
        callbacks[other].AddListener(callback.Invoke);
    }
}
