using Godot;
using System;
using System.Collections.Generic;
public partial class ChunckContainer : Control
{
    [Export] public int ChunkSize = 32;//cells
    [Export] public bool UseBounds = true;
    [Export] public int CellPixelSize = 1;
    [Export] public Rect2I Bounds = new Rect2I(-250, -250, 200, 200); // position (x,y) + size (width,height), in cells
    [Export] public int maxChunks = 100;
    [Export] public int PenaltyRadius = 40; 
    [Export] public int baseValue = 100; 


    private readonly Dictionary<Vector2I, int[,]> chunks = new Dictionary<Vector2I, int[,]>();
    private readonly Dictionary<Vector2I, int[,]> penaltyChunks = new Dictionary<Vector2I, int[,]>();
    public IReadOnlyDictionary<Vector2I, int[,]> Chunks => chunks;
    public IReadOnlyDictionary<Vector2I, int[,]> PenaltyChunks => penaltyChunks;
    

    
    public override void _Ready()
    {
        Resized += RecomputeBoundsFromSize;
        RecomputeBoundsFromSize();
    }
    /*public override Vector2 _GetMinimumSize()
    {
        float minSide = ChunkSize * CellPixelSize;
        return new Vector2(minSide, minSide);
    }*/
/*
    private void RecomputeBoundsFromSize()
    {
        if (Size.X <= 0 || Size.Y <= 0 || CellPixelSize <= 0)
            return; 
 
        int widthCells = Mathf.Max(1, Mathf.RoundToInt(Size.X / CellPixelSize));
        int heightCells = Mathf.Max(1, Mathf.RoundToInt(Size.Y / CellPixelSize));
 
        Bounds = new Rect2I(-widthCells / 2, -heightCells / 2, widthCells, heightCells);
        UseBounds = true;
    }*/
    private void RecomputeBoundsFromSize()
    {
        if (CellPixelSize <= 0)
            return;
 
        int widthCells = Mathf.Max(ChunkSize, Mathf.RoundToInt(Size.X / CellPixelSize));
        int heightCells = Mathf.Max(ChunkSize, Mathf.RoundToInt(Size.Y / CellPixelSize));
 
        Bounds = new Rect2I(-widthCells / 2, -heightCells / 2, widthCells, heightCells);
        UseBounds = true;
    }
    public void ExpandBoundsToInclude(Vector2 pos, int expandChunks = 2)
    {
        if (!UseBounds || maxChunks<getChunkCount())
            return;
 
        int minX = Bounds.Position.X;
        int minY = Bounds.Position.Y;
        int maxX = Bounds.Position.X + Bounds.Size.X - 1;
        int maxY = Bounds.Position.Y + Bounds.Size.Y - 1;
        int growCells = expandChunks * ChunkSize;
        bool changed = false;
 
        if (pos.X <= minX +20) 
            {minX -= growCells; changed = true;}
        if (pos.X >= maxX -20) 
            {maxX += growCells; changed = true;}
        if (pos.Y <= minY +20) 
            {minY -= growCells; changed = true;} 
        if (pos.Y >= maxY -20) 
            maxY += growCells; changed = true;
        if (changed)
            Bounds = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    public Vector2I WorldToChunk(Vector2 worldPos)
    {
        Vector2I cell = worldToCell(worldPos);
        return new Vector2I(Mathf.FloorToInt(cell.X / (float)ChunkSize), Mathf.FloorToInt(cell.Y / (float)ChunkSize));
    }
    public Vector2I worldToCell(Vector2 worldPos)
    {
        return new Vector2I(Mathf.FloorToInt(worldPos.X), Mathf.FloorToInt(worldPos.Y));
    }
    public Vector2I WorldToLocal(Vector2 worldPos)
    {
        Vector2I cell = worldToCell(worldPos);
        int x = Mathf.PosMod(cell.X, ChunkSize);
        int y = Mathf.PosMod(cell.Y, ChunkSize);
        return new Vector2I(x, y);
    }

    private int[,] GetOrCreateChunk(Vector2I chunkPos)
    {
        if (!chunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new int[ChunkSize, ChunkSize];
            chunks[chunkPos] = chunk;
        }
        return chunk;
    }
    private int[,] GetOrCreatePenaltyChunk(Vector2I chunkPos)
    {
        if (!penaltyChunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new int[ChunkSize, ChunkSize];
            penaltyChunks[chunkPos] = chunk;
        }
        return chunk;
    }

    public bool IsWithinBounds(Vector2 worldPos)
    {
        if (!UseBounds)
            return true;

        //return Bounds.HasPoint((Vector2I)worldPos);
        return Bounds.HasPoint(worldToCell(worldPos));
    }
    public Vector2 ClampToBounds(Vector2 worldPos)
    {
        if (!UseBounds)
            return worldPos;

        float minX = Bounds.Position.X;
        float minY = Bounds.Position.Y;
        float maxX = Bounds.Position.X + Bounds.Size.X - 1;
        float maxY = Bounds.Position.Y + Bounds.Size.Y - 1;

        return new Vector2(Mathf.Clamp(worldPos.X, minX, maxX), Mathf.Clamp(worldPos.Y, minY, maxY));
    }
    public void SyncCellPixelSize(int cellPixelSize)
    {
        CellPixelSize = cellPixelSize;
        RecomputeBoundsFromSize();
    }


    public void SetCell(Vector2 worldPos, int value)
    {
        if (!IsWithinBounds(worldPos))
            return; 

        Vector2I chunkPos = WorldToChunk(worldPos);
        Vector2I localPos = WorldToLocal(worldPos);

        int[,] chunk = GetOrCreateChunk(chunkPos);
        chunk[localPos.X, localPos.Y] = value;
        if (value == 2) 
        {
            addPenalty(worldPos,PenaltyRadius);
        }
        else if (value==4)
        {
            value = 2;
            chunk[localPos.X, localPos.Y] = value;
        }
    }
    public int GetCell(Vector2 worldPos)
    {
        Vector2I chunkPos = WorldToChunk(worldPos);
        if (!chunks.TryGetValue(chunkPos, out var chunk))
            return 0;

        Vector2I localPos = WorldToLocal(worldPos);
        return chunk[localPos.X, localPos.Y];
    }

    public bool TryGetChunk(Vector2I chunkPos, out int[,] chunk)
    {
        return chunks.TryGetValue(chunkPos, out chunk);
    }
    public int getChunkCount()
    {
        return chunks.Count;
    }

    public void Clear()
    {
        chunks.Clear();
    }
    public Godot.Collections.Dictionary ToChunksDictionary()
    {
        var dict = new Godot.Collections.Dictionary();
 
        foreach (var kvp in chunks)
        {
            Vector2I chunkPos = kvp.Key;
            int[,] chunk = kvp.Value;
 
            var flat = new int[ChunkSize * ChunkSize];
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    flat[x * ChunkSize + y] = chunk[x, y];
                }
            }
 
            dict[chunkPos] = new Godot.Collections.Array<int>(flat);
        }
 
