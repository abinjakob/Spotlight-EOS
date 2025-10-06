using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LSL;
using System.Threading.Tasks;

public class LSLMarkerStream : MonoBehaviour
{
    // declare LSL outlet and stream info
    private StreamOutlet outlet;
    private StreamInfo streamInfo;
    private bool isStreaming = false;

    // start is called before the first frame update
    void Start()
    {
        // create a new LSL stream info
        streamInfo = new StreamInfo("EventMarkers", "Markers", 1, LSL.LSL.IRREGULAR_RATE, channel_format_t.cf_string);
        // create LSL outlet
        outlet = new StreamOutlet(streamInfo);
        isStreaming = true; 
    }

    // Method to write markers to LSL outlet
    public async void Write(string marker)
    {
        if (!isStreaming)
        {
            Debug.LogWarning("LSL streaming is not active.");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                // create array to hold marker
                string[] sample = new string[1];
                sample[0] = marker;

                // push sample to outlet
                outlet.push_sample(sample);
                Debug.Log($"Marker sent: {marker}"); 
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to push sample to LSL: {ex.Message}");
        }
    }

    // cleanup on destroy
    private void OnDestroy()
    {
        isStreaming = false; 
        if (outlet != null)
        {
            outlet.Dispose();
            outlet = null;
        }
    }

    // Optionally, a method to start/stop streaming
    public void StartStreaming()
    {
        if (!isStreaming)
        {
            isStreaming = true;
            Debug.Log("Started LSL streaming.");
        }
    }

    public void StopStreaming()
    {
        isStreaming = false;
        Debug.Log("Stopped LSL streaming.");
    }
}
