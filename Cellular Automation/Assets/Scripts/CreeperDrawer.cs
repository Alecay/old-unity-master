using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreeperDrawer : MonoBehaviour
{
    public Mesh mesh;
    public Material material;
    public Material terrainMaterial;
    public SmokeSim sim;

    public CreeperSimulation simulator;



    private void Update()
    {
        Draw();
    }

    //private void Draw()
    //{
    //    Vector3 position = transform.position;
    //    Quaternion rotation = Quaternion.identity;

    //    Vector3 scale = new Vector3(0.5f, 1, 0.5f);

    //    Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
    //    float heightScale = 0.5f;
    //    float density = 0f;
    //    float tHeight = 0f;

    //    if (sim.drawTerrain)
    //    {
    //        position = new Vector3((0.5f * sim.width) / 2f, -heightScale, (0.5f * sim.height) / 2f);
    //        scale = new Vector3(0.5f * sim.width, heightScale, 0.5f * sim.height);
    //        matrix = Matrix4x4.TRS(position, rotation, scale);

    //        Graphics.DrawMesh(mesh, matrix, terrainMaterial, 2);
    //    }


    //    for (int x = 0; x < sim.width; x++)
    //    {
    //        for (int y = 0; y < sim.height; y++)
    //        {
    //            tHeight = sim.cells[x + y * sim.width].height;
    //            if (tHeight < 1)
    //            {
    //                continue;
    //            }

    //            position = new Vector3(0.5f * x + 0.5f, tHeight * heightScale / 2f, 0.5f * y + 0.5f);
    //            scale = new Vector3(0.5f, tHeight * heightScale, 0.5f);
    //            matrix = Matrix4x4.TRS(position, rotation, scale);

    //            Graphics.DrawMesh(mesh, matrix, terrainMaterial, 0);
    //        }
    //    }

    //    for (int x = 0; x < sim.width; x++)
    //    {
    //        for (int y = 0; y < sim.height; y++)
    //        {
    //            density = sim.useCellAverage ? sim.GetAverageDensity(x, y) : sim.cells[x + y * sim.width].density;

    //            if (density < 1)
    //            {
    //                continue;
    //            }
    //            tHeight = sim.cells[x + y * sim.width].height;

    //            position = new Vector3(0.5f * x + 0.5f, density * heightScale / 2f, 0.5f * y + 0.5f);
    //            position += new Vector3(0, tHeight * heightScale, 0);
    //            scale = new Vector3(0.5f, density * heightScale, 0.5f);
    //            matrix = Matrix4x4.TRS(position, rotation, scale);

    //            Graphics.DrawMesh(mesh, matrix, material, 1);
    //        }
    //    }
    //}

    private void Draw()
    {
        Vector3 position = transform.position;
        Quaternion rotation = Quaternion.identity;

        Vector3 scale = new Vector3(0.5f, 1, 0.5f);

        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
        float heightScale = 0.5f;
        float density = 0f;
        float tHeight = 0f;

        //if (simulator.drawTerrain)
        //{
        //    position = new Vector3((0.5f * sim.width) / 2f, -heightScale, (0.5f * sim.height) / 2f);
        //    scale = new Vector3(0.5f * sim.width, heightScale, 0.5f * sim.height);
        //    matrix = Matrix4x4.TRS(position, rotation, scale);

        //    Graphics.DrawMesh(mesh, matrix, terrainMaterial, 2);
        //}


        //for (int x = 0; x < sim.width; x++)
        //{
        //    for (int y = 0; y < sim.height; y++)
        //    {
        //        tHeight = sim.cells[x + y * sim.width].height;
        //        if (tHeight < 1)
        //        {
        //            continue;
        //        }

        //        position = new Vector3(0.5f * x + 0.5f, tHeight * heightScale / 2f, 0.5f * y + 0.5f);
        //        scale = new Vector3(0.5f, tHeight * heightScale, 0.5f);
        //        matrix = Matrix4x4.TRS(position, rotation, scale);

        //        Graphics.DrawMesh(mesh, matrix, terrainMaterial, 0);
        //    }
        //}

        if(simulator.denisty == null || simulator.denisty.Length < simulator.width * simulator.height)
        {
            return;
        }

        for (int x = 0; x < simulator.width; x++)
        {
            for (int y = 0; y < simulator.height; y++)
            {
                density = simulator.denisty[x + y * simulator.width];

                if (density < 1)
                {
                    continue;
                }

                tHeight = 0;// sim.cells[x + y * sim.width].height;

                position = new Vector3(0.5f * x + 0.5f, density * heightScale / 2f, 0.5f * y + 0.5f);
                position += new Vector3(0, tHeight * heightScale, 0);
                scale = new Vector3(0.5f, density * heightScale, 0.5f);
                matrix = Matrix4x4.TRS(position, rotation, scale);

                Graphics.DrawMesh(mesh, matrix, material, 1);
            }
        }
    }
}