        return dict;
    }
    public Godot.Collections.Dictionary ToPenaltyChunksDictionary()
    {
        var dict = new Godot.Collections.Dictionary();
 
        foreach (var kvp in penaltyChunks)
        {
            Vector2I chunkPos = kvp.Key;
            int[,] chunk = kvp.Value;
 
            var flat = new int[ChunkSize * ChunkSize];
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    flat[x * ChunkSize + y] = chunk[x, y];
                }
            }
 
            dict[chunkPos] = new Godot.Collections.Array<int>(flat);
        }
 
        return dict;
    }

    private void addPenalty(Vector2 obstacleWorldPos, int radius )
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0) 
                    continue;
                Vector2 neighborWorld = obstacleWorldPos + new Vector2(dx, dy);
                if (!IsWithinBounds(neighborWorld)) 
                {
                    continue;
                }
                double d2 = Math.Sqrt(dx * dx + dy * dy);
                int penalty = baseValue / (int)(d2);
               // int penalty = (int)(d2+baseValue);
                if (penalty <= 0) 
                {
                    continue;
                }
                

                Vector2I chunkPos = WorldToChunk(neighborWorld);
                Vector2I localPos = WorldToLocal(neighborWorld);
                int[,] pChunk = GetOrCreatePenaltyChunk(chunkPos);
                //pChunk[localPos.X, localPos.Y] += penalty;
                if (penalty > pChunk[localPos.X, localPos.Y])
                {
                    pChunk[localPos.X, localPos.Y] = penalty;
                }
                    
                
            }
        }
    }
    /*
    private void addPenalty(Vector2 obstacleWorldPos, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                Vector2 neighborWorld = obstacleWorldPos + new Vector2(dx, dy);
                if (!IsWithinBounds(neighborWorld))
                    continue;

                Vector2 mirrorWorld = obstacleWorldPos + new Vector2(dx * 2, dy * 2);
                if (IsWithinBounds(mirrorWorld) &&
                    GetCell(mirrorWorld) == 2 &&
                    GetCell(neighborWorld) != 2)
                {
                    Vector2I chunkPosN = WorldToChunk(neighborWorld);
                    Vector2I localPosN = WorldToLocal(neighborWorld);
                    int[,] cellChunkN = GetOrCreateChunk(chunkPosN);
                    cellChunkN[localPosN.X, localPosN.Y] = 2;
                }
                double d2 = Math.Sqrt(dx * dx + dy * dy);
                int penalty = (int)(baseValue / d2);
                if (penalty <= 0)
                    continue;
                if (penalty > 80)
                    penalty = 1000;

                Vector2I chunkPos = WorldToChunk(neighborWorld);
                Vector2I localPos = WorldToLocal(neighborWorld);
                int[,] pChunk = GetOrCreatePenaltyChunk(chunkPos);
                if (penalty > pChunk[localPos.X, localPos.Y])
                    pChunk[localPos.X, localPos.Y] = penalty;
            }
        }
    }*/
    public int[,] getPenelty(Vector2 worldPos)
    {
        Vector2I chunkPos = WorldToChunk(worldPos); 
        return GetOrCreatePenaltyChunk(chunkPos);
    }
    public int getBaseValue()
    {
        return baseValue;
    }
}
