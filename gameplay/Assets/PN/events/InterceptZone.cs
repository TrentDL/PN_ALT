// InterceptZone.cs
//
// ADDED: sibling of AnimationZone. Same polygon-entry logic, but drives
// CubismRenderingInterceptController.SortingOrder instead of a Cubism art mesh's
// LocalSortingOrder.
//
// WHY A SEPARATE SCRIPT rather than one widened array: the two numbers live on
// different scales. LocalSortingOrder is small and model-local; the interceptor's
// SortingOrder is compared against the model's global draw-pass order (authored at
// 1509 in this project). One shared sortOrderInside cannot be correct for both, so
// a single component would need two target fields and a type test in every loop.
// Two components, one number each, no branching - each script has one reason to change.
// Pass 3 (Live2D removal): now drives a Unity SortingGroup (whole character) instead of
// the Cubism interceptor. AnimationZone now drives individual Renderers.
// Pass 5 (SpriteRenderer target): now drives Renderer.sortingOrder (the "Order in Layer"
// box on a Sprite Renderer) instead of a SortingGroup.
// Pass 6 (fade suppression): ADDED the read-only IsApplied property, so PlayerVisibility
// can ask "has this zone pushed my sprite behind the player right now?" without
// duplicating the polygon test. Nothing about how the zone sorts has changed.

using UnityEngine;
// Pass 3 (Live2D removal): replaced the Cubism interceptor namespace with Unity's own.
// Why: SortingGroup lives in UnityEngine.Rendering. It's built into Unity, so no package is needed.
// Pass 5 (SpriteRenderer target): removed "using UnityEngine.Rendering;" - Renderer lives in
// UnityEngine, so that extra line is no longer needed.

