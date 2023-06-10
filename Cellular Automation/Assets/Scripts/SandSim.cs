using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandSim : MonoBehaviour
{    
    void Start()
    {
        InitializeCells();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            InitializeCells();
        }

        if (Input.GetKey(KeyCode.S))
        {
            cells[47 + (height - 1) * width].type = Cell.Type.Sand;
            cells[48 + (height - 2) * width].type = Cell.Type.Sand;
            cells[49 + (height - 1) * width].type = Cell.Type.Sand;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            cells[48 + (height - 1) * width].type = Cell.Type.Water;
            cells[47 + (height - 2) * width].type = Cell.Type.Water;
            cells[49 + (height - 1) * width].type = Cell.Type.Water;
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
            UpdateCells();
        }
        
    }

    public bool update = true;

    public uint width = 100;
    public uint height = 100;

    [HideInInspector] public Cell[] cells;
    [HideInInspector] public Cell[] futureCells;

    public int numberOfSims = 0;
    public int sandCount;
    public int waterCount;

    public class Cell
    {
        public enum Type
        {
            Empty,
            Sand,
            Water
        }

        public Type type = Type.Empty;
        public bool updated = false;

        public Cell(Type type)
        {
            this.type = type;
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
                cells[x + y * width] = new Cell(Cell.Type.Empty);
                futureCells[x + y * width] = new Cell(Cell.Type.Empty);
            }
        }
    }

    private void UpdateCells()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                futureCells[x + y * width].type = cells[x + y * width].type;
                futureCells[x + y * width].updated = false;
            }
        }

        //Soilds
        if (numberOfSims % 2 == 0)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    switch (cells[x + y * width].type)
                    {
                        default:
                        case Cell.Type.Empty:
                            continue;
                        case Cell.Type.Sand:
                            UpdateSandCell(x, y);
                            break;
                    }
                }
            }
        }
        else
        {
            for (int x = (int)width - 1; x > -1; x--)
            {
                for (int y = (int)height - 1; y > -1; y--)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    switch (cells[x + y * width].type)
                    {
                        default:
                        case Cell.Type.Empty:
                            continue;
                        case Cell.Type.Sand:
                            UpdateSandCell(x, y);
                            break;
                    }
                }
            }
        }

        //Fluids
        if (numberOfSims % 2 == 0)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    switch (cells[x + y * width].type)
                    {
                        default:
                        case Cell.Type.Empty:
                            continue;
                        case Cell.Type.Water:
                            UpdateWaterCell(x, y);
                            break;
                    }
                }
            }
        }
        else
        {
            for (int x = (int)width - 1; x > -1; x--)
            {
                for (int y = (int)height - 1; y > -1; y--)
                {
                    if (futureCells[x + y * width].updated)
                    {
                        continue;
                    }

                    switch (cells[x + y * width].type)
                    {
                        default:
                        case Cell.Type.Empty:
                            continue;
                        case Cell.Type.Water:
                            UpdateWaterCell(x, y);
                            break;
                    }
                }
            }
        }

        sandCount = 0;
        waterCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x + y * width].type = futureCells[x + y * width].type;
                futureCells[x + y * width].updated = false;

                switch (cells[x + y * width].type)
                {
                    case Cell.Type.Empty:
                        break;
                    case Cell.Type.Sand:
                        sandCount++;
                        break;
                    case Cell.Type.Water:
                        waterCount++;
                        break;
                    default:
                        break;
                }
            }
        }

        numberOfSims++;
    }

    private void UpdateSandCell(int x, int y)
    {
        int nx = x, ny = y;

        nx = x; ny = y + 1;
        bool aboveIsEmpty = (y == height - 1) || (InBounds(nx, ny) && (cells[nx + ny * width].type == Cell.Type.Empty || cells[nx + ny * width].type == Cell.Type.Water));

        futureCells[x + y * width].type = Cell.Type.Sand;
        futureCells[x + y * width].updated = true;

        //Down Water
        nx = x; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Water && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Water;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Down
        nx = x; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        if (!aboveIsEmpty)
        {
            //return;
        }

        //Left Down Water
        nx = x - 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Water && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Water;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Right Down Water
        nx = x + 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Water && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Water;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Left Down
        nx = x - 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Right Down
        nx = x + 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Sand;
            futureCells[nx + ny * width].updated = true;
            return;
        }
    }

    private void UpdateWaterCell(int x, int y)
    {
        int nx = x, ny = y;

        nx = x; ny = y + 1;
        bool aboveIsEmpty = (y == height - 1) || (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty);

        futureCells[x + y * width].type = Cell.Type.Water;
        futureCells[x + y * width].updated = true;

        //Down
        nx = x; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Water;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        if (!aboveIsEmpty)
        {
            return;
        }

        //Left Down
        nx = x - 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Water;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Right Down
        nx = x + 1; ny = y - 1;
        if (InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Water;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        int rand = Mathf.RoundToInt(Random.value);

        //Left
        nx = x - 1; ny = y;
        if (rand == 1 && aboveIsEmpty && InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Water;
            futureCells[nx + ny * width].updated = true;
            return;
        }

        //Right
        nx = x + 1; ny = y;
        if (rand == 0 && aboveIsEmpty && InBounds(nx, ny) && cells[nx + ny * width].type == Cell.Type.Empty && !futureCells[nx + ny * width].updated)
        {
            futureCells[x + y * width].type = Cell.Type.Empty;
            futureCells[x + y * width].updated = true;

            futureCells[nx + ny * width].type = Cell.Type.Water;
            futureCells[nx + ny * width].updated = true;
            return;
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 scale = new Vector3(0.1f, 0.1f, 0);
        Vector3 offset = new Vector3(scale.x / 2, scale.y / 2, 0);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new Vector3(width / 2f * scale.x, height / 2f * scale.y) + transform.position, new Vector3(width * scale.x, height * scale.y));

        if (cells == null || cells.Length != width * height)
        {
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[x + y * width].type != Cell.Type.Empty)
                {
                    switch (cells[x + y * width].type)
                    {
                        default:                            
                        case Cell.Type.Empty:
                            continue;                            
                        case Cell.Type.Sand:
                            Gizmos.color = Color.yellow;
                            break;
                        case Cell.Type.Water:
                            Gizmos.color = Color.blue;
                            break;

                    }

                    Vector3 boxScale = new Vector3(scale.x, scale.y, 0f);
                    Gizmos.DrawCube(new Vector3(x * scale.x, y * scale.y, 0) + offset + transform.position, boxScale);
                }
            }
        }
    }
}
