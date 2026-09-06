using Godot;
using System;
using System.Collections.Generic;
public partial class circle : Node2D
{
    private List<Vector2> circlePos = new List<Vector2>();
    private int circleID = 0;
    //private Vector2 circlePos = new(200, 200); // starting position
    [Export] private float radius = 10f; 
    private bool dragging = false;
    private Vector2 dragOffset;

    public override void _Draw()
    {
        foreach (var pos in circlePos)
        {
            DrawCircle(pos, radius, new Color(0.2f, 0.8f, 1f));
        }
        
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    int i = 0;
                    foreach (var pos in circlePos)
                    {
                        if (pos.DistanceTo(mouseEvent.Position) <= radius)
                        {
                            dragging = true;
                            dragOffset = mouseEvent.Position - pos;
                            circleID = i;
                            break;
                        }
                        i++;
                        // if (pos.DistanceTo(mouseEvent.Position) <= radius)
                        // {
                        //     dragging = true;
                        //     dragOffset = mouseEvent.Position - pos;
                        // }
                    }
                }
                else
                {
                    dragging = false;
                }
            }
        }

        if (@event is InputEventMouseMotion motionEvent && dragging)
        {
            circlePos[circleID] = motionEvent.Position - dragOffset;
            QueueRedraw();
        }
    }
    public void AddCircle(Vector2 position)
    {
        circlePos.Add(position);
        QueueRedraw();
    }
    public void ClearCircles()
    {
        circlePos.Clear();
        QueueRedraw();
    }
    // circlePos.X = Mathf.Clamp(circlePos.X, radius, GetViewportRect().Size.X - radius);
    // circlePos.Y = Mathf.Clamp(circlePos.Y, radius, GetViewportRect().Size.Y - radius);

}
