using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class flickerControl : MonoBehaviour
{
    // shader property name for controlling flicker frequency
    readonly string freqPropertyName = "_FlickerFrequnecy";
    // renderer component of GameObject
    Renderer rend;
    
    // flickering frequency (Hz)
    public float flickerFrequency = 15f;
    // duration of single trial
    public float trialDuration = 60f;

    private bool isFlickering = false;
    private float flickerInterval;

    void Awake()
    {
        // get renderer 
        rend = GetComponent<Renderer>();
        // defining interval for flickering
        flickerInterval = 1f / (flickerFrequency * 2f);

        // enable shader keyword
        rend.material.EnableKeyword(freqPropertyName);
        // setting initial frequency ZERO
        rend.material.SetFloat(freqPropertyName, 0);
        // calling start flickering
        StartFlickering();
    }

    // trigger flickering
    public void StartFlickering()
    {
        // Prevent starting multiple flickering coroutines
        if (!isFlickering)
            // start flickering coroutine
            StartCoroutine(FlickerBox());
    }

    // flicker logic
    IEnumerator FlickerBox()
    {
        isFlickering=true;
        // get start time
        float startTime = Time.time;
        // run flicker for trial duration
        while (Time.time-startTime < trialDuration)
        {
            // turn on flicker
            rend.material.SetFloat(freqPropertyName, flickerFrequency);
            // wait for full cycle
            yield return new WaitForSeconds(flickerInterval * 2f);
            // turn off flicker
            rend.material.SetFloat(freqPropertyName, 0);
        }

        // ensuring flicker ends in OFF state
        rend.material.SetFloat(freqPropertyName, 0);
        isFlickering = false;
    }
}


