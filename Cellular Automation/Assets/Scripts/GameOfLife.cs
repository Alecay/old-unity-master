using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOfLife : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        InitializeCells();
        RandomFill(System.DateTime.Now.ToString());
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCells();

        if (Input.GetKeyDown(KeyCode.R))
        {
            RandomFill(System.DateTime.Now.ToString());//, 48, 48, 5);
        }
    }

    public uint width = 100;
    public uint height = 100;

    public Cell[] cells;
    public Cell[] futureCells;

    public class Cell
    {        
        public bool alive = false;
        public int lifetime = 5;

        public Cell(bool alive, int lifetime)
        {
            this.alive = alive;
            this.lifetime = lifetime;
        }
    }

    private void InitializeCells()
    {
        cells = new Cell[width * height];
        futureCells = new Cell[width * height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width] = new Cell(false, 0);
                futureCells[x + y * width] = new Cell(false, 0);
            }
        }
    }

    private void UpdateCells()
    {
        //1- living cell dies if not next to 2 or 3 alive neighbors
        //2 - dead cell is revived if next to 3 alive neighbors
        int n = 0;
        bool died = false;
        bool born = false;
        bool oldAge = false;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                n = GetNeighborCount(x, y);
                died = false;
                born = false;
                oldAge = false;

                if (false && cells[x + y * width].lifetime < 0)
                {
                    futureCells[x + y * width].alive = false;
                    died = true;
                    oldAge = true;
                }
                else if (cells[x + y * width].alive && (n < 2 || n >  3))
                {
                    futureCells[x + y * width].alive = false;
                    died = true;
                }
                else if(!cells[x + y * width].alive && n == 3)
                {
                    futureCells[x + y * width].alive = true;
                    born = true;
                    futureCells[x + y * width].lifetime = Random.Range(1, 30);
                }
                else if (cells[x + y * width].alive && (x == 0 || x == width - 1 || y == 0 || y == height - 1))
                {
                    futureCells[x + y * width].alive = false;
                    died = true;
                }

                if (!born && !died)
                {
                    futureCells[x + y * width].lifetime = cells[x + y * width].lifetime - 1;
                }

                if(died && oldAge && Random.Range(0, 100) < 5)
                {
                    futureCells[x + y * width].alive = true;
                    born = true;
                }

                if (born)
                {
                    if (!oldAge)
                    {
                        futureCells[x + y * width].lifetime = Random.Range(5, 50);
                    }
                    else
                    {
                        futureCells[x + y * width].lifetime = 1;
                    }
                }


                
                
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width].alive = futureCells[x + y * width].alive;
                cells[x + y * width].lifetime = futureCells[x + y * width].lifetime;
            }
        }
    }

    private int GetNeighborCount(int x, int y)
    {
        int count = 0;
        for (int nx = (int)Mathf.Clamp(x - 1, 0, width); nx >= 0 && nx < width && nx < x + 2; nx++)
        {
            for (int ny = (int)Mathf.Clamp(y - 1, 0, height); ny >= 0 && ny < height && ny < y + 2; ny++)
            {
                if(nx == x && ny == y)
                {
                    continue;
                }

                if(cells[nx + ny * width].alive)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void RandomFill(string seed)
    {
        System.Random rng = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width].alive = rng.Next(0, 100) < 30f;
                cells[x + y * width].lifetime = rng.Next(1, 30);
            }
        }
    }

    private void RandomFill(string seed, int xStart, int yStart, int size)
    {
        System.Random rng = new System.Random(seed.GetHashCode());

        for (int x = xStart; x < width && x < xStart + size; x++)
        {
            for (int y = yStart; y < height && y < yStart + size; y++)
            {
                cells[x + y * width].alive = rng.Next(0, 100) < 30f;
                cells[x + y * width].lifetime = rng.Next(5, 50);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 scale = new Vector3(0.1f, 0.1f, 0);
        Vector3 offset = new Vector3(scale.x / 2, scale.y / 2, 0);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new Vector3(width / 2f * scale.x, height / 2f * scale.y) + transform.position, new Vector3(width * scale.x, height * scale.y));

        if(cells == null || cells.Length != width * height)
        {
            return;
        }

        int n = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(cells[x + y * width].alive)
                {
                    n = GetNeighborCount(x, y);

                    switch (n)
                    {
                        default:
                        case 1:
                            //continue;
                            Gizmos.color = Color.gray;
                            break;
                        case 2:
                            Gizmos.color = Color.black;
                            break;
                        case 3:
                            Gizmos.color = Color.green;
                            break;
                    }
                    //Gizmos.color = (n == 2) ? Color.black : Color.green;

                    Vector3 boxScale = new Vector3(scale.x, scale.y, 0f);                    
                    Gizmos.DrawCube(new Vector3(x * scale.x, y * scale.y, 0) + offset + transform.position, boxScale);
                }
            }
        }
    }
}
