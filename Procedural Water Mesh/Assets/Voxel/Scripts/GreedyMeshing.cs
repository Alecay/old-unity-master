using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

public class GreedyMeshing
{
    public static int XYZToLinearIndex(int x, int y, int z, int width)
    {
        return x + y * width + z * width * width;
    }

    public static Vector3Int LinearIndexToXYZ(int index, int width)
    {
        int sizeSqd = width * width;

        return new Vector3Int(index % sizeSqd % width, index % sizeSqd / width, index / sizeSqd);
    }

    public static int XYToLinearIndex(int x, int y, int width)
    {
        return x + y * width;
    }

    public static Vector2Int LinearIndexToXY(int index, int width)
    {
        return new Vector2Int(index % width, index / width);
    }

    //[BurstCompile]
    struct SliceJob : IJobParallelFor
    {
        public static int MAX_START_END_SIZE = 64;

        /// <summary>
        /// The width of the square 3D area the slice takes place in
        /// </summary>
        public readonly int width;

        /// <summary>
        /// Array of ints that correspond to the ID or type of cell
        /// </summary>
        private int[] cellIDs;

        /// <summary>
        /// The cell IDs of the voxels on this mesh's boundary
        /// </summary>
        private int[] boundaryCellIDs; // Length is width * width * 6 for each face

        /// <summary>
        /// A list of cell IDs that are non-solid meaning they aren't counted when preforming slice
        /// </summary>
        private int[] nonSolidIDs;

        //public VoxelMesh.Palette palette = new VoxelMesh.Palette();

        //An array where every 6 ints represnt the index of each face of a voxel
        private int[] voxelTextureIndices;

        public NativeArray<NativeArray<StartEnd>> slices;

