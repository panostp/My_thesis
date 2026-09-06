using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DrawAppMainGrid : Control
{
	[Export] private int CELL_SIZE = 10; // in mm
	[Export] private int CanvasWidth = 512;
	[Export] private int CanvasHeight = 512;
	[Export] private int laserLength = 50; // in mm         
	[Export] private float maxSteerDeg = 45f;
	[Export] private float MM_PER_CELL = 10f;

	private Vector2 carPos = Vector2.Zero;
	private float carAngle = 0f;
	private Vector2 middleScreen;
	private float scanAngle = 0f;

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
	//List<Vector3> ControlPoints = new List<Vector3>();
	private Line2D lines;
	//car moving animation
	private Boolean CarMoving = false;
	[Export] private float pathFollowSpeed = 30f;
	private int pathIndex = 0;
	//Godot.Collections.Array<Vector2> CurrentPath = new();
	// test===========
	private bool isReplanning = false;
	private float replanCooldown = 0f;
	private const float CoolDown = 0.5f;   // min seconds between replans
	private const int PenaltyReplan = 10;
	private const int LookAhed = 25; 
	private float angle = 0f; 
	private Vector2 laserEnd = Vector2.Zero;
	//==================
	//exploring==========
	private bool isExploring = false;
	private Frontier Frontier;
	private float carHeadingDeg = 0f; 
	private float turnRateDegPerSec = 120f; 
	private GodotObject dubinsPath;
	//private WebSocket webSocketNode;
	//drawing
	private List<Vector2> drawnPoints = new List<Vector2>();
	private bool isDrawingPath = true;
	private bool pathValidated = false;
	private Button btnDrawMode;
	private Button btnValidatePath;
	private Button btnStartCar;
	private bool rightClickHeld = false;
	private Line2D currentLine = null;
	private Line2D drawnLine;
	private bool isDrawing = false;
	private Line2D currentDrawLine;
	private const float MIN_SAMPLE_DIST_CELLS = 3f;
	private Button Check;
	List<Vector2> controlPoints2D = new List<Vector2>();
	private WebSocketMananger WebConnection;


	

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
		drawnLine = GetNode<Line2D>("lineDraw");
		dubinsPath = GetNodeOrNull<Node2D>("DubinsPath");
		//webSocketNode = GetNodeOrNull<WebSocket>("webSocket");
		Check = GetNode<Button>("TextureRect2/ColorRect/Check");
		WebConnection = GetNode<WebSocketMananger>("/root/WebSocketMananger");
		WebConnection.ESP32Data += OnESP32Data;
		//drawing
 
		
		//texRect = GetNode<TextureRect>("TextureRect");

	}

	public override void _Process(double delta)
	{
		HandleInput();
		QueueRedraw();
		if(CarMoving)
		{
			FollowPath(delta);
		}
		//else
			//ExplorationStep();

	}

	private Vector2 findMiddleScreen()
	{
		return GetViewportRect().Size / 2;
	}

	private void HandleInput()
	{
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

		if (Input.MouseMode == Input.MouseModeEnum.Visible && Input.IsMouseButtonPressed(MouseButton.Right) && !rightClickHeld)
		{
			rightClickHeld = true; // simple debounce so one click = one point, not one point per frame held
			Vector2 worldClick = ScreenToWorld(GetLocalMousePosition());
			worldClick = chunkContainer.ClampToBounds(worldClick);

		}

		int gx = (int)tempPos.X;
		int gy = (int)tempPos.Y;
		if (GetCellWorld(new Vector2(gx, gy)) != 2)
		{
			carPos = tempPos;
			SetCellWorld(new Vector2(gx, gy), 1);
		}
	}
	public override void _Input(InputEvent @event)
	{
		if (!isDrawingPath) return; 
		
		if (@event is InputEventMouseButton mouseEvent)
		{   
			if (mouseEvent.ButtonIndex == MouseButton.Right)
			{
				isDrawing = mouseEvent.Pressed;

				if (isDrawing)
				{
					drawnPoints.Clear();
					pathValidated = false;

					// if (currentDrawLine != null)
					//     currentDrawLine.QueueFree();

					// currentDrawLine = new Line2D
					// {
					//     Width = 3f,
					//     DefaultColor = new Color(1f, 1f, 0f) // yellow, matches "unvalidated" convention from earlier
					// };
					//AddChild(currentDrawLine);

					Vector2 worldPos = ScreenToWorld(mouseEvent.Position);
					worldPos = chunkContainer.ClampToBounds(worldPos);
					drawnPoints.Add(worldPos);
					//currentDrawLine.AddPoint(GridToWorld(worldPos));
				}
				else
				{
					GD.Print($"Stroke finished with {drawnPoints.Count} points.");
				}
			}
		}
		else if (@event is InputEventMouseMotion motionEvent && isDrawing)
		{
			Vector2 worldPos = ScreenToWorld(motionEvent.Position);
			worldPos = chunkContainer.ClampToBounds(worldPos);
			if (drawnPoints.Count == 0 || worldPos.DistanceTo(drawnPoints[drawnPoints.Count - 1]) >= MIN_SAMPLE_DIST_CELLS)
			{
				drawnPoints.Add(worldPos);
				//currentDrawLine.AddPoint(GridToWorld(worldPos));
			}
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
		if (isDrawingPath)
		{
			DrawWaypointMarkers();
		}
			
		
	}

	private void DrawWaypointMarkers()
	{
		for (int i = 0; i < drawnPoints.Count; i++)
		{
			Vector2 screenPos = GridToWorld(drawnPoints[i]);
			DrawCircle(screenPos, 10, new Color(0f, 0.5f, 1f)); 
			if (i > 0)
			{
				Vector2 prevScreenPos = GridToWorld(drawnPoints[i - 1]);
				DrawLine(prevScreenPos, screenPos, new Color(0f, 0.5f, 1f, 0.5f), 5f); 
			}
		}
	}

	private void UpdatePathLine()
	{
		lines.ClearPoints();
	foreach (var gridPoint in lastPathGridPoints)
		lines.AddPoint(GridToWorld(gridPoint));
	}
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
		//ScanAtAngle(angle);
		
		float prevAngle = scanAngle;
		scanAngle += 5f;
		int subSteps = 50;
		for (int i = 0; i < subSteps; i++)
		{
			float t = i / (float)subSteps;
			float angles = Mathf.Lerp(prevAngle, scanAngle, t);
			ScanAtAngle(angles); //change to angle withouts s 
		}
	}
	private float Distance(Vector2 a, Vector2 b)
	{
		return (float)Math.Sqrt(Math.Pow(b.X - a.X,2) + Math.Pow(b.Y - a.Y,2));
	}

	private void ScanAtAngle(float angle)
	{
		float rad = Mathf.DegToRad(angle);
		Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		Vector2 pos = carPos;
		Vector2 lastPos = carPos;
		//laserLength = (int)Distance(carPos, laserEnd);
		laserLength = 40;
		for (int i = 0; i < laserLength; i++)
		{
			Vector2 p = pos + dir * i;
			lastPos = p;
			//p =chunkContainer.ClampToBounds(p);
			if (GetCellWorld(p) == 2 /*|| chunkContainer.ClampToBounds(p)!=p*/)
			{
				SetCellWorld(p, 2);
				break;
			}
			SetCellWorld(p, 1);
		}
		Vector2 start = GridToWorld(carPos);
	   // Vector2 end = GridToWorld(laserEnd);
		Vector2 end = GridToWorld(laserLength * dir + carPos);
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
	private void FollowPath(double delta)
	{
		if (pathIndex >= lastPathGridPoints.Count)
		{
			CarMoving = false;
			lastPathGridPoints.Clear();
			return;
		}

		if (replanCooldown > 0f)
			replanCooldown -= (float)delta;

		if (!isReplanning && replanCooldown <= 0f && CheckPathBlocked())
		{
			if (isReplanning) 
				return;
			isReplanning = true;
			replanCooldown = CoolDown;

			Vector2 goal = lastPathGridPoints[lastPathGridPoints.Count - 1];
			CarMoving = false; 
			pathFinding(goal);  
			WebConnection.SendControlPoints(controlPoints2D);
			isReplanning = false;
			return; 
		}

		Vector2 target = lastPathGridPoints[pathIndex];
		Vector2 toTarget = target - carPos;

		float distance = toTarget.Length();
		//==================
		
		float desiredHeadingDeg = Mathf.RadToDeg(Mathf.Atan2(toTarget.Y, toTarget.X));
		float angleDiff = Mathf.Wrap(desiredHeadingDeg - carHeadingDeg, -180f, 180f);
		float maxTurnThisFrame = turnRateDegPerSec * (float)delta;
		float clampedTurn = Mathf.Clamp(angleDiff, -maxSteerDeg, maxSteerDeg);
		clampedTurn = Mathf.Clamp(clampedTurn, -maxTurnThisFrame, maxTurnThisFrame);
		carHeadingDeg += clampedTurn;
		carAngle = carHeadingDeg;
		 float rad = Mathf.DegToRad(carHeadingDeg);
		Vector2 forwardDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		//============
		float step = pathFollowSpeed * (float)delta;
		if (distance <= step)
		{
			//carPos = target; //working
			carPos += forwardDir * step;
			pathIndex++;
		}
		else
		{
			carPos += toTarget.Normalized() * step;
		}

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
		WebConnection.SendControlPoints(controlPoints2D);
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
		controlPoints2D.Clear();
		//List<Vector2> controlPoints2D = new List<Vector2>();
		for(int i = 0; i < angles.Count; i++)
		{
			Vector3 point = angles[i].AsVector3();
			if(i==0)
			{
				float distance = Distance(carPos, new Vector2(point.X, point.Y));
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
			else
			{
				float distance = Distance(new Vector2(angles[i-1].AsVector3().X, angles[i-1].AsVector3().Y), new Vector2(point.X, point.Y));
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
			//ControlPoints.Add(point);
		}
		CarMoving= true;
		UpdatePathLine();
	}
	private static void AppendPointIfNew(List<Vector2> path, Vector2 point)
 	{

		if (path.Count == 0 || !path[path.Count - 1].IsEqualApprox(point))
			path.Add(point);
	}
	private float GetIncomingHeading(List<Vector2> path)
	{
		for (int i = path.Count - 1; i > 0; i--)
 		{

			Vector2 direction = path[i] - path[i - 1];
			if (!direction.IsZeroApprox())
				return Mathf.RadToDeg(Mathf.Atan2(direction.Y, direction.X));
 		}


		return carHeadingDeg;
	}
	private bool TryRepairInvalidControlPoints(out List<Vector2> repairedPath,out int removedPointCount,out int repairedGapCount)
	{
		repairedPath = new List<Vector2> { carPos };
		removedPointCount = 0;
		repairedGapCount = 0;

		var invalidPoints = new bool[drawnPoints.Count];
		int validPointCount = 0;
		for (int i = 0; i < drawnPoints.Count; i++)
 		{
			Vector2 point = drawnPoints[i];
			bool isObstacle = IsObstacle(point);
			int penalty = GetPenaltyAt(point);
			invalidPoints[i] = isObstacle || penalty > PenaltyReplan;

			if (invalidPoints[i])
 			{
				removedPointCount++;
				string reason = isObstacle ? "it is inside an obstacle" : $"its penalty is {penalty}";
				GD.Print($"Removing control point {point} because {reason}.");
			}
			else
			{
				validPointCount++;
 			}
 		}
		if (validPointCount < 2)
 		{
			GD.PrintErr("Not enough valid control points remain to repair the path.");
			return false;
 		}
 		var chunksDict = chunkContainer.ToChunksDictionary();
 		var penaltyChunksDict = chunkContainer.ToPenaltyChunksDictionary();
 		int chunkSizeCells = chunkContainer.ChunkSize;
		int pointIndex = 0;
		while (pointIndex < drawnPoints.Count)
		{
			if (!invalidPoints[pointIndex])
 			{
				AppendPointIfNew(repairedPath, drawnPoints[pointIndex]);
				pointIndex++;
				continue;
 			}
			int invalidRunStart = pointIndex;
			while (pointIndex < drawnPoints.Count && invalidPoints[pointIndex])
				pointIndex++;

			// A trailing invalid run has no next good point, so removing it is enough.
			if (pointIndex >= drawnPoints.Count)
 			{
				GD.Print($"Removed {drawnPoints.Count - invalidRunStart} trailing invalid control point(s).");
				break;
 			}
			Vector2 lastGoodPoint = repairedPath[repairedPath.Count - 1];
			Vector2 nextGoodPoint = drawnPoints[pointIndex];
			float incomingHeading = GetIncomingHeading(repairedPath);
			var bridgePath = (Godot.Collections.Array)pathFinder.Call(
				"findPath",
				lastGoodPoint,
				nextGoodPoint,
				chunksDict,
				penaltyChunksDict,
				chunkSizeCells,
				incomingHeading);

			if (bridgePath.Count < 2)
 			{
				GD.PrintErr($"Path INVALID: cannot repair the gap from {lastGoodPoint} to {nextGoodPoint}.");
				return false;
 			}
 
			for (int i = 1; i < bridgePath.Count; i++)
				AppendPointIfNew(repairedPath, (Vector2)bridgePath[i]);
			AppendPointIfNew(repairedPath, nextGoodPoint);

			repairedGapCount++;
			pointIndex++;
 		}
 
		return repairedPath.Count >= 2;
	}

	private List<Vector2> DensifyPath(List<Vector2> path, float maxSegmentLength)
	{
		if (path.Count < 2) 
			return path;
		var result = new List<Vector2> { path[0] };

		for (int i = 1; i < path.Count; i++)
		{
			Vector2 a = path[i - 1];
			Vector2 b = path[i];
			float dist = a.DistanceTo(b);
			int subdivisions = Mathf.Max(1, Mathf.CeilToInt(dist / maxSegmentLength));
			for (int s = 1; s <= subdivisions; s++)
			{
				float t = (float)s / subdivisions;
				result.Add(a.Lerp(b, t));
			}
		}

		return result;
	}
private void OnValidatePath()
{
		
		if(CarMoving )
			return;
		if(Check.Text=="Start")
		{
			OnStartCar();
			Check.Text = "Check";
			return;
		}
		if (drawnPoints.Count < 2)
		{
			GD.Print("Need at least 2 waypoints to validate a path.");
			return;
		}

		if (!TryRepairInvalidControlPoints(out var fullRawPath, out int removedPointCount, out int repairedGapCount))
 		{
 			pathValidated = false;
 			GD.Print("Validation failed — adjust waypoints and try again.");
 			return;
 		}

		drawnPoints = fullRawPath.Skip(1).ToList();
		RedrawFilteredWaypoints();
 		var densifiedPath = DensifyPath(fullRawPath, 3f); 
 		var Xpoints = new Godot.Collections.Array();
 		var Ypoints = new Godot.Collections.Array();
		foreach (Vector2 pt in densifiedPath)
		{
			Xpoints.Add((double)pt.X);
			Ypoints.Add((double)pt.Y);
		}
		float controlDistance = 10f;
		var angles = new Godot.Collections.Array();
		var Result = (Godot.Collections.Array)catmullRomGrid.Call("createSplineWithHeading", Xpoints, Ypoints, true, carHeadingDeg, controlDistance);
		angles = (Godot.Collections.Array)catmullRomGrid.Call("returnControlPoints");
		lastPathGridPoints.Clear();
		controlPoints2D.Clear();
		for(int i = 0; i < angles.Count; i++)
		{
			Vector3 point = angles[i].AsVector3();
			if(i==0)
			{
				float distance = Distance(carPos, new Vector2(point.X, point.Y));
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
			else
			{
				float distance = Distance(new Vector2(angles[i-1].AsVector3().X, angles[i-1].AsVector3().Y), new Vector2(point.X, point.Y));
				controlPoints2D.Add(new Vector2(distance, -1*point.Z));
			}
		}
		lastPathGridPoints.Clear();
		foreach (Vector3 point in Result)
		{
			lastPathGridPoints.Add(new Vector2(point.X, point.Y));
		}
 		isDrawingPath = false;
 		UpdatePathLine();
 		lines.DefaultColor = Colors.Green;
 		Check.Text = "Start";
 		drawnPoints.Clear();
 	}


	private void RedrawFilteredWaypoints()
	{
		if (currentDrawLine != null)
			currentDrawLine.ClearPoints();

		foreach (Vector2 pt in drawnPoints)
			currentDrawLine?.AddPoint(GridToWorld(pt));

		QueueRedraw();
	}
	private void OnClearPressed()
	{
		drawnPoints.Clear();
		lines.ClearPoints();
		lastPathGridPoints.Clear();
		//lines.Dispose();
		pathValidated = false;
		isDrawingPath = true;
	}
	private void OnStartCar()
	{
		if (!pathValidated || lastPathGridPoints.Count == 0 || controlPoints2D.Count==0)
		{
			GD.Print("No validated path to drive. Draw and validate a path first.");
			return;
		}
		WebConnection.SendControlPoints(controlPoints2D);
		controlPoints2D.Clear();
		pathIndex = 0;
		CarMoving = true;
		GD.Print("Car starting to follow validated path.");
	}
	void OnMenu()
	{
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
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
		// sensor A
		if (distanceA >= 0)
		{
			float worldAngleA = carHeadingDeg + angleA;
			float angleRadA = Mathf.DegToRad(worldAngleA);
			this.angle = angleRadA;
			float distanceCellsA = distanceA / MM_PER_CELL; 
			float endXA = carPos.X + distanceCellsA * Mathf.Cos(angleRadA);
			float endYA = carPos.Y + distanceCellsA * Mathf.Sin(angleRadA);
			int gridXA = (int)Mathf.Round(endXA);
			int gridYA = (int)Mathf.Round(endYA);
			laserEnd = new Vector2(gridXA, gridYA);
			SetCell(gridXA, gridYA, 2);
		}
	
	   
	}
}
