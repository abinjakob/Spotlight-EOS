using Network;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cueControl : MonoBehaviour
{
    // GameObjects
    public GameObject FixationCross;
    // target flcikers
    public GameObject Target_1;
    public GameObject Target_2;
    public GameObject Target_3;
    public GameObject Target_4;
    public GameObject Target_5;
    public GameObject Target_6;
    // target holder
    public GameObject Target_1_holder;
    public GameObject Target_2_holder;
    public GameObject Target_3_holder;
    public GameObject Target_4_holder;
    public GameObject Target_5_holder;
    public GameObject Target_6_holder;
    // target cues
    public GameObject Target_1_cue;
    public GameObject Target_2_cue;
    public GameObject Target_3_cue;
    public GameObject Target_4_cue;
    public GameObject Target_5_cue;
    public GameObject Target_6_cue;


    // experiment start delay
    public float startDelay = 5;

    // total number of trials
    public int trialcount = 10;

    // trial setup
    public string[] targets;
    public string[] trialSequence;
    public string currentTrial;

    // base ITI duration
    public float fixationPeriod = 1f;
    public float interTrialInterval = 3f;
    // ITI with a jitter
    private float iti;

    // reference from flickerControl 
    private float flickerPeriod;
    private flickerControl flickerControl1;
    private flickerControl flickerControl2;
    private flickerControl flickerControl3;
    private flickerControl flickerControl4;
    private flickerControl flickerControl5;
    private flickerControl flickerControl6;

    // connection manager
    public ConnectionManager connectionManager;

    // classifier output
    private int classifierEventCode;
    private float classifierX;
    private float classifierY;

    // target cue render matkerials for color changing
    private Renderer Target_1_cue_rend;
    private Renderer Target_2_cue_rend;
    private Renderer Target_3_cue_rend;
    private Renderer Target_4_cue_rend;
    private Renderer Target_5_cue_rend;
    private Renderer Target_6_cue_rend;
    private Color originalColor;



    void Awake()
    {
        // initialise all gameObjects inactive
        FixationCross.SetActive(false);
        Target_1.SetActive(false);
        Target_2.SetActive(false);
        Target_3.SetActive(false);
        Target_4.SetActive(false);
        Target_5.SetActive(false);
        Target_6.SetActive(false);
        Target_1_holder.SetActive(false);
        Target_2_holder.SetActive(false);
        Target_3_holder.SetActive(false);
        Target_4_holder.SetActive(false);
        Target_5_holder.SetActive(false);
        Target_6_holder.SetActive(false);
        Target_1_cue.SetActive(false);
        Target_2_cue.SetActive(false);
        Target_3_cue.SetActive(false);
        Target_4_cue.SetActive(false);
        Target_5_cue.SetActive(false);
        Target_6_cue.SetActive(false);

        // get reference from flickerControl script
        flickerControl1 = Target_1.GetComponent<flickerControl>();
        flickerControl2 = Target_2.GetComponent<flickerControl>();
        flickerControl3 = Target_3.GetComponent<flickerControl>();
        flickerControl4 = Target_4.GetComponent<flickerControl>();
        flickerControl5 = Target_5.GetComponent<flickerControl>();
        flickerControl6 = Target_6.GetComponent<flickerControl>();
        flickerPeriod = flickerControl1.trialDuration;

        // get renderer materials
        Target_1_cue_rend = Target_1_cue.GetComponent<Renderer>();
        Target_2_cue_rend = Target_2_cue.GetComponent<Renderer>();
        Target_3_cue_rend = Target_3_cue.GetComponent<Renderer>();
        Target_4_cue_rend = Target_4_cue.GetComponent<Renderer>();
        Target_5_cue_rend = Target_5_cue.GetComponent<Renderer>();
        Target_6_cue_rend = Target_6_cue.GetComponent<Renderer>();
        originalColor = Target_1_cue_rend.material.color;

        // connection manager
        connectionManager = FindObjectOfType<ConnectionManager>();

        // create trial sequence
        targets = new string[] { "1", "2", "3", "4", "5", "6" };
        trialSequence = new string[trialcount];
        for (int i = 0; i < trialcount; i++)
        {
            trialSequence[i] = targets[i % targets.Length]; 
        }

        // start experiment loop
        ShuffleArray(trialSequence);
        StartCoroutine(RunExperiment());
    }

    private void OnEnable()
    {
        ClassifierReceiver.OnClassifierEvent += HandleClassifierEvent;
    }

    private void OnDisable()
    {
        ClassifierReceiver.OnClassifierEvent -= HandleClassifierEvent;
    }

    private void HandleClassifierEvent(int eventCode, float x, float y)
    {
        classifierEventCode = eventCode;
        classifierX = x;
        classifierY = y;

        Debug.Log($"Classifier Event: Code={eventCode}, X={x}, Y={y}");
    }

    void ShuffleArray(string[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    //experiment loop coroutine
    IEnumerator RunExperiment()
    {
        yield return new WaitForSeconds(startDelay);
        // loop over trials 
        for (int i = 0; i < trialcount; i++)
        {
            currentTrial = trialSequence[i];
            // run trials
            yield return StartCoroutine(BeginTrial());
            // run iti with a jitter
            iti = interTrialInterval + Random.value;
            yield return new WaitForSeconds(iti);
        }
    }

    // function for timeout for listening to the classifier output
    private IEnumerator WaitForClassifierEvent(int expectedCode, float timeoutSeconds)
    {
        float startTime = Time.time;
        classifierEventCode = 0;
        classifierX = 0;

        while (classifierEventCode != expectedCode)
        {
            if (Time.time - startTime > timeoutSeconds)
            {
                Debug.LogWarning($"Timeout waiting for classifier event {expectedCode}. Skipping...");
                yield break; 
            }
            yield return null; 
        }

        Debug.Log($"Received classifier event {expectedCode}");
    }



    // single trial logic
    IEnumerator BeginTrial()
    {

        // show stimulus fixation 
        FixationCross.SetActive(true);
        yield return new WaitForSeconds(fixationPeriod);
        // hide fixation cross
        FixationCross.SetActive(false);

        Target_1_holder.SetActive(true);
        Target_2_holder.SetActive(true);
        Target_3_holder.SetActive(true);
        Target_4_holder.SetActive(true);
        Target_5_holder.SetActive(true);
        Target_6_holder.SetActive(true);

        if (currentTrial == "1")
        {
            yield return new WaitForSeconds(3);
            // starting trial cue
            Target_1_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_1", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_1");
            }
            else
                Debug.Log("Not Connected");

            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_1_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_1_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_1", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_1");
            }
            else
                Debug.Log("Not Connected");
            Target_1_holder.SetActive(false);
            Target_1.SetActive(true);
            flickerControl1.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_1.SetActive(false);
            Target_1_holder.SetActive(true);

            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_1_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_1_cue.SetActive(false);
            Target_1_cue_rend.material.color = originalColor;
            classifierEventCode = 0;

        }

        else if (currentTrial == "2")
        {
            yield return new WaitForSeconds(3f);
            // starting trial cue
            Target_2_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_2", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_2");
            }
            else
                Debug.Log("Not Connected");


            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_2_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_2_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_2", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_2");
            }
            else
                Debug.Log("Not Connected");
            Target_2_holder.SetActive(false);
            Target_2.SetActive(true);
            flickerControl2.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_2.SetActive(false);
            Target_2_holder.SetActive(true);

            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_2_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_2_cue.SetActive(false);
            Target_2_cue_rend.material.color = originalColor;
            classifierEventCode = 0;

        }

        else if (currentTrial == "3")
        {
            yield return new WaitForSeconds(3f);
            // starting trial cue
            Target_3_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_3", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_3");
            }
            else
                Debug.Log("Not Connected");


            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_3_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_3_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_3", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_3");
            }
            else
                Debug.Log("Not Connected");
            Target_3_holder.SetActive(false);
            Target_3.SetActive(true);
            flickerControl3.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_3.SetActive(false);
            Target_3_holder.SetActive(true);

            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_3_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_3_cue.SetActive(false);
            Target_3_cue_rend.material.color = originalColor;
            classifierEventCode = 0;
        }

        else if (currentTrial == "4")
        {
            yield return new WaitForSeconds(3f);
            // starting trial cue
            Target_4_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_4", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_4");
            }
            else
                Debug.Log("Not Connected");


            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_4_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_4_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_4", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_4");
            }
            else
                Debug.Log("Not Connected");
            Target_4_holder.SetActive(false);
            Target_4.SetActive(true);
            flickerControl4.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_4.SetActive(false);
            Target_4_holder.SetActive(true);

            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_4_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_4_cue.SetActive(false);
            Target_4_cue_rend.material.color = originalColor;
            classifierEventCode = 0;
        }

        else if (currentTrial == "5")
        {
            yield return new WaitForSeconds(3f);
            // starting trial cue
            Target_5_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_5", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_5");
            }
            else
                Debug.Log("Not Connected");


            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_5_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_5_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_5", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_5");
            }
            else
                Debug.Log("Not Connected");
            Target_5_holder.SetActive(false);
            Target_5.SetActive(true);
            flickerControl5.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_5.SetActive(false);
            Target_5_holder.SetActive(true);

            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_5_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_5_cue.SetActive(false);
            Target_5_cue_rend.material.color = originalColor;
            classifierEventCode = 0;
        }

        else if (currentTrial == "6")
        {
            yield return new WaitForSeconds(3f);
            // starting trial cue
            Target_6_cue.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (connectionManager != null)
            {
                connectionManager.Send("cue_6", "MarkerStream");
                Debug.Log("Sending marker: " + "cue_6");
            }
            else
                Debug.Log("Not Connected");


            // wait for classifier to send event 1
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_6_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(1f);
            Target_6_cue_rend.material.color = originalColor;
            if (connectionManager != null)
            {
                connectionManager.Send("flk_6", "MarkerStream");
                Debug.Log("Sending marker: " + "flk_6");
            }
            else
                Debug.Log("Not Connected");
            Target_6_holder.SetActive(false);
            Target_6.SetActive(true);
            flickerControl6.StartFlickering();
            yield return new WaitForSeconds(flickerPeriod);
            Target_6.SetActive(false);
            Target_6_holder.SetActive(true);


            // wait for classifier to send event 2
            classifierEventCode = 0;
            yield return StartCoroutine(WaitForClassifierEvent(1, 5f));
            Target_6_cue_rend.material.color = (classifierX != 0) ? Color.red : Color.green;
            yield return new WaitForSeconds(2f);
            Target_6_cue.SetActive(false);
            Target_6_cue_rend.material.color = originalColor;
            classifierEventCode = 0;
        }

        Target_1_holder.SetActive(false);
        Target_2_holder.SetActive(false);
        Target_3_holder.SetActive(false);
        Target_4_holder.SetActive(false);
        Target_5_holder.SetActive(false);
        Target_6_holder.SetActive(false);

    }
}