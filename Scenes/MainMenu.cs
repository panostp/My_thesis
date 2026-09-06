using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainMenu : Control
{
	[Export] private int boxSize = 50;
	private float waveTime = 0f;
	List<Vector2> boxColor = new List<Vector2>();
	private WebSocketMananger WebConnection;
	
	public override void _Ready()
	{
		WebConnection = GetNode<WebSocketMananger>("/root/WebSocketMananger");
	}

	public override void _Process(double delta)
	{
		waveTime += (float)delta;
		QueueRedraw();
	}
	public override void _Draw()
	{
		DrawBackground();
	}



	private void DrawBackground()
	{
		Vector2 size = GetViewport().GetVisibleRect().Size;
		Vector2 mouse = GetLocalMousePosition();
		float radius = 250f;       
		float waveLength = 200f;    
		float waveSpeed = 3f;      
		int index=0;

		for (int i = 0; i <= size.X; i += boxSize)
		{
			for (int j = 0; j <= size.Y; j += boxSize)
			{
				float t = Mathf.Clamp((i + j) / size.X, 0f, 0.5f);
				if (index >= boxColor.Count)
				{
					float randomT = Random.Shared.NextSingle() * 0.5f;
					boxColor.Add(new Vector2(index, randomT));
				}
				t = boxColor[index].Y;
				//
				
				Color color = new Color(0f,1f - t,1f );

				//t = Mathf.SmoothStep(0.5f, 1f, t);
				
				Vector2 boxCenter = new Vector2(i + boxSize / 2f, j + boxSize / 2f);
				float distance = boxCenter.DistanceTo(mouse);
				
				//float wave =Mathf.Sin(i * 0.03f +waveTime * 2f);

				float influence = 1f - Mathf.Clamp(distance / radius, 0f, 1f);
	
				influence = Mathf.SmoothStep(0f, 1f, influence);
				float wave = Mathf.Sin(distance / waveLength -waveTime * waveSpeed);
				float scale = 1f + (wave) * 0.15f * influence;
				t= Mathf.Clamp(t + (wave) * 0.15f * influence, 0f, 1f);

				color = new Color(0f,1f - t,1f );
				float newSize = boxSize * scale;
				float offset = (boxSize - newSize) / 2f;
				Rect2 rect = new Rect2(i + offset,j + offset,newSize,newSize);

				DrawRect(rect, color);
				Color borderColor =new Color(0f, 0f, 0f, 0.18f);
				DrawRect(rect, borderColor, false);
				index++;
			}
		}
	}
	public void OnStartPress()
	{
		if(WebConnection.ip!="")
			GetTree().ChangeSceneToFile("res://Scenes/ChooseMode.tscn");
		else
			GetTree().ChangeSceneToFile("res://Scenes/connect_to_web_socket.tscn");
	}
	public void _on_exit_pressed()
	{
		GetTree().Quit();
	}
	public void setingsPressed()
	{
		GD.Print("Start Pressed");
		//GetTree().ChangeSceneToFile("res://Scenes/drawAppMainGrid.tscn");
		GetTree().ChangeSceneToFile("res://Scenes/drawingAppGrid.tscn");
	}
}
