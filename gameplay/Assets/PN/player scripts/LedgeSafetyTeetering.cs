/*
made to work with PlayerControl script 
goal is to make game more forgiving about platforming due to weird orthographic perspective of the hand-drawn graphics

works like the allow walk off bool. except if the player walks off a ledge the game graciously stops them for a second before they fall off
active during sprinting and walking
*/

using UnityEngine;

public class LedgeSafetyTeetering : MonoBehaviour
{
    private string TeeterTime = "walk";

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
