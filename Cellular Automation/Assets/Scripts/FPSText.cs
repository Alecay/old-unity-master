using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSText : MonoBehaviour
{
    public Text text;


    private int[] fpsHistory = new int[120];
    private int index;
    private int lowestFPS = 120;

    private int FPSThisFrame()
    {
        return (int)(1.0f / Time.smoothDeltaTime);
    }

    private void Start()
    {
        fpsHistory = new int[120];
        index = 0;
        lowestFPS = 120;

        text = GetComponent<Text>();
    }

    private void Update()
    {
        int fpsTF = FPSThisFrame();


        fpsHistory[index] = fpsTF;

        int average = 0;

        for (int i = 0; i < 120; i++)
        {
            average += fpsHistory[i];
        }

        average /= 120;

        index++;

        if(index >= 120)
        {
            index = 0;
        }

        if(fpsTF < lowestFPS && Time.realtimeSinceStartup > 0 && fpsTF > 0)
        {
            lowestFPS = fpsTF;
        }

        if(fpsTF < 30 && Time.realtimeSinceStartup > 10)
        {
            //Debug.Log("FPS dipped below 30");            
        }

        if(Time.realtimeSinceStartup > 10)
        {
            text.text = "FPS: " + fpsTF + "\nAverage FPS: " + average + "\nLowest FPS: " + lowestFPS;
        }
        
    }
}
