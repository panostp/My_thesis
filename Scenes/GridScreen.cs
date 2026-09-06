using Godot;
using System;

public partial class GridScreen : Control
{
     const int GRID_SIZE_X = 100;
    const int GRID_SIZE_Y = 50;
    [Export] private int CELL_SIZE = 10; // in mm
    [Export] private int CanvasWidth = 512;
    [Export] private int CanvasHeight = 512;
    [Export] private int laserLength = 50; // in mm

    private Vector2 carPos = Vector2.Zero;
    private float carAngle = 0f;
    private Vector2 middleScreen;
    private float scanAngle = 0f;

    // chunks =========
    const int chunkSize = 32;
    private ChunckContainer chunkContainer;
    // chunks =========

    public override void _Ready()
    {
        carPos = Vector2.Zero;
        middleScreen = findMiddleScreen();
        chunkContainer = GetNodeOrNull<ChunckContainer>("ChunkContainer");
        if (chunkContainer == null)
        {
            chunkContainer = new ChunckContainer { ChunkSize = chunkSize, Name = "ChunkContainer" };
            AddChild(chunkContainer);
        }
        chunkContainer.SyncCellPixelSize(CELL_SIZE);
    }

    public override void _Process(double delta)
    {
        HandleInput();
        QueueRedraw();
    }

    private Vector2 findMiddleScreen()
    {
        return GetViewportRect().Size / 2;
    }

    private void HandleInput()
    {
        // Movement
        Vector2 tempPos = carPos;
        Vector2 move = Vector2.Zero;

        if (Input.IsActionPressed("ui_up")) move.Y -= 1;
        if (Input.IsActionPressed("ui_down")) move.Y += 1;
        if (Input.IsActionPressed("ui_left")) move.X -= 1;
        if (Input.IsActionPressed("ui_right")) move.X += 1;

        tempPos += move * 0.5f;
        tempPos = chunkContainer.ClampToBounds(tempPos); 

        if (Input.MouseMode == Input.MouseModeEnum.Visible && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = GetLocalMousePosition() - middleScreen;

            int gxs = (int)carPos.X;
            int targetX = gxs + (int)(mousePos.X / CELL_SIZE);
            int gys = (int)carPos.Y;
            int targetY = gys + (int)(mousePos.Y / CELL_SIZE);

            SetCellWorld(new Vector2(targetX, targetY), 2); // Mark clicked cell as obstacle
            return;
        }
        int gx = (int)tempPos.X;
        int gy = (int)tempPos.Y;
        if (GetCellWorld(new Vector2(gx, gy)) != 2)
        {
            carPos = tempPos;
            SetCellWorld(new Vector2(gx, gy), 1);
        }
    }

    public override void _Draw()
    {
        DrawGrid();
        DrawCar();
    }

