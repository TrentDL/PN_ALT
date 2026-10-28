// PlayerStart.cs
//
// ADDED (reset pass): a DEBUG tool. Put this on the PlayerStart object in the scene.
//   Press the reset key (R by default) and the player is sent back to this object's
//   position, standing on the ground, with any jump or fall cancelled.
//
// WHY it calls player.ResetTo() instead of moving the player itself:
//   PlayerControl4 redraws the player every frame from its own private footprint
//   (groundPosition). Moving player.transform from here would be undone one frame
//   later. Asking the player to move itself keeps that rule in one script.
//
// SETUP: drag the 'player' object into the Player slot in the Inspector, then place
//   this object ON THE FLOOR where the player should reappear (its Y is the
//   footprint, the same way the player's own starting spot is read in Start).

using UnityEngine;

// CHANGED (reset pass): now inherits MonoBehaviour. A plain class cannot be attached
// to a GameObject and never gets Update() called, so the key press was never read.
public class PlayerStart : MonoBehaviour
{
    [Tooltip("The player to send back here. Drag the 'player' object in.")]
    public PlayerControl4 player;

    [Tooltip("Key that sends the player back to this spot.")]
    public KeyCode resetKey = KeyCode.R;


    void Start()
    {
        // Same style of warning PlayerControl4 uses for a missing animator.
        if (player == null)
        {
            Debug.LogError("PlayerStart: player is not assigned. The reset key will do nothing.", this);
        }
    }//end of function >:D


    void Update()
    {
        if (player == null || !Input.GetKeyDown(resetKey)) return;

        // transform.position is a Vector3; ResetTo wants a Vector2. Unity converts it
        // automatically by dropping Z - Z does not matter for a 2D footprint.
        player.ResetTo(transform.position);
    }//end of function >:D
}
