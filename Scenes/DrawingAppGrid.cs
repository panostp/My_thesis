using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


public partial class DrawingAppGrid : Control
{
	[Export] private int CELL_SIZE = 10; // in mm
	[Export] private int CanvasWidth = 512;
	[Export] private int CanvasHeight = 512;
	[Export] private int laserLength = 50; // in mm         
	[Export] private float maxSteerDeg = 45f;
	[Export] private float MM_PER_CELL = 10f;//one cell = 1cm
	[Export] private float maxSensorRangeMM = 1300f; 
	private Vector2 carPos = Vector2.Zero;
	private float carAngle = 0f;
	private Vector2 middleScreen;
	private float scanAngle = 0f;
	private bool rightClickHeld = false;

	// chunks =========
	const int chunkSize = 32;
	private ChunckContainer chunkContainer;
	private bool drawBoundsOnce = true;
	// chunks =========
	//camera==========
	private Vector2 viewCenter = Vector2.Zero;
	private bool isDragging = false;
	private Vector2 dragStartMouse;
	private Vector2 dragStartViewCenter;
	//camera==========
	//pathfinding==========
	private Node2D pathFinder;
	private GodotObject catmullRomGrid;
	private bool once = true;
	List<Vector2> lastPathGridPoints = new List<Vector2>();
	private List<Vector2> currentSegmentTargets = new List<Vector2>();
	private int animSegmentIndex = 0;
	//List<Vector3> ControlPoints = new List<Vector3>();
	private Line2D lines;
	//car moving animation
	private Boolean CarMoving = false;
	[Export] private float pathFollowSpeed = 33f;
	private int pathIndex = 0;
	//Godot.Collections.Array<Vector2> CurrentPath = new();
	// test===========
	private bool isReplanning = false;
	private float replanCooldown = 0f;
	private const float CoolDown = 0.5f;   // min seconds between replans
	private const int PenaltyReplan = 10;
	private const int LookAhed = 45; 
	private float angle = 0f; 
	private Vector2 laserEnd = Vector2.Zero;
	//==================
	//exploring==========
	private bool isExploring = false;
	private Frontier Frontier;
	private float carHeadingDeg = 0f; 
	private float turnRateDegPerSec = 120f; 
	private GodotObject dubinsPath;
	private WebSocket webSocketNode;
	private bool Explore =false;
	//websocket
	private WebSocketMananger WebConnection;
	//look
	private Label Wrong;

	

	public override void _Ready()
	{
		//carPos = Vector2.Zero;
		middleScreen = findMiddleScreen();
		chunkContainer = GetNodeOrNull<ChunckContainer>("ChunkContainer");
		if (chunkContainer == null)
		{
			chunkContainer = new ChunckContainer { ChunkSize = chunkSize, Name = "ChunkContainer" };
			AddChild(chunkContainer);
		}
		chunkContainer.SyncCellPixelSize(CELL_SIZE);
		carPos = chunkContainer.Bounds.Position + chunkContainer.Bounds.Size / 2; 
		viewCenter = carPos;
		pathFinder = GetNode<Node2D>("PathFinding");
		catmullRomGrid = GetNodeOrNull<Node2D>("CatmullRom");   
		Frontier = GetNode<Frontier>("Frontier");
		lines = GetNode<Line2D>("Line2D");
		dubinsPath = GetNodeOrNull<Node2D>("DubinsPath");
		Wrong =GetNode<Label>("Wrong");
		WebConnection = GetNode<WebSocketMananger>("/root/WebSocketMananger");
		WebConnection.ESP32Data += OnESP32Data;
		WebConnection.ESP32Stop += OnESP32Stop;
		WebConnection.SegmentComplete += OnSegmentComplete;
		Wrong.Visible=false;
		 
	}

	public override void _Process(double delta)
	{
		HandleInput();
		QueueRedraw();
		if(CarMoving)
		{
			FollowPath(delta);
		}
		else if (Explore)
			ExplorationStep();

	}

	private Vector2 findMiddleScreen()
	{
		return GetViewportRect().Size / 2;
	}

