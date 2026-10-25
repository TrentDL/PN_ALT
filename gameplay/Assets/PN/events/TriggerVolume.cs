using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Fires an event when the player enters the trigger volume.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerVolume : MonoBehaviour
{
    [SerializeField] private UnityEvent playerEnteredTriggerVolume;
    [SerializeField] private bool canBeTriggeredMultipleTimes = false;

    private bool triggered;
    
    // called in edit mode when this component is first added to a gameobject.
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered && !canBeTriggeredMultipleTimes) return;

        triggered = true;
        playerEnteredTriggerVolume?.Invoke();
    }
}