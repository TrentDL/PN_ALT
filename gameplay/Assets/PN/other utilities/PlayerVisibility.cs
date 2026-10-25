// PlayerVisibility.cs
//
// ADDED: fades this sprite while the player is drawn behind it, so the player
//      is never lost behind scenery. Add one to each sprite that can hide them.
//
// HOW "BEHIND" IS DECIDED: by the same rule the rest of the project sorts by -
//      world Y is depth (see TileStep's DEPTH note). A player whose FOOTPRINT is
//      higher up the screen than this sprite's base is further from the camera,
//      so they are drawn behind it.
//
// REJECTED: comparing the renderers' sortingOrder values instead. The Live2D
//      model and plain sprites sort on different scales (LocalSortingOrder vs the
//      global draw order), so comparing them directly is a latent bug.
//
// TWO PLAYER POINTS, ON PURPOSE:
//      playerRoot   - the footprint. Decides DEPTH (in front or behind).
//      playerVisual - where the body is DRAWN (lifted by elevation). Decides
//                     OVERLAP (is this sprite actually covering them).
//      One point for both breaks on any raised platform - the same footprint-vs-
//      visual lesson as wall3 and the roof window.
//
// Pass 6 (fade suppression): ADDED an optional InterceptZone link.
// WHY: the Y rule above cannot see an InterceptZone. While that zone holds this
//      sprite's order down, the sprite is drawn BEHIND the player even though the
//      player's Y says "further away" - so the Y rule faded a sprite that was not
//      covering anyone. The zone already knows whether it is active, so this script
//      ASKS it (IsApplied) rather than repeating the polygon test (DRY).
// REJECTED: giving this script its own PolygonCollider2D to test. Two shapes for one
//      area would have to be kept identical by hand, and would drift apart.
//
// Pass 7 (footprint depth): DEPTH now reads PlayerControl4.GroundPosition when the
//      player has one, instead of playerRoot's transform.
// WHY: PlayerControl4 LIFTS its root by (elevation + jumpYOffset). The root's Y is
//      therefore footprint + height, so a jump in front of scenery pushed the Y up
//      and counted as "further away" - a false fade. GroundPosition is the chalk mark
//      on the floor and never lifts, which is what "depth" means in this project.
// NOTE: playerVisual is UNCHANGED and should still be the lifted root - overlap is
//      about where the body is DRAWN, and with PlayerControl4 that is the root itself.
// FALLBACK: no PlayerControl4 on playerRoot (e.g. PlayerControl3) = the old
//      transform Y, so nothing else breaks.

using UnityEngine;

// RequireComponent: Unity adds a SpriteRenderer if one is missing, so the
// GetComponent in Awake can never come back empty.
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerVisibility : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("The player's root - the object PlayerControl3 is on. Its Y is the player's depth.")]
    public Transform playerRoot;

    [Tooltip("The player's visual root - the part lifted by elevation. Used to test whether this sprite covers the player.")]
    public Transform playerVisual;

    // The visual root sits at the feet. Testing a point a little higher (the
    // chest) means the fade starts when the BODY is covered, not just the shoes.
    [Tooltip("Offset from the visual root to the point tested for overlap, in world units. Raise Y to test higher up the body.")]
    public Vector2 overlapOffset = new Vector2(0f, 1f);

    [Header("Depth")]
    // Optional. Empty = this object's own position (its pivot). Set it when the
    // pivot is not where the object's base touches the floor.
    [Tooltip("Where this object's base meets the floor. Leave empty to use this object's own position.")]
    public Transform depthPoint;

    // Pass 6 (fade suppression): ADDED. Optional - leave empty and nothing changes.
    // Typed as InterceptZone (not Component) so the Inspector only accepts the right
    // script; dragging in the truck's ground object finds the zone on it automatically.
    [Header("Intercept")]
    [Tooltip("Optional. The InterceptZone that pushes this sprite behind the player. While it is active, this sprite never fades.")]
    public InterceptZone interceptZone;

    [Header("Fade")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of the sprite's normal opacity to keep while the player is behind it. 1 = no fade.")]
    public float hiddenAlpha = 0.4f;

    [Tooltip("How fast opacity changes, in alpha per second. Higher = snappier.")]
    public float fadeSpeed = 4f;

    private SpriteRenderer sprite;
    private float fullAlpha;   // the opacity the sprite was authored with, restored when clear

    // Pass 7 (footprint depth): ADDED. Found on playerRoot once in Awake, so no new
    // Inspector slot to wire and no lookup per frame. Null = not a PlayerControl4.
    private PlayerControl4 playerControl;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        fullAlpha = sprite.color.a;

        if (depthPoint == null) depthPoint = transform;

        if (playerRoot != null) playerControl = playerRoot.GetComponent<PlayerControl4>();
        // Pass 7 (footprint depth): ADDED. TryGetComponent would also work; GetComponent
        // matches the rest of the project.
    }// end of function >:D

    /// <summary>The player's depth: the footprint's Y, never the lifted body.</summary>
    // Pass 7 (footprint depth): ADDED. One small function so the "which Y is depth?"
    // decision lives in exactly one place (single responsibility).
    private float PlayerDepthY()
    {
        return (playerControl != null) ? playerControl.GroundPosition.y : playerRoot.position.y;
    }// end of function >:D

    void Update()
    {
        // Unassigned in the Inspector - do nothing rather than throw every frame.
        if (playerRoot == null || playerVisual == null) return;

        float target = PlayerIsHidden() ? fullAlpha * hiddenAlpha : fullAlpha;

        // Color is a struct (a value, not a reference): copy it, change the copy,
        // then assign it back. sprite.color.a = x on its own will not compile.
        Color c = sprite.color;

        // Already at the target - skip the write, so idle sprites cost almost nothing.
        if (Mathf.Approximately(c.a, target)) return;

        c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.deltaTime);
        sprite.color = c;
    }// end of function >:D

    /// <summary>True if the player is both further away than this sprite AND covered by it.</summary>
    private bool PlayerIsHidden()
    {
        // Pass 6 (fade suppression): ADDED. Checked FIRST because it overrides the Y rule:
        // if the zone has drawn this sprite behind the player, the player cannot be hidden
        // by it, whatever their Y says. Returning false sends the alpha back to full, so a
        // sprite that was mid-fade eases back in rather than snapping.
        if (interceptZone != null && interceptZone.IsApplied) return false;

        // Depth: higher Y = further from the camera = drawn behind.
        if (PlayerDepthY() <= depthPoint.position.y) return false;
        // Pass 7 (footprint depth): was playerRoot.position.y - the lifted root under
        // PlayerControl4. See the header note.

        // Overlap: is the tested point inside the rectangle this sprite is drawn in?
        // Only X and Y are compared - this is a 2D game, so Z plays no part.
        Vector2 p = (Vector2)playerVisual.position + overlapOffset;
        Bounds b = sprite.bounds;

        return p.x >= b.min.x && p.x <= b.max.x
            && p.y >= b.min.y && p.y <= b.max.y;
    }// end of function >:D
}