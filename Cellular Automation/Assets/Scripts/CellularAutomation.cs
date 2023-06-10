using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellularAutomation : MonoBehaviour
{
    public int width;
    public int height;

    public string seed;

    //Values between 0 and 100
    private int[,] cells;

    public Color emptyColor;
    public Color fillColor;

    private Color[] colors;

    private int[,] cellChange;

    public float multipler = 0.5f;

    private float timeSinceLastSimulation = 0;
    public float simulationSpeed = 1;

    public int simulationAmount = 10;
    public int simulationMin = 10;

    public bool emission = true;
    public bool evaporation = true;

    public int numberOfSims = 0;

    System.Random rng = new System.Random();

    private void Start()
    {
        InitializeCells();
        InitializeColors();
        RandomFill(seed);
    }

    private void Update()
    {
        if (!Input.GetKey(KeyCode.Mouse0))
        {
            if(Time.time > timeSinceLastSimulation + simulationSpeed)
            {
                timeSinceLastSimulation = Time.time;
                Simulate();
            }
        }
    }

    private void InitializeColors()
    {
        colors = new Color[10];
        colors[0] = Color.clear;

        for (int i = 0; i < 10; i++)
        {
            colors[i] = Color.Lerp(emptyColor, fillColor, i / 10f);
        }
    }

    private void InitializeCells()
    {
        cells = new int[(width > 0) ? width : 1, (height > 0) ? height : 1];
        cellChange = new int[(width > 0) ? width : 1, (height > 0) ? height : 1];
    }

    private void RandomFill(string seed)
    {
        System.Random rng = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = Mathf.RoundToInt(rng.Next(0, 100) * multipler);
                cells[x, y] = Mathf.Clamp(cells[x, y], 0, 99);
            }
        }
    }

    private bool InBounds(int x, int y)
    {
        return (x >= 0 && x < width && y >= 0 && y < height);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index">Index refres to which side of the cell. 0 - Top, 1 - Left, 2 - Bottom, 3 - Right</param>
    private int GetAdjacentCell(int x, int y, int index)
    {
        index %= 4;

        if (!InBounds(x, y))
        {
            return -1;
        }

        switch (index)
        {
            //Top
            default:
            case 0:
                if (InBounds(x, y + 1))
                {
                    return cells[x, y + 1];
                }
                break;
            //Left
            case 1:
                if (InBounds(x - 1, y))
                {
                    return cells[x - 1, y];
                }
                break;
            //Bottom
            case 2:
                if (InBounds(x, y - 1))
                {
                    return cells[x, y - 1];
                }
                break;
            //Right
            case 3:
                if (InBounds(x + 1, y))
                {
                    return cells[x + 1, y];
                }
                break;
        }

        return -1;
    }

    private void Simulate()
    {
        
        int n = 0;
        int less = 0;//, more = 0;
        int offX = 0, offY = 0;
        int r = 0;

        rng = new System.Random(Time.time.ToString().GetHashCode() + seed.GetHashCode());
        r = rng.Next(0, width * height);

        int simIndex = numberOfSims % 4;

        //for (int x = 0; x < width; x++)
        //{
        //    for (int y = 0; y < height; y++)
        //    {

        //        for (int i = 0; i < 4; i++)
        //        {
        //            less = (simIndex + i) % 4;

        //            n = GetAdjacentCell(x, y, less);

        //            switch (less)
        //            {
        //                default:
        //                case 0:
        //                    offX = 0;
        //                    offY = 1;
        //                    break;
        //                case 1:
        //                    offX = -1;
        //                    offY = 0;
        //                    break;
        //                case 2:
        //                    offX = 0;
        //                    offY = -1;
        //                    break;
        //                case 3:
        //                    offX = 1;
        //                    offY = 0;
        //                    break;
        //            }

        //            if (n < 0)
        //            {
        //                continue;
        //            }

        //            if (cells[x, y] > n && cells[x, y] > simulationMin)// && n + simulationAmount < 100)
        //            {
        //                cells[x, y] -= simulationAmount;
        //                cells[x + offX, y + offY] += simulationAmount;
        //            }
        //        }
        //    }
        //}

        switch (simIndex)
        {
            default:
            case 3:
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        r = rng.Next(0, 4);

                        for (int i = 0; i < 4; i++)
                        {
                            less = (r + i) % 4;

                            n = GetAdjacentCell(x, y, less);

                            switch (less)
                            {
                                default:
                                case 0:
                                    offX = 0;
                                    offY = 1;
                                    break;
                                case 1:
                                    offX = -1;
                                    offY = 0;
                                    break;
                                case 2:
                                    offX = 0;
                                    offY = -1;
                                    break;
                                case 3:
                                    offX = 1;
                                    offY = 0;
                                    break;
                            }

                            if (n < 0)
                            {
                                continue;
                            }

                            if (cells[x, y] > n && cells[x, y] > simulationMin)// && n + simulationAmount < 100)
                            {
                                cells[x, y] -= simulationAmount;
                                cells[x + offX, y + offY] += simulationAmount;
                            }
                        }
                    }
                }
                break;
            case 1:
                for (int x = width - 1; x >= 0; x--)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        r = rng.Next(0, 4);

                        for (int i = 0; i < 4; i++)
                        {
                            less = (r + i) % 4;

                            n = GetAdjacentCell(x, y, less);

                            switch (less)
                            {
                                default:
                                case 0:
                                    offX = 0;
                                    offY = 1;
                                    break;
                                case 1:
                                    offX = -1;
                                    offY = 0;
                                    break;
                                case 2:
                                    offX = 0;
                                    offY = -1;
                                    break;
                                case 3:
                                    offX = 1;
                                    offY = 0;
                                    break;
                            }

                            if (n < 0)
                            {
                                continue;
                            }

                            if (cells[x, y] > n && cells[x, y] > simulationMin)// && n + simulationAmount < 100)
                            {
                                cells[x, y] -= simulationAmount;
                                cells[x + offX, y + offY] += simulationAmount;
                            }
                        }
                    }


                }
                break;
            case 2:
                for (int x = width - 1; x >= 0; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        r = rng.Next(0, 4);

                        for (int i = 0; i < 4; i++)
                        {
                            less = (r + i) % 4;

                            n = GetAdjacentCell(x, y, less);

                            switch (less)
                            {
                                default:
                                case 0:
                                    offX = 0;
                                    offY = 1;
                                    break;
                                case 1:
                                    offX = -1;
                                    offY = 0;
                                    break;
                                case 2:
                                    offX = 0;
                                    offY = -1;
                                    break;
                                case 3:
                                    offX = 1;
                                    offY = 0;
                                    break;
                            }

                            if (n < 0)
                            {
                                continue;
                            }

                            if (cells[x, y] > n && cells[x, y] > simulationMin)// && n + simulationAmount < 100)
                            {
                                cells[x, y] -= simulationAmount;
                                cells[x + offX, y + offY] += simulationAmount;
                            }
                        }
                    }
                }
                break;
            case 0:
                for (int x = 0; x < width; x++)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        r = rng.Next(0, 4);

                        for (int i = 0; i < 4; i++)
                        {
                            less = (r + i) % 4;

                            //less = less == 0 ? 3 : (less == 3 ? 0 : less);

                            n = GetAdjacentCell(x, y, less);

                            switch (less)
                            {
                                default:
                                case 0:
                                    offX = 0;
                                    offY = 1;
                                    break;
                                case 1:
                                    offX = -1;
                                    offY = 0;
                                    break;
                                case 2:
                                    offX = 0;
                                    offY = -1;
                                    break;
                                case 3:
                                    offX = 1;
                                    offY = 0;
                                    break;
                            }

                            if (n < 0)
                            {
                                continue;
                            }

                            if (cells[x, y] > n && cells[x, y] > simulationMin)// && n + simulationAmount < 100)
                            {
                                cells[x, y] -= simulationAmount;
                                cells[x + offX, y + offY] += simulationAmount;
                            }
                        }
                    }
                }
                break;
        }

        if (emission)
        {
            int mapCenterX = width / 2, mapCenterY = height / 2;

            for (int x = mapCenterX; x < mapCenterX + 3; x++)
            {
                for (int y = mapCenterY; y < mapCenterY + 3; y++)
                {
                    cells[x, y] += simulationMin;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                //cells[x, y] += cellChange[x, y];

                cells[x, y] = Mathf.Clamp(cells[x, y], 0, 99);

                if (evaporation && cells[x, y] < 20 && cells[x, y] > 0)
                {
                    cells[x, y] -= 1;
                }
            }
        }

        numberOfSims++;
    }

    private void OnDrawGizmos()
    {
        Vector3 scale = new Vector3(0.1f, 0.1f, 0);
        Vector3 offset = new Vector3(scale.x / 2, scale.y / 2, 0);

        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new Vector3(width / 2f * scale.x, height / 2f * scale.y) + transform.position, new Vector3(width * scale.x, height * scale.y));

        if (cells == null)
        {
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(cells[x, y] == 0)
                {
                    continue;
                }

                Vector3 boxScale = new Vector3(scale.x, scale.y, cells[x, y] / 100f * 1);
                Gizmos.color = colors[cells[x, y] / 10];
                Gizmos.DrawCube(new Vector3(x * scale.x, y * scale.y, 0) + offset + new Vector3(0, 0, -boxScale.z / 2f) + transform.position, boxScale);
            }
        }
    }
}
