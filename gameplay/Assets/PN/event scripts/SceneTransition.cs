using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads a scene by name, fading out first if a ScreenFader is assigned.
/// Call LoadScene from any UnityEvent (e.g. TriggerVolume) in the Inspector.
/// </summary>
// ADDED (pass 1): new script. Loading scenes is a separate job from detecting
// the player (TriggerVolume) and from fading (ScreenFader).
public class SceneTransition : MonoBehaviour
{
    // ADDED (pass 1): optional. Leave empty for an instant cut with no fade.
    [SerializeField] private ScreenFader fader;

    // ADDED (pass 1): guard so two calls in a row can't start two loads.
    private bool isLoading;

    /// <summary>
    /// Public with one string parameter so it shows up in the UnityEvent dropdown
    /// and you can type the scene name right in the Inspector.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading) return;

        isLoading = true;
        StartCoroutine(FadeThenLoad(sceneName));
    }

    private IEnumerator FadeThenLoad(string sceneName)
    {
        // Unity objects are checked with != null, not ?. (Unity overrides ==).
        if (fader != null) yield return fader.FadeTo(1f);

        // The screen is already black, so a plain LoadScene is enough (KISS).
        // No async loading or progress bar is needed.
        SceneManager.LoadScene(sceneName);
    }
}