[RequireComponent(typeof(PolygonCollider2D))]
public class InterceptZone : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Whose position is tested. Usually the player root.")]
    public Transform watched;

    [Tooltip("Objects with a Sprite Renderer (any Renderer). Dropping in the GameObject is fine - the component is found on it or its children.")]
    // Pass 3 (Live2D removal): tooltip text updated to name SortingGroup.
    // Pass 5 (SpriteRenderer target): tooltip text updated to name Sprite Renderer. Field name kept
    // as "interceptors" so anything already dragged into this slot in the Inspector is not lost.
    public Component[] interceptors;

    [Tooltip("Order in Layer to apply while inside. Same scale as the Sprite Renderer's Order in Layer.")]
    // Pass 3 (Live2D removal): tooltip text updated. The old 1509 was a Live2D-authored value.
    // Pass 5 (SpriteRenderer target): tooltip text updated. Set the real value in the Inspector.
    public int sortOrderInside = 1509;

    [Header("Behaviour")]
    [Tooltip("ON: re-applies the order every frame while inside, so nothing else can steal it back.")]
    public bool holdWhileInside = true;

    [Tooltip("Seconds to hold the sorting order after the player exits. Cover the full fall duration.")]
    public float restoreDelay = 1f;

    [Header("Debug Visualization")]
    public bool showDebugGizmos = true;

    private PolygonCollider2D area;
    private bool wasInside = false;               // edge detection - act on ENTRY, not every frame
    private float exitTime = -1f;                 // when they left; negative means inside

    // ADDED: resolved once at startup so the per-frame loops never do a lookup or null check.
    private Renderer[] resolved;
    // Pass 3 (Live2D removal): CubismRenderingInterceptController[] -> SortingGroup[].
    // Pass 5 (SpriteRenderer target): SortingGroup[] -> Renderer[]. Renderer is the parent type of
    // SpriteRenderer, so a Sprite Renderer fits (polymorphism).
    private int[] originalOrders;                 // whatever was authored, captured not hardcoded

    // Pass 6 (fade suppression): ADDED. True from the first SetOrder until Restore puts the
    // authored order back - so it also stays true during restoreDelay, while the player is
    // still drawn in front.
    // WHY "get; private set;": anyone may READ it, only this script may CHANGE it
    // (encapsulation). Another script cannot switch the flag on by mistake.
    // WHY NOT reuse wasInside: wasInside goes false the moment the player steps out, but the
    // sorting order is held for restoreDelay longer. This tracks the renderers, not the player.
    // Microsoft C# - auto-implemented properties: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties
    // Microsoft C# - restricting accessor accessibility: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/restricting-accessor-accessibility
    public bool IsApplied { get; private set; }

    void Awake()
    {
        area = GetComponent<PolygonCollider2D>();

        // ADDED: two-pass fill into plain arrays. Bad entries are dropped here rather than
        // null-checked every frame in SetOrder. Arrays only - no extra namespace needed.
        Renderer[] temp = new Renderer[interceptors.Length];
        // Pass 3 (Live2D removal): type swap only; the logic is unchanged.
        // Pass 5 (SpriteRenderer target): type swap only; the logic is unchanged.
        int count = 0;

        for (int i = 0; i < interceptors.Length; i++)
        {
            Renderer c = Resolve(interceptors[i]);
            if (c == null)
            {
                Debug.LogWarning(name + ": interceptors[" + i + "] has no Renderer - skipped.", this);
                // Pass 3 (Live2D removal): warning text names SortingGroup.
                // Pass 5 (SpriteRenderer target): warning text names Renderer.
                continue;
            }
            temp[count] = c;
            count++;
        }

        // ADDED: only copy when something was skipped - normally temp is already the right size.
        if (count == temp.Length)
        {
            resolved = temp;
        }
        else
        {
            resolved = new Renderer[count];
            for (int i = 0; i < count; i++)
                resolved[i] = temp[i];
        }

        originalOrders = new int[resolved.Length];
        for (int i = 0; i < resolved.Length; i++)
            originalOrders[i] = resolved[i].sortingOrder;
        // Pass 3 (Live2D removal): SortingOrder -> sortingOrder (Unity's name for it).
    }// end of function >:D

    // ADDED: accepts the component itself, or finds it on that object or a child. Your
    // Inspector showed a Transform in the slot - dragging a GameObject assigns its Transform,
    // so without this the list would silently resolve to nothing.
    private static Renderer Resolve(Component c)
    // Pass 3 (Live2D removal): return type and lookups now use SortingGroup.
    // Pass 5 (SpriteRenderer target): return type and lookups now use Renderer.
    {
        if (c == null) return null;

        Renderer direct = c as Renderer;
        if (direct != null) return direct;

        return c.GetComponentInChildren<Renderer>();
    }// end of function >:D

    void Update()
    {
        bool inside = area.OverlapPoint(watched.position);

        if (inside && !wasInside)
        {
            SetOrder(sortOrderInside);
            exitTime = -1f;                       // cancel any pending restore
        }
        // ADDED: re-assert every frame while inside. Entry-only assignment is a one-shot write -
        // anything that changes SortingOrder afterwards wins permanently.
        else if (inside && holdWhileInside)
        {
            SetOrder(sortOrderInside);
        }
        else if (!inside && wasInside)
        {
            exitTime = Time.time;                 // start the clock instead of restoring now
        }

        // ADDED: the delayed restore. A timer rather than a coroutine so a re-entry cancels
        // it by simply resetting exitTime.
        if (exitTime >= 0f && Time.time - exitTime >= restoreDelay)
        {
            Restore();
            exitTime = -1f;
        }

        wasInside = inside;
    }// end of function >:D

    private void SetOrder(int order)
    {
        for (int i = 0; i < resolved.Length; i++)
            resolved[i].sortingOrder = order;
        // Pass 3 (Live2D removal): SortingOrder -> sortingOrder.

        IsApplied = true;
        // Pass 6 (fade suppression): ADDED. Set HERE rather than in Update so the flag can
        // only be true when the renderers really were changed - one writer, one place to look.
    }// end of function >:D

    private void Restore()
    {
        for (int i = 0; i < resolved.Length; i++)
            resolved[i].sortingOrder = originalOrders[i];
        // Pass 3 (Live2D removal): SortingOrder -> sortingOrder.

        IsApplied = false;
        // Pass 6 (fade suppression): ADDED. The authored order is back, so the sprite can
        // hide the player again - PlayerVisibility may fade it from here on.
    }// end of function >:D

    void OnDrawGizmos()
    {
        PolygonCollider2D poly = area != null ? area : GetComponent<PolygonCollider2D>();
        if (!showDebugGizmos || poly == null) return;

        // ADDED: cyan, so these read as distinct from AnimationZone's magenta at a glance.
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;

        for (int p = 0; p < poly.pathCount; p++)
        {
            Vector2[] path = poly.GetPath(p);
            for (int i = 0; i < path.Length; i++)
                Gizmos.DrawLine(path[i] + poly.offset, path[(i + 1) % path.Length] + poly.offset);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }// end of function >:D
}