	private void HandleInput()
	{
		// Movement
		HandleCameraDrag();
		Vector2 tempPos = carPos;
		Vector2 move = Vector2.Zero;

		if (Input.IsActionPressed("ui_up")) move.Y -= 1;
		if (Input.IsActionPressed("ui_down")) move.Y += 1;
		if (Input.IsActionPressed("ui_left")) move.X -= 1;
		if (Input.IsActionPressed("ui_right")) move.X += 1;
		if (Input.IsActionPressed("ui_accept"))
		{
			Vector2 worldClick = ScreenToWorld(GetLocalMousePosition());
			SetCellWorld(new Vector2((int)worldClick.X, (int)worldClick.Y), 2);
		}

		tempPos += move * 0.5f;
		chunkContainer.ExpandBoundsToInclude(tempPos);
		tempPos = chunkContainer.ClampToBounds(tempPos); 

		if (Input.MouseMode == Input.MouseModeEnum.Visible && Input.IsMouseButtonPressed(MouseButton.Right) &&!rightClickHeld )
		{
			Vector2 worldClick = ScreenToWorld(GetLocalMousePosition());
			worldClick = chunkContainer.ClampToBounds(worldClick);
			rightClickHeld = true; 
			//SetCellWorld(new Vector2((int)worldClick.X, (int)worldClick.Y), 2);
			if(GetPenaltyAt(worldClick)<=0)
				pathFinding(worldClick);
			else
				OnError();
			return;
		}
		else if (!Input.IsMouseButtonPressed(MouseButton.Right))
		{
			rightClickHeld = false; 
		}
		int gx = (int)tempPos.X;
		int gy = (int)tempPos.Y;
		if (GetCellWorld(new Vector2(gx, gy)) != 2)
		{
			carPos = tempPos;
			SetCellWorld(new Vector2(gx, gy), 1);
		}
	}
	private void HandleCameraDrag()
	{
		if (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			Vector2 mouseScreen = GetLocalMousePosition();
			if (!isDragging)
			{
				isDragging = true;
				dragStartMouse = mouseScreen;
				dragStartViewCenter = viewCenter;
			}
			else
			{
				Vector2 screenDelta = mouseScreen - dragStartMouse;
				viewCenter = dragStartViewCenter - screenDelta / CELL_SIZE;
				viewCenter = chunkContainer.ClampToBounds(viewCenter); 
			}
			UpdatePathLine();
		}
		else
		{
			isDragging = false;
		}
	}


	public override void _Draw()
	{
		DrawGrid();
		DrawCar();
		if(drawBoundsOnce)
		{
			drawBoundsOnce = false;
			drawBackground();
		}
		
		
	}
	private void UpdatePathLine()
	{
		lines.ClearPoints();
	foreach (var gridPoint in lastPathGridPoints)
		lines.AddPoint(GridToWorld(gridPoint));
	}
/*
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
					//if (cell == 0) continue;

					Vector2 worldPos = new Vector2(chunkPos.X * chunkSize + x, chunkPos.Y * chunkSize + y);
					Vector2 drawPos = middleScreen + (worldPos - carPos) * CELL_SIZE;

					DrawRect(new Rect2(drawPos, Vector2.One * CELL_SIZE), getColor(cell));
				}
			}
		}
	}*/
	private void DrawGrid()
	{
		drawBackground();
		int chunkSizeCells = chunkContainer.ChunkSize;
		Vector2 halfViewCells = middleScreen / CELL_SIZE;
		Vector2 worldMin = viewCenter - halfViewCells;
		Vector2 worldMax = viewCenter + halfViewCells;
 
		Vector2I minChunk = chunkContainer.WorldToChunk(worldMin);
		Vector2I maxChunk = chunkContainer.WorldToChunk(worldMax);
		for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
		{
			for (int cy = minChunk.Y; cy <= maxChunk.Y; cy++)
			{
				Vector2I chunkPos = new Vector2I(cx, cy);
				if (!chunkContainer.TryGetChunk(chunkPos, out int[,] chunk))
					continue;
 
				for (int x = 0; x < chunkSizeCells; x++)
				{
					for (int y = 0; y < chunkSizeCells; y++)
					{
						int cell = chunk[x, y];
						if (cell == 0) //to save performance
							continue;
 
						Vector2 worldPos = new Vector2(chunkPos.X * chunkSizeCells + x, chunkPos.Y * chunkSizeCells + y);
						Vector2 drawPos = middleScreen + (worldPos - viewCenter) * CELL_SIZE;

						DrawRect(new Rect2(drawPos, Vector2.One * CELL_SIZE), getColor(cell, worldPos));
					}
				}
			}
		}
	}
	private void drawBackground()
	{
		Rect2I bounds = chunkContainer.Bounds;
		Vector2 topLeft = GridToWorld(new Vector2(bounds.Position.X, bounds.Position.Y));
		Vector2 sizePixels = new Vector2(bounds.Size.X, bounds.Size.Y) * CELL_SIZE;
		DrawRect(new Rect2(topLeft, sizePixels), new Color(0.2f, 0.2f, 0.2f));
	}