        public SliceJob(int width, VoxelMesh.Palette palette, int[] cellIDs, int[] boundaryCellIDs, List<int> nonSolidIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            this.boundaryCellIDs = boundaryCellIDs;
            this.nonSolidIDs = new int[nonSolidIDs.Count];
            nonSolidIDs.CopyTo(this.nonSolidIDs);

            slices = new NativeArray<NativeArray<StartEnd>>(36, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            //this.palette = palette;

            voxelTextureIndices = palette.GetVoxelTextureIndices();
        }

        public void SetNonSolidIDs(List<int> nonSolidIDs)
        {
            this.nonSolidIDs = new int[nonSolidIDs.Count];
            nonSolidIDs.CopyTo(this.nonSolidIDs);
        }

        public int GetCellID(int x, int y, int z)
        {
            //return cellIDs[XYZToLinearIndex(x, y, z, width)];
            if (InBounds(x, y, z))
            {
                return cellIDs[GreedyMeshing.XYZToLinearIndex(x, y, z, width)];
            }
            else if (InBoundaryBounds(x, y, z))
            {
                return boundaryCellIDs[GetBoundaryIndex(x, y, z)];
            }

            return 0;
        }

        private int GetBoundaryIndex(int x, int y, int z)
        {
            //Up
            if (y == width)
            {
                return (width * width) * 0 + x + z * width;
            }
            //Down
            else if (y == -1)
            {
                return (width * width) * 1 + x + z * width;
            }
            //Left
            else if (x == -1)
            {
                return (width * width) * 2 + z + y * width;
            }
            //Right
            else if (x == width)
            {
                return (width * width) * 3 + z + y * width;
            }
            //Forward
            else if (z == width)
            {
                return (width * width) * 4 + x + y * width;
            }
            //Back
            else if (z == -1)
            {
                return (width * width) * 5 + x + y * width;
            }

            return 0;
        }

        public int GetCellTextureIndex(int x, int y, int z, int faceIndex)
        {
            int index = cellIDs[XYZToLinearIndex(x, y, z, width)];

            int startingIndex = index * 6;

            return voxelTextureIndices[startingIndex + faceIndex % 6];            
        }

        //public int GetCellAnimationInfo(int x, int y, int z, int faceIndex)
        //{
        //    int index = cellIDs[XYZToLinearIndex(x, y, z, width)];

        //    var voxelID = palette.GetVoxelID(index);

        //    var voxelData = VoxelDataCollection.GetVoxelData(voxelID);

        //    string textureID = voxelData.GetTextureID(faceIndex);

        //    var tData = TextureDataCollection.GetTextureData(textureID);

        //    return tData.GetAnimationDataInt();
        //}

        public bool IsSolid(int x, int y, int z)
        {
            int id = GetCellID(x, y, z);
            for (int i = 0; i < nonSolidIDs.Length; i++)
            {
                if(nonSolidIDs[i] == id)
                {
                    return false;
                }
            }

            return true;
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < width && z >= 0 && z < width;
        }

        private bool InBoundaryBounds(int x, int y, int z)
        {
            bool inRange = x >= -1 && x <= width && y >= -1 && y <= width && z >= -1 && z <= width;

            int outerCount = 0;

            if (x == -1 || x == width)
            {
                outerCount++;
            }

            if (y == -1 || y == width)
            {
                outerCount++;
            }

            if (z == -1 || z == width)
            {
                outerCount++;
            }

            return inRange && outerCount == 1;
        }

        private bool FaceIsVisible(int x, int y, int z, int faceIndex)
        {
            switch (faceIndex)
            {
                default: //Up
                case 0:
                    if (!IsSolid(x, y + 1, z))
                    {
                        return true;
                    }
                    break;
                case 1: //Down
                    if (!IsSolid(x, y - 1, z))
                    {
                        return true;
                    }
                    break;
                case 2: //Left
                    if (!IsSolid(x - 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 3: //Right
                    if (!IsSolid(x + 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 4: //Forward
                    if (!IsSolid(x, y, z + 1))
                    {
                        return true;
                    }
                    break;
                case 5: //Back
                    if (!IsSolid(x, y, z - 1))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        public struct StartEnd
        {
            public Vector3Int start;
            public Vector3Int end;

            public Vector3Int SizeInts
            {
                get
                {
                    return (end + Vector3Int.one) - start;
                }
            }

            public Vector3 Size
            {
                get
                {
                    return (end + Vector3.one) - start;
                }
            }

            public Vector3 Center
            {
                get
                {
                    return start + Size / 2f;
                }
            }

            public Vector3[] Corners
            {
                get
                {
                    Vector3[] corners = new Vector3[8];

                    //Bottom face starting in bottom left going clockwise
                    corners[0] = start;
                    corners[1] = new Vector3(start.x, start.y, end.z + 1);
                    corners[2] = new Vector3(end.x + 1, start.y, end.z + 1);
                    corners[3] = new Vector3(end.x + 1, start.y, start.z);

                    corners[4] = new Vector3(start.x, end.y + 1, start.z);
                    corners[5] = new Vector3(start.x, end.y + 1, end.z + 1);
                    corners[6] = end + Vector3Int.one;
                    corners[7] = new Vector3(end.x + 1, end.y + 1, start.z);

                    return corners;
                }
            }

            public StartEnd(Vector3Int start, Vector3Int end)
            {
                this.start = start;
                this.end = end;
            }

            public StartEnd(int startX, int startY, int startZ, int endX, int endY, int endZ)
            {
                this.start = new Vector3Int(startX, startY, startZ);
                this.end = new Vector3Int(endX, endY, endZ);
            }
        }

        public List<StartEnd> PreformXZSlice(int yIndex, bool upFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = new int[width * width];
            bool[] visible = new bool[width * width];

            int visibleCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < width; z++)
                {

                    cellIDs2D[XYToLinearIndex(x, z, width)] = GetCellTextureIndex(x, yIndex, z, upFaces ? 0 : 1);

                    visible[XYToLinearIndex(x, z, width)] = IsSolid(x, yIndex, z) && FaceIsVisible(x, yIndex, z, upFaces ? 0 : 1);

                    if (visible[XYToLinearIndex(x, z, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            Slice2D slice2D = new Slice2D(width, cellIDs2D, visible, new List<int>(nonSolidIDs));

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(startEnd2D.start.x, yIndex, startEnd2D.start.y, startEnd2D.end.x, yIndex, startEnd2D.end.y));
            }

            return startEnds;
        }

        public List<StartEnd> PreformZYSlice(int xIndex, bool leftFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = new int[width * width];
            bool[] visible = new bool[width * width];

            int visibleCount = 0;

            for (int z = 0; z < width; z++)
            {
                for (int y = 0; y < width; y++)
                {
                    cellIDs2D[XYToLinearIndex(z, y, width)] = GetCellTextureIndex(xIndex, y, z, leftFaces ? 2 : 3);

                    visible[XYToLinearIndex(z, y, width)] = IsSolid(xIndex, y, z) && FaceIsVisible(xIndex, y, z, leftFaces ? 2 : 3);

                    if (visible[XYToLinearIndex(z, y, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            Slice2D slice2D = new Slice2D(width, cellIDs2D, visible, new List<int>(nonSolidIDs));

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(xIndex, startEnd2D.start.y, startEnd2D.start.x, xIndex, startEnd2D.end.y, startEnd2D.end.x));
            }

            return startEnds;
        }

        public List<StartEnd> PreformXYSlice(int zIndex, bool forwardFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = new int[width * width];
            bool[] visible = new bool[width * width];

            int visibleCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    cellIDs2D[XYToLinearIndex(x, y, width)] = GetCellTextureIndex(x, y, zIndex, forwardFaces ? 4 : 5);

                    visible[XYToLinearIndex(x, y, width)] = IsSolid(x, y, zIndex) && FaceIsVisible(x, y, zIndex, forwardFaces ? 4 : 5);

                    if (visible[XYToLinearIndex(x, y, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            Slice2D slice2D = new Slice2D(width, cellIDs2D, visible, new List<int>(nonSolidIDs));

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(startEnd2D.start.x, startEnd2D.start.y, zIndex, startEnd2D.end.x, startEnd2D.end.y, zIndex));
            }

            return startEnds;
        }

        public List<StartEnd>[] PreformSlices()
        {
            List<StartEnd>[] slices = new List<StartEnd>[6];

            for (int i = 0; i < 6; i++)
            {
                slices[i] = new List<StartEnd>();
            }

            for (int index = 0; index < width; index++)
            {
                slices[0].AddRange(PreformXZSlice(index, true)); //Up

                slices[1].AddRange(PreformXZSlice(index, false)); //Down

                slices[2].AddRange(PreformZYSlice(index, true)); //Left

                slices[3].AddRange(PreformZYSlice(index, false)); //Right

                slices[4].AddRange(PreformXYSlice(index, true)); //Forward

                slices[5].AddRange(PreformXYSlice(index, false)); //Back
            }

            return slices;
        }

        public void Execute(int index)
        {
            //Work on finishing job section and test for speed comparsion.
            //each slice should happen and then be stored into slices

            //Top
            if(index < 6)
            {
                List<StartEnd> slice = PreformXZSlice(index, true);
                slices[index] = new NativeArray<StartEnd>(slice.ToArray(), Allocator.TempJob);
            }
            //Bottom
            else if (index < 12)
            {
                List<StartEnd> slice = PreformXZSlice(index - 6, false);
                slices[index] = new NativeArray<StartEnd>(slice.ToArray(), Allocator.TempJob);
            }
        }
    }

    public class Slice2D
    {
        public static int MAX_START_END_SIZE = 64;

        /// <summary>
        /// The width of the square 2D area the slice takes place in
        /// </summary>
        public int width;

        /// <summary>
        /// A bool array that corresponds to each cell, true if this cell has been visited before
        /// </summary>
        private bool[] visited;

        /// <summary>
        /// A bool array that corresponds to each cell, true if this cell is visible
        /// </summary>
        public bool[] visible;

        /// <summary>
        /// Array of ints that correspond to the ID or type of cell
        /// </summary>
        public int[] cellIDs;

        /// <summary>
        /// A list of cell IDs that are non-solid meaning they aren't counted when preforming slice
        /// </summary>
        private List<int> nonSolidIDs = new List<int>();

        public Slice2D()
        {
            width = 16;
            visited = new bool[width * width];
            cellIDs = new int[width * width];
            nonSolidIDs = new List<int>();
        }

        public Slice2D(int width, int[] cellIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            //this.nonSolidIDs = nonSolidIDs;

            visible = new bool[width * width];
            for (int i = 0; i < width * width; i++)
            {
                visible[i] = true;
            }

            visited = new bool[width * width];            
        }

        public Slice2D(int width, int[] cellIDs, bool[] visible, List<int> nonSolidIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            this.visible = visible;
            this.nonSolidIDs = nonSolidIDs;

            visited = new bool[width * width];
        }

        public void SetData(int width, int[] cellIDs, bool[] visible, List<int> nonSolidIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            this.visible = visible;
            this.nonSolidIDs = nonSolidIDs;

            for (int i = 0; i < width * width; i++)
            {
                visited[i] = false;
            }
        }

        public void SetNonSolidIDs(List<int> nonSolidIDs)
        {
            this.nonSolidIDs = nonSolidIDs;
        }

        public void AddNonSolidID(int id)
        {
            if(!nonSolidIDs.Contains(id))
                nonSolidIDs.Add(id);
        }

        public int GetCellID(int x, int y)
        {
            return cellIDs[XYToLinearIndex(x, y, width)];
        }

        public bool IsSolid(int x, int y)
        {
            return !nonSolidIDs.Contains(GetCellID(x, y));
        }

        private bool Visited(int x, int y)
        {
            return visited[XYToLinearIndex(x, y, width)];
        }

        private bool Visible(int x, int y)
        {
            return visible[XYToLinearIndex(x, y, width)];
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < width;
        }

        public class StartEnd
        {
            public Vector2Int start;
            public Vector2Int end;

            public Vector2Int SizeInts
            {
                get
                {
                    return (end + Vector2Int.one) - start;
                }
            }

            public Vector2 Size
            {
                get
                {
                    return (end + Vector2.one) - start;
                }
            }

            public Vector2 Center
            {
                get
                {
                    return start + Size / 2f;
                }
            }

            public StartEnd(Vector2Int start, Vector2Int end)
            {
                this.start = start;
                this.end = end;
            }

            public StartEnd(int startX, int startY, int endX, int endY)
            {
                this.start = new Vector2Int(startX, startY);
                this.end = new Vector2Int(endX, endY);
            }
        }

        private bool LCheck(int anchorX, int anchorY, int size)
        {
            //This function checks in an L shape to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            //[0][1][2][3][4]
            // |          [3]
            // |          [2]
            // |   size   [1]
            //[A]---------[0]

            int anchorID = GetCellID(anchorX, anchorY);

            if (!InBounds(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
            {
                return false;
            }

            if(size < 1)
            {
                return false;
            }

            size = Mathf.Clamp(size, 1, MAX_START_END_SIZE);

            if(size == 1)
            {
                return true;
            }

            int x;
            int y;

            //Top of L
            y = anchorY + size - 1;
            for (x = anchorX; x < anchorX + size; x++)
            {
                //Is the current cell solid? & not been visited? and the same ID as the anchor?
                if(!InBounds(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID(x, y) != anchorID)
                {
                    return false;
                }
            }

            //Right of L
            x = anchorX + size - 1;
            for (y = anchorY; y < anchorY + size - 1; y++)
            {
                //Is the current cell solid? & not been visited? and the same ID as the anchor?
                if (!InBounds(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID(x, y) != anchorID)
                {
                    return false;
                }
            }


            return true;
        }

        private bool HorizontalCheck(int anchorX, int anchorY, int size, int xWidth)
        {
            //This function checks in a horizontal line to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            //[0][1][2][3][4]
            // |           |
            // |           |
            // |   size    |
            //[A]----------|

            int anchorID = GetCellID(anchorX, anchorY);            

            if (!InBounds(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
            {
                return false;
            }

            if (size < 1)
            {
                return false;
            }

            size = Mathf.Clamp(size, 1, MAX_START_END_SIZE);

            if (size == 1)
            {
                return true;
            }

            int x;
            int y;

            //Top of L
            y = anchorY + size - 1;
            for (x = anchorX; x < anchorX + xWidth; x++)
            {
                //Is the current cell solid? & not been visited? and the same ID as the anchor?
                if (!InBounds(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID(x, y) != anchorID)
                {
                    return false;
                }
            }

            return true;
        }

        private bool VerticalCheck(int anchorX, int anchorY, int size, int yWidth)
        {
            //This function checks in an vertical Line to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            // -----------[4]
            // |          [3]
            // |          [2]
            // |   size   [1]
            //[A]---------[0]

            int anchorID = GetCellID(anchorX, anchorY);            

            if (!InBounds(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
            {
                return false;
            }

            if (size < 1)
            {
                return false;
            }

            size = Mathf.Clamp(size, 1, MAX_START_END_SIZE);

            if (size == 1)
            {
                return true;
            }

            int x;
            int y;

            //Right of L
            x = anchorX + size - 1;
            for (y = anchorY; y < anchorY + yWidth; y++)
            {
                //Is the current cell solid? & not been visited? and the same ID as the anchor?
                if (!InBounds(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID(x, y) != anchorID)
                {
                    return false;
                }
            }


            return true;
        }

        private void ExpandDiagonally(ref StartEnd startEnd)
        {
            int lSize = 0;

            //Find the biggest L size
            for (int l = 1; l <= MAX_START_END_SIZE; l++)
            {
                if(!LCheck(startEnd.start.x, startEnd.start.y, l))
                {
                    break;
                }

                lSize = l;
            }

            //Resize end to reflect
            startEnd.end = startEnd.start + Vector2Int.one * (lSize - 1);
        }

        private void ExpandVertically(ref StartEnd startEnd)
        {            
            Vector2Int startEndSize = startEnd.SizeInts;

            int vSize = startEndSize.y;

            //Find the biggest V size
            for (int v = startEndSize.y + 1; v <= MAX_START_END_SIZE; v++)
            {
                if (!HorizontalCheck(startEnd.start.x, startEnd.start.y, v, startEndSize.x))
                {
                    break;
                }

                vSize = v;
            }

            //Resize end to reflect
            startEnd.end = new Vector2Int(startEnd.end.x, startEnd.start.y + vSize - 1);
        }

        private void ExpandHorizontally(ref StartEnd startEnd)
        {
            Vector2Int startEndSize = startEnd.SizeInts;

            int hSize = startEndSize.x;

            //Find the biggest V size
            for (int h = startEndSize.x + 1; h <= MAX_START_END_SIZE; h++)
            {
                if (!VerticalCheck(startEnd.start.x, startEnd.start.y, h, startEndSize.y))
                {
                    break;
                }

                hSize = h;
            }           

            //Resize end to reflect
            startEnd.end = new Vector2Int(startEnd.start.x + hSize - 1, startEnd.end.y);
        }

        private void SetVisited(StartEnd startEnd)
        {
            for (int x = startEnd.start.x; x <= startEnd.end.x; x++)
            {
                for (int y = startEnd.start.y; y <= startEnd.end.y; y++)
                {
                    visited[XYToLinearIndex(x, y, width)] = true;
                }
            }
        }

        public List<StartEnd> PreformSlice()
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            StartEnd currentStartEnd;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!IsSolid(x, y) || Visited(x, y) || !Visible(x, y))
                    {
                        continue;
                    }

                    currentStartEnd = new StartEnd(x, y, x, y);

                    ExpandDiagonally(ref currentStartEnd);

                    ExpandVertically(ref currentStartEnd);

                    ExpandHorizontally(ref currentStartEnd);

                    SetVisited(currentStartEnd);

                    startEnds.Add(currentStartEnd);
                }
            }

            return startEnds;
        }               


    }

    public class Slice3D
    {
        public static int MAX_START_END_SIZE = 64;

        /// <summary>
        /// The width of the square 3D area the slice takes place in
        /// </summary>
        public readonly int width;

        /// <summary>
        /// Array of ints that correspond to the ID or type of cell
        /// </summary>
        private int[] cellIDs;

        /// <summary>
        /// The cell IDs of the voxels on this mesh's boundary
        /// </summary>
        private int[] boundaryCellIDs; // Length is width * width * 6 for each face

        /// <summary>
        /// A list of cell IDs that are non-solid meaning they aren't counted when preforming slice
        /// </summary>
        private List<int> nonSolidIDs = new List<int>();

        /// <summary>
        /// The Texture of each side of the voxel
        /// </summary>
        private int[] voxelTextureIndices;

        /// <summary>
        /// The Texture of each side of the voxel
        /// </summary>
        private int[] arrayTextureIndices;

        //public VoxelMesh.Palette palette = new VoxelMesh.Palette();

        private Slice2D slice2D;

        public Slice3D()
        {
            width = 16;
            cellIDs = new int[width * width * width];
            nonSolidIDs = new List<int>();
        }

        public Slice3D(int width, VoxelMesh.Palette palette, int[] cellIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            //this.nonSolidIDs = nonSolidIDs;
            //this.palette = palette;
            this.voxelTextureIndices = palette.GetVoxelTextureIndices();
            this.arrayTextureIndices = palette.GetArrayTextureIndices();
        }

        public Slice3D(int width, VoxelMesh.Palette palette, int[] cellIDs, int[] boundaryCellIDs, List<int> nonSolidIDs)
        {
            this.width = width;
            this.cellIDs = cellIDs;
            this.boundaryCellIDs = boundaryCellIDs;
            this.nonSolidIDs = new List<int>(nonSolidIDs);

            this.voxelTextureIndices = palette.GetVoxelTextureIndices();
            this.arrayTextureIndices = palette.GetArrayTextureIndices();
            //this.palette = palette;
        }

        public void SetNonSolidIDs(List<int> nonSolidIDs)
        {
            this.nonSolidIDs = nonSolidIDs;
        }

        public void AddNonSolidID(int id)
        {
            if (!nonSolidIDs.Contains(id))
                nonSolidIDs.Add(id);
        }

        public int GetCellID(int x, int y, int z)
        {
            //return cellIDs[XYZToLinearIndex(x, y, z, width)];
            if (InBounds(x, y, z))
            {
                return cellIDs[GreedyMeshing.XYZToLinearIndex(x, y, z, width)];
            }
            else if (InBoundaryBounds(x, y, z))
            {
                return boundaryCellIDs[GetBoundaryIndex(x, y, z)];
            }

            return 0;
        }

        private int GetBoundaryIndex(int x, int y, int z)
        {
            //Up
            if (y == width)
            {
                return (width * width) * 0 + x + z * width;
            }
            //Down
            else if (y == -1)
            {
                return (width * width) * 1 + x + z * width;
            }
            //Left
            else if (x == -1)
            {
                return (width * width) * 2 + z + y * width;
            }
            //Right
            else if (x == width)
            {
                return (width * width) * 3 + z + y * width;
            }
            //Forward
            else if (z == width)
            {
                return (width * width) * 4 + x + y * width;
            }
            //Back
            else if (z == -1)
            {
                return (width * width) * 5 + x + y * width;
            }

            return 0;
        }

        public int GetCellTextureIndex(int x, int y, int z, int faceIndex)
        {
            int index = cellIDs[XYZToLinearIndex(x, y, z, width)];

            int startingIndex = index * 6;

            return voxelTextureIndices[startingIndex + faceIndex % 6];
        }

        public int GetArrayIndex(int textureID)
        {
            return arrayTextureIndices[textureID];
        }

        //public int GetCellTextureIndex(int x, int y, int z, int faceIndex)
        //{
        //    int index = cellIDs[XYZToLinearIndex(x, y, z, width)];

        //    return palette.GetTextureIndex(index, faceIndex);
        //}

        //public int GetCellAnimationInfo(int x, int y, int z, int faceIndex)
        //{
        //    int index = cellIDs[XYZToLinearIndex(x, y, z, width)];

        //    var voxelID = palette.GetVoxelID(index);

        //    var voxelData = VoxelDataCollection.GetVoxelData(voxelID);

        //    string textureID = voxelData.GetTextureID(faceIndex);

        //    var tData = TextureDataCollection.GetTextureData(textureID);

        //    return tData.GetAnimationDataInt();
        //}

        public bool IsSolid(int x, int y, int z)
        {
            return !nonSolidIDs.Contains(GetCellID(x, y, z));
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < width && z >= 0 && z < width;
        }

        private bool InBoundaryBounds(int x, int y, int z)
        {
            bool inRange = x >= -1 && x <= width && y >= -1 && y <= width && z >= -1 && z <= width;

            int outerCount = 0;

            if (x == -1 || x == width)
            {
                outerCount++;
            }

            if (y == -1 || y == width)
            {
                outerCount++;
            }

            if (z == -1 || z == width)
            {
                outerCount++;
            }

            return inRange && outerCount == 1;
        }

        private bool FaceIsVisible(int x, int y, int z, int faceIndex)
        {
            switch (faceIndex)
            {
                default: //Up
                case 0:
                    if (!IsSolid(x, y + 1, z))
                    {
                        return true;
                    }
                    break;
                case 1: //Down
                    if (!IsSolid(x, y - 1, z))
                    {
                        return true;
                    }
                    break;
                case 2: //Left
                    if (!IsSolid(x - 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 3: //Right
                    if (!IsSolid(x + 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 4: //Forward
                    if (!IsSolid(x, y, z + 1))
                    {
                        return true;
                    }
                    break;
                case 5: //Back
                    if (!IsSolid(x, y, z - 1))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        public class StartEnd
        {
            public Vector3Int start;
            public Vector3Int end;

            public Vector3Int SizeInts
            {
                get
                {
                    return (end + Vector3Int.one) - start;
                }
            }

            public Vector3 Size
            {
                get
                {
                    return (end + Vector3.one) - start;
                }
            }

            public Vector3 Center
            {
                get
                {
                    return start + Size / 2f;
                }
            }

            public Vector3[] Corners
            {
                get
                {
                    Vector3[] corners = new Vector3[8];

                    //Bottom face starting in bottom left going clockwise
                    corners[0] = start;
                    corners[1] = new Vector3(start.x, start.y, end.z + 1);
                    corners[2] = new Vector3(end.x + 1, start.y, end.z + 1);
                    corners[3] = new Vector3(end.x + 1, start.y, start.z);

                    corners[4] = new Vector3(start.x, end.y + 1, start.z);
                    corners[5] = new Vector3(start.x, end.y + 1, end.z + 1);
                    corners[6] = end + Vector3Int.one;
                    corners[7] = new Vector3(end.x + 1, end.y + 1, start.z);

                    return corners;
                }
            }

            public StartEnd(Vector3Int start, Vector3Int end)
            {
                this.start = start;
                this.end = end;
            }

            public StartEnd(int startX, int startY, int startZ, int endX, int endY, int endZ)
            {
                this.start = new Vector3Int(startX, startY, startZ);
                this.end = new Vector3Int(endX, endY, endZ);
            }
        }

        public List<StartEnd> PreformXZSlice(int yIndex, bool upFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = this.slice2D.cellIDs;
            bool[] visible = this.slice2D.visible;

            int visibleCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < width; z++)
                {                    

                    cellIDs2D[XYToLinearIndex(x, z, width)] = GetCellTextureIndex(x, yIndex, z, upFaces ? 0 : 1);

                    visible[XYToLinearIndex(x, z, width)] = IsSolid(x, yIndex, z) && FaceIsVisible(x, yIndex, z, upFaces ? 0 : 1);

                    if (visible[XYToLinearIndex(x, z, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            //Slice2D slice2D = new Slice2D(width, cellIDs2D, visible, nonSolidIDs);
            slice2D.SetData(width, cellIDs2D, visible, nonSolidIDs);

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(startEnd2D.start.x, yIndex, startEnd2D.start.y, startEnd2D.end.x, yIndex, startEnd2D.end.y));
            }

            return startEnds;
        }

        public List<StartEnd> PreformZYSlice(int xIndex, bool leftFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = this.slice2D.cellIDs;
            bool[] visible = this.slice2D.visible;

            int visibleCount = 0;

            for (int z = 0; z < width; z++)
            {
                for (int y = 0; y < width; y++)
                {
                    cellIDs2D[XYToLinearIndex(z, y, width)] = GetCellTextureIndex(xIndex, y, z, leftFaces ? 2 : 3);

                    visible[XYToLinearIndex(z, y, width)] = IsSolid(xIndex, y, z) && FaceIsVisible(xIndex, y, z, leftFaces ? 2 : 3);

                    if (visible[XYToLinearIndex(z, y, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            slice2D.SetData(width, cellIDs2D, visible, nonSolidIDs);

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(xIndex, startEnd2D.start.y, startEnd2D.start.x, xIndex, startEnd2D.end.y, startEnd2D.end.x));
            }

            return startEnds;
        }

        public List<StartEnd> PreformXYSlice(int zIndex, bool forwardFaces)
        {
            List<StartEnd> startEnds = new List<StartEnd>();

            int[] cellIDs2D = this.slice2D.cellIDs;
            bool[] visible = this.slice2D.visible;

            int visibleCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    cellIDs2D[XYToLinearIndex(x, y, width)] = GetCellTextureIndex(x, y, zIndex, forwardFaces ? 4 : 5);

                    visible[XYToLinearIndex(x, y, width)] = IsSolid(x, y, zIndex) && FaceIsVisible(x, y, zIndex, forwardFaces ? 4 : 5);

                    if (visible[XYToLinearIndex(x, y, width)])
                    {
                        visibleCount++;
                    }
                }
            }

            if (visibleCount == 0)
            {
                return new List<StartEnd>();
            }

            slice2D.SetData(width, cellIDs2D, visible, nonSolidIDs);

            List<Slice2D.StartEnd> startEnds2D = slice2D.PreformSlice();

            Slice2D.StartEnd startEnd2D;
            for (int i = 0; i < startEnds2D.Count; i++)
            {
                startEnd2D = startEnds2D[i];
                startEnds.Add(new StartEnd(startEnd2D.start.x, startEnd2D.start.y, zIndex, startEnd2D.end.x, startEnd2D.end.y, zIndex));
            }

            return startEnds;
        }

        public List<StartEnd>[] PreformSlices()
        {
            List<StartEnd>[] slices = new List<StartEnd>[6];
            slice2D = new Slice2D(width, new int[width * width], new bool[width * width], nonSolidIDs);

            for (int i = 0; i < 6; i++)
            {
                slices[i] = new List<StartEnd>();
            }

            for (int index = 0; index < width; index++)
            {
                slices[0].AddRange(PreformXZSlice(index, true)); //Up

                slices[1].AddRange(PreformXZSlice(index, false)); //Down

                slices[2].AddRange(PreformZYSlice(index, true)); //Left

                slices[3].AddRange(PreformZYSlice(index, false)); //Right

                slices[4].AddRange(PreformXYSlice(index, true)); //Forward

                slices[5].AddRange(PreformXYSlice(index, false)); //Back
            }

            return slices;
        }

        private List<Vector3> vertices = new List<Vector3>();
        private List<Vector3> normals = new List<Vector3>();
        private List<int> triangles = new List<int>();
        private List<Vector3> uvs = new List<Vector3>();

        private List<int> usedIDs = new List<int>();

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            vertices.Clear();
            normals.Clear();
            triangles.Clear();
            uvs.Clear();

            usedIDs.Clear();

            int triIndex = 0;

            var slices = PreformSlices();

            StartEnd startEnd;
            int id;
            int textureIndex;
            Vector3[] corners;

            //TODO:

            //Add support for textureAnimations -> use w component of uv to translate animation data from textureData
            //Add support for rotated blocks -> ID data class should be expaned to allow for more data such as rotation and later things like
            //custom data like inventory for chests etc...
            //Rotation -> all voxels should have a rotation field that says what direction the voxel is facing
            //Look at minecraft's log mechanics to get ideas about how they do things
            //Look at increasing chunk build times
            //Change logic around solid blocks being skipped and add support for instanced objects like chests, torches

            //Go through bottom slice and add faces
            for (int index = 0; index < slices[0].Count; index++)
            {
                startEnd = slices[0][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 0);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[4]);
                vertices.Add(corners[5]);
                vertices.Add(corners[6]);
                vertices.Add(corners[7]);

                float xWidth = startEnd.Size.x;
                float yWidth = startEnd.Size.z;

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.up);
                }
            }

            //Go through bottom slice and add faces
            for (int index = 0; index < slices[1].Count; index++)
            {
                startEnd = slices[1][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 1);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[3]);
                vertices.Add(corners[2]);
                vertices.Add(corners[1]);
                vertices.Add(corners[0]);

                float xWidth = startEnd.Size.x;
                float yWidth = startEnd.Size.z;                

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.down);
                }
            }

            //Go through left slice and add faces
            for (int index = 0; index < slices[2].Count; index++)
            {
                startEnd = slices[2][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 2);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[1]);
                vertices.Add(corners[5]);
                vertices.Add(corners[4]);
                vertices.Add(corners[0]);

                float xWidth = startEnd.Size.z;
                float yWidth = startEnd.Size.y;

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.left);
                }
            }

            //Go through right slice and add faces
            for (int index = 0; index < slices[3].Count; index++)
            {
                startEnd = slices[3][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 3);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[3]);
                vertices.Add(corners[7]);
                vertices.Add(corners[6]);
                vertices.Add(corners[2]);

                float xWidth = startEnd.Size.z;
                float yWidth = startEnd.Size.y;

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.right);
                }
            }

            //Go through forward slice and add faces
            for (int index = 0; index < slices[4].Count; index++)
            {
                startEnd = slices[4][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 4);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[2]);
                vertices.Add(corners[6]);
                vertices.Add(corners[5]);
                vertices.Add(corners[1]);

                float xWidth = startEnd.Size.x;
                float yWidth = startEnd.Size.y;

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.forward);
                }
            }            

            //Go through back slice and add faces
            for (int index = 0; index < slices[5].Count; index++)
            {
                startEnd = slices[5][index];

                id = GetCellTextureIndex(startEnd.start.x, startEnd.start.y, startEnd.start.z, 5);

                textureIndex = GetArrayIndex(id);

                corners = startEnd.Corners;

                vertices.Add(corners[0]);
                vertices.Add(corners[4]);
                vertices.Add(corners[7]);
                vertices.Add(corners[3]);

                float xWidth = startEnd.Size.x;
                float yWidth = startEnd.Size.y;

                uvs.Add(new Vector3(0, 0, textureIndex));
                uvs.Add(new Vector3(0, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 1 * yWidth, textureIndex));
                uvs.Add(new Vector3(1 * xWidth, 0, textureIndex));

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);

                triangles.Add(triIndex + 1);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 0);

                triIndex += 4;

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.back);
                }
            }            

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);

            mesh.SetTriangles(triangles, 0);
            
            mesh.SetUVs(0, uvs);            

            return mesh;
        }

        public List<int> GetUsedIDs()
        {
            return usedIDs;
        }

        public void SetMaterialProperties(MeshRenderer meshRenderer, VoxelMesh.Palette palette)
        {
            if (meshRenderer == null)
            {
                return;
            }

            List<Texture2D> textures = new List<Texture2D>();
            List<Vector4> animationInfo = new List<Vector4>();
            string textureID;
            TextureData tData;

            for (int i = 0; i < palette.TextureIDCount; i++)
            {
                textureID = palette.GetTextureID(i);

                tData = TextureDataCollection.GetTextureData(textureID);                

                if(tData.id == "base:none")
                {
                    continue;
                }

                for (int j = 0; j < tData.textures.Count; j++)
                {
                    textures.Add(tData.textures[j]);
                    animationInfo.Add(tData.GetAnimationData());
                }
            }

            Texture2DArray array = new Texture2DArray(textures[0].width, textures[0].height, textures.Count, textures[0].format, false);

            for (int i = 0; i < textures.Count; i++)
            {
                array.SetPixels(textures[i].GetPixels(), i);
            }

            array.filterMode = FilterMode.Point;
            array.wrapMode = TextureWrapMode.Repeat;
            array.Apply();

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetTexture("_MainTex", array);
            props.SetFloat("_ArrayLength", textures.Count);
            props.SetVectorArray("_AnimationInfo", animationInfo);            

            //Debug.Log("Created Texture array with " + array.depth + " textures");

            meshRenderer.SetPropertyBlock(props);

            //Debug.Log("Setting material props, animation data length: " + animationInfo.Count);

        }

    }
}
