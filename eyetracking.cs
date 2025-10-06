using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MixedReality.Toolkit.Input;
using System.Diagnostics;
using System;
using Network;


public class eyetrack : MonoBehaviour
{
    [SerializeField]
    private GazeInteractor gazeInteractor;

    // to write tracking data 
    private StreamWriter trackerdata;
    public ConnectionManager connectionManager;

    private void Awake()
    {
        // file to save eyetracking data
        var filepath = Path.Combine(Application.persistentDataPath, "eyetrackerdata.csv");
        // initialise StreamWrite to write to CSV file
        trackerdata = new StreamWriter(filepath);
        // ensuring dat ais written immediately 
        trackerdata.AutoFlush = true;
        UnityEngine.Debug.Log(filepath);

        connectionManager = FindObjectOfType<ConnectionManager>();
    }

    private void Update()
    {
        // get current gaze direction
        Vector3 gazeDirection = gazeInteractor.rayOriginTransform.forward;

        // log the gaze direction
        WriteTrackingPoint(gazeDirection);

    }

    // method to write to CSV
    private void WriteTrackingPoint(Vector3 gazeDirection)
    {
        // create eye tracking data
        string eyeData = FormattableString.Invariant($"{Time.time}, {gazeDirection.x}, {gazeDirection.y}, {gazeDirection.z}");
        // writes eye tracking data to localfile
        trackerdata.WriteLine(eyeData);

        // send eye tracking data via network
        if (connectionManager != null)
        {
            connectionManager.Send(eyeData, "EyeStream");
        }


    }

    // called when script instance is destroyed to ensure
    // all data is flushed to file and is properly closed 
    private void OnDestroy()
    {
        trackerdata.Close();
    }
}