using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the screen to or from black by changing a CanvasGroup's alpha.
/// It only fades. It knows nothing about scenes (single responsibility).
/// </summary>
// ADDED (pass 1): new script. Fading is its own job so it can be reused later
// for deaths, cutscenes, etc., not only scene changes.
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    // ADDED (pass 1): how long a full fade takes, in real seconds.
    [SerializeField] private float fadeDuration = 0.5f;

    // ADDED (pass 1): lets a freshly loaded scene start black and fade in,
    // so the transition looks the same on both sides.
    [SerializeField] private bool fadeInOnStart = true;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // ADDED (pass 1): Start can run as a coroutine when it returns IEnumerator.
    private IEnumerator Start()
    {
        if (!fadeInOnStart) yield break;

        canvasGroup.alpha = 1f;   // begin fully black
        yield return FadeTo(0f);  // then fade to clear
    }

    /// <summary>
    /// Moves the screen toward targetAlpha (0 = clear, 1 = black) over fadeDuration.
    /// </summary>
    // ADDED (pass 1): one method handles both directions (DRY), no separate FadeIn/FadeOut.
    public IEnumerator FadeTo(float targetAlpha)
    {
        canvasGroup.blocksRaycasts = true; // block clicks while fading

        while (canvasGroup.alpha != targetAlpha)
        {
            // Step size = time since last frame ÷ total fade time.
            // unscaledDeltaTime keeps the fade working even if the game is paused (timeScale = 0).
            float step = Time.unscaledDeltaTime / fadeDuration;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, step);
            yield return null; // wait one frame
        }

        canvasGroup.blocksRaycasts = targetAlpha > 0f; // only block clicks while not clear
    }
}