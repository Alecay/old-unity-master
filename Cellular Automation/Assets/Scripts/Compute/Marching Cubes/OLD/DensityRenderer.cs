using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityRenderer : MonoBehaviour
{
    [Range(-1, 1)]
    public float isoLevel = 0;
    public DensityGenerator densityGenerator;

    public Vector4[] points;
    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            points = new Vector4[densityGenerator.noiseData.chunkSize * densityGenerator.noiseData.chunkSize];
            densityGenerator.densityValuesBuffer.GetData(points);
        }
    }

    private void Start()
    {
        points = new Vector4[densityGenerator.noiseData.chunkSize * densityGenerator.noiseData.chunkSize];
        densityGenerator.densityValuesBuffer.GetData(points);
    }

    private void OnDrawGizmosSelected()
    {
        if(densityGenerator && densityGenerator.densityValuesBuffer != null)
        {
            Color c;
            int drawnPoints = 0;
            Vector3 pos;

            for (int i = 0; i < points.Length && drawnPoints < 5000; i++)
            {

                if (points[i].w < isoLevel)
                {
                    continue;
                }
                c = Color.Lerp(Color.black, Color.white, points[i].w);
                Gizmos.color = c;

                drawnPoints++;
                pos = new Vector3(points[i].x, points[i].y, points[i].z) + transform.position;
                Gizmos.DrawSphere(pos, 0.25f);
            }
        }
    }
}