	private Color getColor(int cell, Vector2 worldpos)
	{   
		//if (cell == 0)
		//{
			
			//
			
			// if(GetPenaltyAt(worldpos) >1 )
			// {
			//     return new Color(0.0f, 1.0f, 0.0f); 
			// }
			//else
			   // return new Color(0.0f, 0.0f, 0.0f); // not scanned
	   // }   
		if (cell == 1)
		{
			int penalty = GetPenaltyAt(worldpos);
			if (penalty > 0)
			{
				float t = Mathf.Clamp(penalty / 200f, 0f, 1f); 
				
				return new Color(0f, 1f-t, 1f); 
			}
			return new Color(1f, 1f, 1f); // free
		}     
		else if (cell == 2)
			return new Color(0f, 0f, 0f); // obstacle
		else if (cell == 3)
			return new Color(0f, 1f, 0f); // path
		else
			return new Color(1f, 0f, 0f); // unknown
	}

	private void DrawCar()
	{
		Vector2 actualPos = GridToWorld(carPos);
		DrawPolygon(new Vector2[] {
			actualPos + new Vector2(30, 0).Rotated(Mathf.DegToRad(carAngle)),
			actualPos + new Vector2(-30, 25).Rotated(Mathf.DegToRad(carAngle)),
			actualPos + new Vector2(-30, -25).Rotated(Mathf.DegToRad(carAngle))
		}, new Color[] { new Color(255, 165, 0) }); 
		//DrawCircle(actualPos, 20, new Color(255, 165, 0));
		ScanAtAngle();
		/*
		float prevAngle = scanAngle;
		scanAngle += 5f;
		int subSteps = 50;
		for (int i = 0; i < subSteps; i++)
		{
			float t = i / (float)subSteps;
			float angles = Mathf.Lerp(prevAngle, scanAngle, t);
			ScanAtAngle(angles); //change to angle withouts s 
		}*/
	}
	private float Distance(Vector2 a, Vector2 b)
	{
		return (float)Math.Sqrt(Math.Pow(b.X - a.X,2) + Math.Pow(b.Y - a.Y,2));
	}

private void ScanAtAngle()
{
	Vector2 pos = carPos;
	Vector2 toEnd = laserEnd - carPos;
	laserLength = (int)toEnd.Length();
	Vector2 dir = laserLength > 0 ? toEnd / laserLength : Vector2.Zero;
	Vector2 lastPos = carPos;

	for (int i = 0; i <= laserLength; i++)
	{
		Vector2 p = pos + dir * i;
		lastPos = p;
		p = chunkContainer.ClampToBounds(p);
		if (GetCellWorld(p) == 2)
		{
			SetCellWorld(p, 2);
			break;
		}
		SetCellWorld(p, 1);
	}

	Vector2 start = GridToWorld(carPos);
	Vector2 end = GridToWorld(laserEnd);
	chunkContainer.ExpandBoundsToInclude(lastPos);
	DrawLine(start, end, Colors.Red, 2);
}

	private bool IsObstacle(Vector2 pos)
	{
		return GetCellWorld(pos) == 2;
	}

	Vector2 GridToWorld(Vector2 gridPos)
	{
		Vector2 offset = middleScreen - viewCenter * CELL_SIZE;
		return offset + gridPos * CELL_SIZE;
	}

