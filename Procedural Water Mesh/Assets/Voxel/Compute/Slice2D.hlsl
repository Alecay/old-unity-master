#ifndef Slice2D
#define Slice2D

    struct StartEnd
    {
	    int2 start;
	    int2 end;
	
	    int faceIndex;   
    };

    int MAX_START_END_SIZE = 64;

    //The size of the grid of the slice
    int _SliceWidth;

    RWStructuredBuffer<bool> visited2D;
    RWStructuredBuffer<bool> visible2D;

    RWStructuredBuffer<int> cellIDs2D;

    RWStructuredBuffer<int> voxelIDs;
    //Length -> _SliceWidth * _SliceWidth * 6
    //The texture of each face of each voxel
    RWStructuredBuffer<int> textureIDs;

    int _NonSolidCount;

    RWStructuredBuffer<int> nonSolidVoxelIDs;

    int _MaxStartEndsCount; // -> _SliceWidth * _SliceWidth * 0.5f

    RWStructuredBuffer<StartEnd> startEnds;

    int2 GetSizeInts(StartEnd startEnd)
    {
	    return (startEnd.end + int2(1, 1)) - startEnd.start;
    }

    int LinearIndex(int x, int y, int z)
    {
	    return x + y * _SliceWidth + z * _SliceWidth * _SliceWidth;
    }

    int LinearIndex(int x, int y)
    {
	    return x + y * _SliceWidth;
    }

    void ClearVisited()
    {
	    int length = _SliceWidth * _SliceWidth;
	    for (int i = 0; i < length; i++)
	    {
		    visited2D[i] = false;
	    }
    }
        
    int GetCellID2D(int x, int y)
    {
	    return cellIDs2D[LinearIndex(x, y)];
    }
        
    bool IsSolid(int x, int y)
    {
	    int id = GetCellID2D(x, y);
	
	    for (int i = 0; i < _NonSolidCount; i++)
	    {
		    if (nonSolidVoxelIDs[i] == id)
		    {
			    return false;
		    }
	    }
	
	    return true;
    }
        
    bool Visited(int x, int y)
    {
	    return visited2D[LinearIndex(x, y)];
    }
        
    bool Visible(int x, int y)
    {
	    return visible2D[LinearIndex(x, y)];
    }
        
    bool InBounds2D(int x, int y)
    {
	    return x >= 0 && x < _SliceWidth && y >= 0 && y < _SliceWidth;
    }

    bool LCheck(int anchorX, int anchorY, int size)
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

        int anchorID = GetCellID2D(anchorX, anchorY);

        if (!InBounds2D(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
        {
            return false;
        }

        if(size < 1)
        {
            return false;
        }

        size = clamp(size, 1, MAX_START_END_SIZE);

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
            if(!InBounds2D(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID2D(x, y) != anchorID)
            {
                return false;
            }
        }

        //Right of L
        x = anchorX + size - 1;
        for (y = anchorY; y < anchorY + size - 1; y++)
        {
            //Is the current cell solid? & not been visited? and the same ID as the anchor?
            if (!InBounds2D(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID2D(x, y) != anchorID)
            {
                return false;
            }
        }


        return true;
    }

    bool HorizontalCheck(int anchorX, int anchorY, int size, int xWidth)
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

        int anchorID = GetCellID2D(anchorX, anchorY);            

        if (!InBounds2D(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
        {
            return false;
        }

        if (size < 1)
        {
            return false;
        }

        size = clamp(size, 1, MAX_START_END_SIZE);

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
            if (!InBounds2D(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID2D(x, y) != anchorID)
            {
                return false;
            }
        }

        return true;
    }

    bool VerticalCheck(int anchorX, int anchorY, int size, int yWidth)
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

        int anchorID = GetCellID2D(anchorX, anchorY);            

        if (!InBounds2D(anchorX, anchorY) || !IsSolid(anchorX, anchorY) || Visited(anchorX, anchorY) || !Visible(anchorX, anchorY))
        {
            return false;
        }

        if (size < 1)
        {
            return false;
        }

        size = clamp(size, 1, MAX_START_END_SIZE);

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
            if (!InBounds2D(x, y) || !IsSolid(x, y) || Visited(x, y) || !Visible(x, y) || GetCellID2D(x, y) != anchorID)
            {
                return false;
            }
        }


        return true;
    }

    StartEnd ExpandDiagonally(StartEnd startEnd)
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
	    startEnd.end = startEnd.start + int2(1, 1) * (lSize - 1);
    
	    return startEnd;
    }

    StartEnd ExpandVertically(StartEnd startEnd)
    {            
	    int2 startEndSize = GetSizeInts(startEnd);

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
        startEnd.end = int2(startEnd.end.x, startEnd.start.y + vSize - 1);
	    return startEnd;
    }

    StartEnd ExpandHorizontally(StartEnd startEnd)
    {
	    int2 startEndSize = GetSizeInts(startEnd);

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
        startEnd.end = int2(startEnd.start.x + hSize - 1, startEnd.end.y);
	    return startEnd;
    }

    void SetVisited(StartEnd startEnd)
    {
        for (int x = startEnd.start.x; x <= startEnd.end.x; x++)
        {
            for (int y = startEnd.start.y; y <= startEnd.end.y; y++)
            {
                visited2D[LinearIndex(x, y)] = true;
            }
        }
    }

    void PreformSlice()
    {
        //List<StartEnd> startEnds = new List<StartEnd>();

        //StartEnd currentStartEnd;
        for (int x = 0; x < _SliceWidth; x++)
        {
            for (int y = 0; y < _SliceWidth; y++)
            {
                //Is the current cell solid? & not been visited? and the same ID as the anchor?
                if (!IsSolid(x, y) || Visited(x, y) || !Visible(x, y))
                {
                    continue;
                }

			    StartEnd currentStartEnd;
			    currentStartEnd.start = int2(x, y);
			    currentStartEnd.end = int2(x, y);

			    currentStartEnd = ExpandDiagonally(currentStartEnd);

			    currentStartEnd = ExpandVertically(currentStartEnd);

			    currentStartEnd = ExpandHorizontally(currentStartEnd);

                SetVisited(currentStartEnd);

                //startEnds.Add(currentStartEnd);
            }
        }

        //return startEnds;
    }               

#endif