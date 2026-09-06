using Godot;
using System;
using System.Linq;


public partial class DrawingApp : Control
{
    [Export] private int CanvasWidth = 512;
    [Export] private int CanvasHeight = 512;
    [Export] private Color BrushColor = new(1, 0, 0); 
    [Export] private int BrushSize = 8;
 	private Image image;
    private ImageTexture texture;
    private bool isDrawing = false;
	//private Vector2 lastPos;
	private Line2D currentLine = null;
	private Line2D lines;
    private TextureRect texRect;
    private circle circles;
    private bool edit = false;
    private Node2D catmullRom;
	public override void _Ready()
    {
		image = Image.CreateEmpty(CanvasWidth, CanvasHeight, false, Image.Format.Rgba8);
		image.Fill(new Color(1, 1, 1, 1)); 

		texture = ImageTexture.CreateFromImage(image);
		GetNode<TextureRect>("TextureRect").Texture = texture;
		var slider = GetNodeOrNull<Slider>("HSlider");
		lines = GetNode<Line2D>("Line2D");
        texRect = GetNode<TextureRect>("TextureRect");
        circles = GetNode<circle>("Circle");
        catmullRom = GetNode<Node2D>("CatmullRom");
    }
	public override void _Input(InputEvent @event)
	{
		//Vector2 mousePos = GetLocalMousePosition();
		bool isInside = new Rect2(Vector2.Zero, texRect.Size).HasPoint(texRect.GetLocalMousePosition());
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
				isDrawing = mouseEvent.Pressed;
				if (isDrawing && isInside && !edit)
                {
                    currentLine = new Line2D
                    {
                        Width = BrushSize,
                        DefaultColor = BrushColor
                    };
                    lines.AddChild(currentLine);
                    currentLine.AddPoint(mouseEvent.Position);
                }
				
            }
		}
		else if (@event is InputEventMouseMotion motionEvent && isDrawing && isInside && !edit)
		{
            currentLine.AddPoint(motionEvent.Position);
		}
	}
	private void OnBrushSizeChanged(double value)
    {
        BrushSize = (int)value;
    }

    private void OnClearPressed()
    {
        foreach (Node child in lines.GetChildren().ToArray())
        {
            child.QueueFree();
        }
        lines.ClearPoints();
        circles.ClearCircles();
        edit = false;
    }
    private void SendPress()
    {
        int i = 0;
        var Xpoints = new Godot.Collections.Array();
        var Ypoints = new Godot.Collections.Array();
        var Result = new Godot.Collections.Array();
        foreach (Line2D line in lines.GetChildren().Cast<Line2D>())
        {
            foreach (Vector2 point in line.Points)
            {
                Xpoints.Add(point.X);
                Ypoints.Add(point.Y);
                
            }
        }
        //GD.Print("Xpoints length: " + Xpoints.Count);
        edit = true;
        Result = (Godot.Collections.Array)catmullRom.Call("createSpline", Xpoints, Ypoints, true);
        //GD.Print("Received array from C++ of size: ", Result.Count);
        foreach (Node child in lines.GetChildren().ToArray())
        {
            child.Free();
        }
        lines.ClearPoints();
        foreach (Vector2 item in Result)
        {
            lines.AddPoint(item);
            if (i % 10 == 0)
            {
                circles.AddCircle(item);
            }
            i++;
            //DrawCircle(item, 5f, new Color(0.0f, 0.0f, 1f));
        }
        lines.DefaultColor = Colors.Black;
        /*lines.ClearPoints();
        currentLine.ClearPoints();
        
        //lines.AddChild(currentLine);
        foreach (Vector2 item in Result.Select(v => (Vector2)v))
        {
            currentLine.AddPoint(item);
        }*/
        
        

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}
    public void Drawgrid()
    {
        // Draw the texture on the control
        if (texture != null)
        {
            DrawTexture(texture, Vector2.Zero);
        }
    }
}