	Vector2 ScreenToWorld(Vector2 screenPos)
	{
		return viewCenter + (screenPos - middleScreen) / CELL_SIZE;
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
	private int FindClosestPathIndex(Vector2 point, int searchFrom)
	{
		if (lastPathGridPoints.Count == 0) return 0;
		int best = Math.Clamp(searchFrom, 0, lastPathGridPoints.Count - 1);
		float bestDist = float.MaxValue;
		for (int n = searchFrom; n < lastPathGridPoints.Count; n++)
		{
			float d = (lastPathGridPoints[n] - point).LengthSquared();
			if (d < bestDist) { bestDist = d; best = n; }
			else if (d > bestDist * 4f) break; // already well past the closest point
		}
		return best;
	}


	private void FollowPath(double delta)
	{
		if (animSegmentIndex >= currentSegmentTargets.Count)
		{
			CarMoving = false;
			return;
		}

		if (replanCooldown > 0f)
			replanCooldown -= (float)delta;

		if (!isReplanning && replanCooldown <= 0f && CheckPathBlocked())
		{
			isReplanning = true;
			replanCooldown = CoolDown;
			Vector2 goal = lastPathGridPoints[lastPathGridPoints.Count - 1];
			pathFinding(goal);
			isReplanning = false;
			return;
		}

		Vector2 target = currentSegmentTargets[animSegmentIndex];
		Vector2 toTarget = target - carPos;
		float distance = toTarget.Length();

		const float arrivalEpsilon = 0.05f;
		if (distance <= arrivalEpsilon)
		{
			carPos = target;
			return; 
		}

		float desiredHeadingDeg = Mathf.RadToDeg(Mathf.Atan2(toTarget.Y, toTarget.X));
		float angleDiff = Mathf.Wrap(desiredHeadingDeg - carHeadingDeg, -180f, 180f);
		float maxTurnThisFrame = turnRateDegPerSec * (float)delta;
		float clampedTurn = Mathf.Clamp(angleDiff, -maxSteerDeg, maxSteerDeg);
		clampedTurn = Mathf.Clamp(clampedTurn, -maxTurnThisFrame, maxTurnThisFrame);
		carHeadingDeg += clampedTurn;
		carAngle = carHeadingDeg;

		float step = pathFollowSpeed * (float)delta;
		carPos += (distance <= step) ? toTarget : toTarget.Normalized() * step;

		chunkContainer.ExpandBoundsToInclude(carPos);
		SetCellWorld(new Vector2((int)carPos.X, (int)carPos.Y), 1);
	}
	private bool CheckPathBlocked()
	{
		int end = Math.Min(pathIndex + LookAhed, lastPathGridPoints.Count);
		for (int n = pathIndex; n < end; n++)
		{
			Vector2 tt = lastPathGridPoints[n];
			if (GetCellWorld(tt) == 2 || GetPenaltyAt(tt) > PenaltyReplan)//10
			{
				return true;
			}
		}
		return false;
	}
	private void TriggerReplan()
	{
		if (isReplanning) 
			return;
		isReplanning = true;
		replanCooldown = CoolDown;
		Vector2 goal = lastPathGridPoints[lastPathGridPoints.Count - 1];
		CarMoving = false; 
		pathFinding(goal);  
		isReplanning = false;
	}
	private int GetPenaltyAt(Vector2 worldPos)
	{
		Vector2I localPos = chunkContainer.WorldToLocal(worldPos);
		int[,] pChunk = chunkContainer.getPenelty(worldPos);
		return pChunk[localPos.X, localPos.Y];
	}

	// deprecated
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
	private int distance(Vector2 a, Vector2 b)
	{
		return (int)Mathf.Round((a - b).Length());
	}
	private void pathFinding(Vector2 goal)
	{
		//CurrentPath.Clear();
		pathIndex=0;
		//Godot.Collections.Array<int> flat = new();
		int chunkSizeCells = chunkContainer.ChunkSize;
		var chunksDict = chunkContainer.ToChunksDictionary();
		var penaltyChunksDict = chunkContainer.ToPenaltyChunksDictionary();
		Vector2 start = carPos;
		var path = new Godot.Collections.Array(); 
		path =  (Godot.Collections.Array)pathFinder.Call("findPath", start, goal, chunksDict, penaltyChunksDict, chunkSizeCells, carHeadingDeg);
		var Xpoints = new Godot.Collections.Array();
		var Ypoints = new Godot.Collections.Array();
		var Result = new Godot.Collections.Array();
		var angles = new Godot.Collections.Array();
		lines.ClearPoints();
		foreach (Vector2 pat in path)
		{
			Xpoints.Add((double)pat.X);
			Ypoints.Add((double)pat.Y);
			
			//SetCellWorld(new Vector2((int)pat.X, (int)pat.Y), 3);
		}
		lines.DefaultColor = Colors.Orange;
		//Result = (Godot.Collections.Array)catmullRomGrid.Call("createSpline",Xpoints,Ypoints, true);

		float controlDistance = 20f; // tune
		Result = (Godot.Collections.Array)catmullRomGrid.Call("createSplineWithHeading", Xpoints, Ypoints, true, carHeadingDeg, controlDistance);
		lastPathGridPoints.Clear();
		foreach (Vector2 point in Result)
		{
			lastPathGridPoints.Add(point);
			//SetCellWorld(new Vector2((int)point.X, (int)point.Y), 3);
		}
		angles = (Godot.Collections.Array)catmullRomGrid.Call("returnControlPoints");
		//ControlPoints.Clear();
		List<Vector2> controlPoints2D = new List<Vector2>();
		currentSegmentTargets.Clear();
		animSegmentIndex = 0;
		for(int i = 0; i < angles.Count; i++)
		{
			Vector3 point = angles[i].AsVector3();
			currentSegmentTargets.Add(new Vector2(point.X, point.Y));

			if(i==0)
			{
				float distance = Distance(carPos, new Vector2(point.X, point.Y));
				GD.Print("Distance from car to first control point: " + distance);
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
			else
			{
				float distance = Distance(new Vector2(angles[i-1].AsVector3().X, angles[i-1].AsVector3().Y), new Vector2(point.X, point.Y));
				GD.Print("Distance between control point " + (i-1) + " and control point " + i + ": " + distance);
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
		}	
		WebConnection.SendControlPoints(controlPoints2D);
		CarMoving= true;
		UpdatePathLine();
	}

	private void ExplorationStep()
	{
		if (CarMoving) 
			return; 
		var goal = Frontier.PickExplorationGoal(chunkContainer, carPos, carHeadingDeg, 45f);
		if (goal == null)
		{
			GD.Print("Exploration complete — no more frontiers.");
			isExploring = false;
			return;
		}
		pathFinding(goal.Value);
	}
	void on_button_pressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
	private async void OnError()
	{
		Wrong.Visible=true;
		await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);
		Wrong.Visible=false;
	}
	// =====API STUFF=====

	void ProcessSensor(float robotX, float robotY, float angle, float distance)
	{
		float rad = Mathf.DegToRad(angle);
		for (float d = 0; d < distance; d += 1f)
		{
			float x = robotX + d * Mathf.Cos(rad);
			float y = robotY + d * Mathf.Sin(rad);
			SetCellWorld(new Vector2(x, y), 1);
		}
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

	public void OnSensorData(float x, float y, float angle, float distance)//not used
	{
		float angleRad = Mathf.DegToRad(angle);
		float endX = x + distance * Mathf.Cos(angleRad);
		float endY = y + distance * Mathf.Sin(angleRad);
		int gridX = (int)Mathf.Round(endX);
		int gridY = (int)Mathf.Round(endY);
		GD.Print($"Adding obstacle at: {gridX}, {gridY}");
		SetCell(gridX, gridY, 2);
	}

private void OnESP32Data(int angleA, float distanceA)
	{
		float worldAngleA = carHeadingDeg + angleA;
		float angleRadA = Mathf.DegToRad(worldAngleA);
		this.angle = worldAngleA; 

		if (distanceA >= 0)
		{
			float distanceCellsA = distanceA / MM_PER_CELL;
			float endXA = carPos.X + distanceCellsA * Mathf.Cos(angleRadA);
			float endYA = carPos.Y + distanceCellsA * Mathf.Sin(angleRadA);
			int gridXA = (int)Mathf.Round(endXA);
			int gridYA = (int)Mathf.Round(endYA);
			laserEnd = new Vector2(gridXA, gridYA);
			SetCell(gridXA, gridYA, 2);
		}
		else
		{
			float distanceCellsA = maxSensorRangeMM / MM_PER_CELL;
			float endXA = carPos.X + distanceCellsA * Mathf.Cos(angleRadA);
			float endYA = carPos.Y + distanceCellsA * Mathf.Sin(angleRadA);
			int gridXA = (int)Mathf.Round(endXA);
			int gridYA = (int)Mathf.Round(endYA);
			laserEnd = new Vector2(gridXA, gridYA);
		}
	   
	}
	private void OnESP32Stop()
	{
		lastPathGridPoints.Clear();
		OnError();
	}
	private void OnSegmentComplete(int index, bool pathComplete)
	{
		if (index >= 0 && index < currentSegmentTargets.Count)
		{
			carPos = currentSegmentTargets[index];
			chunkContainer.ExpandBoundsToInclude(carPos);
			pathIndex = FindClosestPathIndex(carPos, pathIndex);
		}

		animSegmentIndex = index + 1;

		if (pathComplete || animSegmentIndex >= currentSegmentTargets.Count)
		{
			CarMoving = false;
			lastPathGridPoints.Clear();
		}
	}
   
}
