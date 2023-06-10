using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchMiner : MonoBehaviour
{
    private Camera cam;

    public MarchRegion region;

    #region Cubes

    private void CreateCubes()
    {
        
        int index;
        int length = 3;
        cube0 = new Vector3Int[length * length * length];
        Vector3Int offset = new Vector3Int(-1, -1, -1);

        for (int x = 0; x < length; x++)
        {
            for (int y = 0; y < length; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    index = x + y * length + z * length * length;

                    cube0[index] = new Vector3Int(x, y, z) + offset;
                }
            }
        }

        length = 5;
        cube1 = new Vector3Int[length * length * length];        
        offset = new Vector3Int(-2, -2, -2);

        for (int x = 0; x < length; x++)
        {
            for (int y = 0; y < length; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    index = x + y * length + z * length * length;

                    cube1[index] = new Vector3Int(x, y, z) + offset;
                }
            }
        }

        float radius = 3;
        float dist;

        length = 7;
        List<Vector3Int> sphere = new List<Vector3Int>();
        offset = new Vector3Int(-3, -3, -3);
        Vector3Int pos;

        for (int x = 0; x < length; x++)
        {
            for (int y = 0; y < length; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    pos = new Vector3Int(x, y, z) + offset;

                    dist = Vector3Int.Distance(pos, offset);

                    if(dist <= radius)
                    {
                        sphere.Add(pos);
                    }
                }
            }
        }

        sphere0 = sphere.ToArray();
    }

    public static Vector3Int[] cube0;
    public static Vector3Int[] cube1;

    public static Vector3Int[] sphere0;

    #endregion

    private float timeSinceLastMine = 0;

    private void Start()
    {
        cam = Camera.main;

        CreateCubes();

        timeSinceLastMine = Time.time;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Mouse0))
        {
            Mine(false);
        }
        else if (Input.GetKey(KeyCode.Mouse1))
        {
            Mine();
        }
    }

    private void Mine(bool remove = true)
    {
        if(Time.time < timeSinceLastMine + 0.1f)
        {
            return;
        }

        timeSinceLastMine = Time.time;

        var ray = cam.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            var mesh = hitInfo.collider.GetComponent<MarchingCubeMesh>();
            if (mesh != null)
            {
                Vector3Int pos = mesh.GetLocalVoxelPos(hitInfo.point);

                //print("Hit mesh at " + pos.ToString());

                int length = cube1.Length;
                float[] values = new float[length];
                float value = remove ? 0.5f : -0.5f;

                for (int i = 0; i < length; i++)
                {
                    values[i] = value;
                }

                mesh.ModifyDenisty(sphere0, pos, values);
                mesh.UpdateMesh();
            }
        }
    }
}