    private void DrawGrid()
    {
        foreach (var kvp in chunkContainer.Chunks)
        {
            Vector2I chunkPos = kvp.Key;
            int[,] chunk = kvp.Value;

            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    int cell = chunk[x, y];
                    if (cell == 0) continue;

                    Vector2 worldPos = new Vector2(chunkPos.X * chunkSize + x, chunkPos.Y * chunkSize + y);
                    Vector2 drawPos = middleScreen + (worldPos - carPos) * CELL_SIZE;

                    DrawRect(new Rect2(drawPos, Vector2.One * CELL_SIZE), getColor(cell));
                }
            }
        }
    }

    private Color getColor(int cell)
    {
        if (cell == 0)
            return new Color(0.0f, 0.0f, 0.0f); // not scanned
        else if (cell == 1)
            return new Color(1f, 1f, 1f); // free
        else if (cell == 2)
            return new Color(0f, 0f, 0f); // obstacle
        else
            return new Color(0f, 0f, 0f); // unknown
    }

    private void DrawCar()
    {
        Vector2 actualPos = GridToWorld(carPos);
        DrawCircle(actualPos, 5, new Color(1, 0, 0));

        float prevAngle = scanAngle;
        scanAngle += 5f;

        int subSteps = 50;
        for (int i = 0; i < subSteps; i++)
        {
            float t = i / (float)subSteps;
            float angle = Mathf.Lerp(prevAngle, scanAngle, t);
            ScanAtAngle(angle);
        }
    }

    private void ScanAtAngle(float angle)
    {
        float rad = Mathf.DegToRad(angle);
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        Vector2 pos = carPos;
        Vector2 lastPos = carPos;

        for (int i = 0; i < laserLength; i++) // max distance in cells
        {
            Vector2 p = pos + dir * i;
            lastPos = p;

            
            if (IsObstacle(p))
            {
                SetCellWorld(p, 2);
                break;
            }
            SetCellWorld(p, 1); // free
        }

        Vector2 start = GridToWorld(carPos);
        Vector2 end = GridToWorld(lastPos);
        DrawLine(start, end, Colors.Red, 2);
    }

    private bool IsObstacle(Vector2 pos)
    {
        return GetCellWorld(pos) == 2;
    }

    Vector2 GridToWorld(Vector2 gridPos)
    {
        Vector2 offset = middleScreen - carPos * CELL_SIZE;
        return offset + gridPos * CELL_SIZE;
    }

    private float CastRay(float angleDeg)
    {
        Vector2 dir = new Vector2(1, 0).Rotated(Mathf.DegToRad(angleDeg));

        int maxSteps = laserLength; // in cells
        float stepSize = 1f;

        for (float d = 0; d < maxSteps; d += stepSize)
        {
            Vector2 worldPos = carPos + dir * (d / CELL_SIZE);
            if (GetCellWorld(worldPos) == 2)
                return d;
        }

        return maxSteps;
    }

    private Vector2 scanLine()
    {
        Vector2 dir = new Vector2(1, 0).Rotated(Mathf.DegToRad(scanAngle));
        return dir * laserLength;
    }

    // deprecated but kept working against chunk storage
    private void scanForObstacles()
    {
        Vector2 mousePos = scanLine();
        int gx = (int)carPos.X;
        int gy = (int)carPos.Y;

        int targetX = gx + (int)Mathf.Round(mousePos.X / CELL_SIZE);
        int targetY = gy + (int)Mathf.Round(mousePos.Y / CELL_SIZE);

        int dx = targetX - gx;
        int dy = targetY - gy;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int scanX = gx + (int)Mathf.Round(dx * t);
            int scanY = gy + (int)Mathf.Round(dy * t);

            Vector2 cell = new Vector2(scanX, scanY);
            if (GetCellWorld(cell) != 2)
                SetCellWorld(cell, 1);
        }
    }

    private void SetCellWorld(Vector2 worldPos, int value)
    {
        chunkContainer.SetCell(worldPos, value);
    } 

    private int GetCellWorld(Vector2 worldPos)
    {
        return chunkContainer.GetCell(worldPos);
    }
     

    // ===== External API =====

    void ProcessSensor(float robotX, float robotY, float angle, float distance)
    {
        float rad = Mathf.DegToRad(angle);

        // mark free space
        for (float d = 0; d < distance; d += 1f)
        {
            float x = robotX + d * Mathf.Cos(rad);
            float y = robotY + d * Mathf.Sin(rad);
            SetCellWorld(new Vector2(x, y), 1);
        }

        // mark obstacle
        float ox = robotX + distance * Mathf.Cos(rad);
        float oy = robotY + distance * Mathf.Sin(rad);
        SetCellWorld(new Vector2(ox, oy), 2);
    }

    public void SetCell(int x, int y, int value)
    {
        SetCellWorld(new Vector2(x, y), value);
    }

    public void SetCar(float x, float y, float angle)
    {
        carPos = new Vector2(x, y);
        carAngle = angle;
    }

    public void AddObstacle(int x, int y)
    {
        SetCell(x, y, 2);
    }

    public void OnSensorData(float x, float y, float angle, float distance)
    {
        float angleRad = Mathf.DegToRad(angle);
        float endX = x + distance * Mathf.Cos(angleRad);
        float endY = y + distance * Mathf.Sin(angleRad);
        int gridX = (int)Mathf.Round(endX);
        int gridY = (int)Mathf.Round(endY);
        GD.Print($"Adding obstacle at: {gridX}, {gridY}");
        SetCell(gridX, gridY, 2);
    }

    private void on_websocket_esp_32_data(float x, float y, float angle, float distance)
    {
        float angleRad = Mathf.DegToRad(angle);
        float endX = carPos.X + distance * Mathf.Cos(angleRad);
        float endY = carPos.Y + distance * Mathf.Sin(angleRad);
        int gridX = (int)Mathf.Round(endX);
        int gridY = (int)Mathf.Round(endY);
        GD.Print($"Adding obstacle at: {gridX}, {gridY}");
        SetCell(gridX, gridY, 2);
    }
}
