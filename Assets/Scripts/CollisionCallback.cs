using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public class CollisionCallback : MonoBehaviour
{
    [SerializeField, Tooltip("Les callbacks à appeler lors d'une collision")]
    private Dictionary<GameObject, UnityEvent> callbacks = new();

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision détectée avec {collision.gameObject.name} sur {gameObject.name}");
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
