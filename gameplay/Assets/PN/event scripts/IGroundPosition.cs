// IGroundPosition.cs
//
// Pass 7 (jump-aware zones): ADDED. A one-line "contract" any script can sign to say
// "here is where my feet are on the floor, ignoring any jump in progress."
//
// WHY AN INTERFACE rather than InterceptZone reading PlayerControl4 directly:
// - The zone does not need to know HOW jumping works (jumpYOffset, coroutines, etc.).
//   It only needs a point on the floor. That knowledge stays inside the jumper (encapsulation).
// - Anything can sign it later - an enemy that jumps, a thrown object - and every zone
//   works with it unchanged (polymorphism / SOLID "depend on abstractions").
// - The jump math lives in ONE place (the class that owns the jump) instead of being
//   copied into InterceptZone, AnimationZone, and whatever comes next (DRY).
// Microsoft C# - interfaces: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces
// Microsoft C# - interface properties: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/interface-properties

using UnityEngine;

public interface IGroundPosition
{
    // Read-only: zones may ask, never set.
    Vector2 GroundPosition { get; }
}