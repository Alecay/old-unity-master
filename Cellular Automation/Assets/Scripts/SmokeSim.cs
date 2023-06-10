using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmokeSim : MonoBehaviour
{    
    void Start()
    {
        InitializeCells();

        for (int x = 30; x < 35; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width].height = 3;
            }
        }

        for (int x = 30; x < 70; x++)
        {
            for (int y = 30; y < 35; y++)
            {
                cells[x + y * width].height = 3;
            }
        }

        for (int x = 30; x < 70; x++)
        {
            for (int y = 65; y < 70; y++)
            {
                cells[x + y * width].height = 3;
            }
        }

        for (int x = 65; x < 70; x++)
        {
            for (int y = 30; y < 70; y++)
            {
                cells[x + y * width].height = 3;
            }
        }

        for (int x = 30; x < 35; x++)
        {
            for (int y = 45; y < 50; y++)
            {
                cells[x + y * width].height = 1;
            }
        }        
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            InitializeCells();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            emission = !emission;          
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            update = !update;
        }

        if (!update && Input.GetKeyDown(KeyCode.RightArrow))
        {
            UpdateCells();
        }

        if (update)
        {
            for (int i = 0; i < 5; i++)
            {
                UpdateCells();
            }
        }

        int max = 50;
        if (emission)
        {
            cells[47 + 47 * width].density = max;
            cells[48 + 47 * width].density = max;
            cells[47 + 48 * width].density = max;
            cells[48 + 48 * width].density = max;
        }
    }

    public bool update = true;
    public bool emission = true;
    public bool useCellAverage = false;
    public bool drawTerrain = false;

    public uint width = 100;
    public uint height = 100;

    [HideInInspector] public Cell[] cells;
    [HideInInspector] public Cell[] futureCells;

    public int numberOfSims = 0;
    public int densitySum = 0;

    public class Cell
    {
        public int height = 0;
        public int density = 0;
        public bool updated = false;        

        public Cell(int density)
        {
            this.density = density;
            updated = false;
        }
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private void InitializeCells()
    {
        cells = new Cell[width * height];
        futureCells = new Cell[width * height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width] = new Cell(0);
                futureCells[x + y * width] = new Cell(0);
            }
        }
    }

    private void UpdateCells()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                futureCells[x + y * width].density = cells[x + y * width].density;
                futureCells[x + y * width].updated = false;
            }
        }        
        
        //Up Right
        if (numberOfSims % 4 == 0)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (futureCells[x + y * width].updated)
                    {                        
                        continue;
                    }

                    UpdateCell(x, y);
                }
            }
        }//Down Left
        else if (numberOfSims % 4 == 1)
        {
            for (int x = (int)width - 1; x > -1; x--)
            {
                for (int y = (int)height - 1; y > -1; y--)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    UpdateCell(x, y);
                }
            }
        }//Down Right
        else if (numberOfSims % 4 == 2)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = (int)height - 1; y > -1; y--)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    UpdateCell(x, y);
                }
            }
        }//Up Left
        else if (numberOfSims % 4 == 3)
        {
            for (int x = (int)width - 1; x > -1; x--)
            {
                for (int y = 0; y < height; y++)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    UpdateCell(x, y);
                }
            }
        }

        densitySum = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width].density = futureCells[x + y * width].density;
                futureCells[x + y * width].updated = false;


                densitySum += cells[x + y * width].density;

            }
        }

        numberOfSims++;
    }

    private void UpdateCell(int x, int y)
    {
        int nx = x, ny = y;        

        futureCells[x + y * width].density = cells[x + y * width].density;

        int rand = Mathf.RoundToInt(Random.value * 4);
        int index = 0;

        int min = 1, max = 20;
        int nCount = GetNeighborCount(x, y);

        if (nCount < 3)
        {
            if(nCount == 3 && futureCells[x + y * width].density == 1)
            {
                futureCells[x + y * width].density--;
            }
            
            futureCells[x + y * width].updated = true;
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            index = (rand + i) % 4;
            //index = 0;

            switch (index)
            {
                default:
                case 3:
                    //Down
                    nx = x; ny = y - 1;
                    break;
                case 1:
                    //Up
                    nx = x; ny = y + 1;
                    break;
                case 2:
                    //Left
                    nx = x - 1; ny = y;
                    break;
                case 0:
                    //Right
                    nx = x + 1; ny = y;
                    break;
                case 4:
                    //Down Right
                    nx = x + 1; ny = y - 1;
                    break;
                case 5:
                    //Down Left
                    nx = x - 1; ny = y - 1;
                    break;
                case 6:
                    //Top Right
                    nx = x + 1; ny = y + 1;
                    break;
                case 7:
                    //Top Left
                    nx = x - 1; ny = y + 1;
                    break;
            }

            ChangeDensity(x, y, nx, ny, min, max);
        }

        futureCells[x + y * width].updated = true;

    }

    private void ChangeDensity(int x, int y, int nx, int ny, int min, int max)
    {
        if(futureCells[x + y * width].updated || !InBounds(nx, ny))
        {
            return;
        }

        int denisty = cells[x + y * width].density;
        int aHeight = cells[x + y * width].density + cells[x + y * width].height; //Height of current cell
        int nHeight = cells[nx + ny * width].density + cells[nx + ny * width].height; //height of n cell

        int canMove = denisty - min - cells[nx + ny * width].height;

        if(canMove + cells[nx + ny * width].density > max)
        {
            canMove = max - cells[nx + ny * width].density;
        }        

        if (!futureCells[nx + ny * width].updated)
        {
            if (canMove > 0 && cells[nx + ny * width].density < max && nHeight < aHeight && cells[nx + ny * width].height < aHeight)
            {
                int rand = Random.Range(1, canMove);

                futureCells[x + y * width].density -= rand;
                futureCells[x + y * width].updated = true;

                futureCells[nx + ny * width].density += rand;
                futureCells[nx + ny * width].updated = true;                
            }
        }        
    }

    private int GetNeighborCount(int x, int y)
    {
        int count = 0;
        int density = cells[x + y * width].density;
        int aheight = cells[x + y * width].height + density;
        int nHeight = 0;

        for (int nx = (int)Mathf.Clamp(x - 1, 0, width); nx >= 0 && nx < width && nx < x + 2; nx++)
        {
            for (int ny = (int)Mathf.Clamp(y - 1, 0, height); ny >= 0 && ny < height && ny < y + 2; ny++)
            {
                if (nx == x && ny == y)
                {
                    continue;
                }

                nHeight = cells[nx + ny * width].height + cells[nx + ny * width].density;

                if (cells[nx + ny * width].density < density && nHeight < aheight)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public float GetAverageDensity(int x, int y)
    {
        float sum = 0;
        int count = 0;        

        for (int nx = (int)Mathf.Clamp(x - 2, 0, width); nx >= 0 && nx < width && nx < x + 4; nx++)
        {
            for (int ny = (int)Mathf.Clamp(y - 2, 0, height); ny >= 0 && ny < height && ny < y + 4; ny++)
            {
                sum += cells[nx + ny * width].density;
                count++;
            }
        }

        return sum / (float)count;
    }

    private void OnDrawGizmos()
    {
        Vector3 scale = new Vector3(0.1f, 0.1f, 0);
        Vector3 offset = new Vector3(scale.x / 2, scale.y / 2, 0);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new Vector3(width / 2f * scale.x, height / 2f * scale.y) + transform.position, new Vector3(width * scale.x, height * scale.y));
        Color orange = Color.Lerp(Color.red, Color.yellow, 0.5f);

        if (cells == null || cells.Length != width * height)
        {
            return;
        }

        Gizmos.color = Color.black;
        int density = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                density = (useCellAverage) ? Mathf.RoundToInt(GetAverageDensity(x, y)) : cells[x + y * width].density;

                if (density > 0)
                {
                    switch (density)
                    {
                        case 1:
                            Gizmos.color = Color.red;
                            break;
                        case 2:
                            Gizmos.color = orange;
                            break;
                        case 3:
                            Gizmos.color = Color.yellow;
                            break;
                        default:
                            Gizmos.color = Color.black;
                            break;

                    }                    

                    Vector3 boxScale = new Vector3(scale.x, scale.y, 0f);
                    Gizmos.DrawCube(new Vector3(x * scale.x, y * scale.y, 0) + offset + transform.position, boxScale);
                }
            }
        }

        if (drawTerrain)
        {
            int tHeight = 0;

            Color tColor = new Color(0, 0, 1, 0.1f);
            Color tColor2 = new Color(0, 1, 0, 0.1f);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tHeight = cells[x + y * width].height;

                    if (tHeight > 0)
                    {
                        switch (tHeight)
                        {
                            default:
                                Gizmos.color = tColor;
                                break;
                            case 5:
                                Gizmos.color = tColor2;
                                break;

                        }

                        Vector3 boxScale = new Vector3(scale.x, scale.y, 0f);
                        Gizmos.DrawCube(new Vector3(x * scale.x, y * scale.y, -0.1f) + offset + transform.position, boxScale);
                    }
                }
            }
        }

        
    }
}
