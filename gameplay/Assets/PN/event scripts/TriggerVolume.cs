using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Fires an event when the player enters the trigger volume.
/// </summary>
// CHANGED (2D pass): Collider -> Collider2D. The player's GroundPoint uses a
// PolygonCollider2D, and Unity's 2D and 3D physics never detect each other.
// ADDED (2D pass): Rigidbody2D. Unity only sends trigger messages when at least one
// of the two objects has a Rigidbody2D, and the player has none. Putting it here
// means the player's movement code does not have to change.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class TriggerVolume : MonoBehaviour
{
    [SerializeField] private UnityEvent playerEnteredTriggerVolume;
    [SerializeField] private bool canBeTriggeredMultipleTimes = false;

    private bool triggered;
    
    // called in edit mode when this component is first added to a gameobject.
    private void Reset()
    {
        ConfigurePhysics(); // CHANGED (2D pass): was the isTrigger line; now shared with Awake (DRY)
    }

    private void Awake()
    {
        ConfigurePhysics(); // CHANGED (2D pass): was the isTrigger line; now shared with Reset (DRY)
    }

    // ADDED (2D pass): Reset and Awake did the same setup, and the Rigidbody2D line
    // would have doubled that. One method = one place to change it.
    private void ConfigurePhysics()
    {
        GetComponent<Collider2D>().isTrigger = true;

        // Kinematic = gravity and collisions never move it. A new Rigidbody2D is
        // Dynamic by default and would make the trigger fall out of the level.
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    private void OnTriggerEnter2D(Collider2D other) // CHANGED (2D pass): was OnTriggerEnter(Collider other)
    {
        /* Debug.Log($"[{name}] touched by {other.name} (tag: {other.tag}) on frame {Time.frameCount}", this); */

        if (!other.CompareTag("Player")) return;
        if (triggered && !canBeTriggeredMultipleTimes) return;
         /* Debug.Break(); */

        triggered = true;
        playerEnteredTriggerVolume?.Invoke();
    }
}