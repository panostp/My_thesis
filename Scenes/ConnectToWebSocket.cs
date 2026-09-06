using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class ConnectToWebSocket : Control
{
	private WebSocketMananger WebConnection;
	private LineEdit TextInput;
	private LineEdit TextInput2;
	private LineEdit TextInput3;
	private LineEdit TextInput4;
	private Label Indicator;
	private String ip="";
	[Export] private int boxSize = 50;
	private float waveTime = 0f;
	List<Vector2> boxColor = new List<Vector2>();
	private Control PanelContainer;
	private Label espName;
	private DeviceDiscovery deviceDiscovery;

	public override void _Ready()
	{
		WebConnection = GetNode<WebSocketMananger>("/root/WebSocketMananger");
		TextInput = GetNode<LineEdit>("LineEdit");
		TextInput2 = GetNode<LineEdit>("LineEdit2");
		TextInput3 = GetNode<LineEdit>("LineEdit3");
		TextInput4 = GetNode<LineEdit>("LineEdit4");
		Indicator = GetNode<Label>("Wrong");
		deviceDiscovery = GetNode<DeviceDiscovery>("DeviceDiscovery");
		PanelContainer = GetNode<Control>("PanelContainer");
		espName = GetNode<Label>("PanelContainer/Devices/espName");
		PanelContainer.Visible =false;
		Indicator.Visible=false;
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

	public void Line1Changed(String Text)
	{
		if(Text.Length>2)
		{
			if(CheckIP(Text))
			{
				GD.Print("OK");
				WebConnection.ip = Text;
				GD.Print(WebConnection.ip);
				Color color = new Color(1.0f,0f,0f);
				Indicator.Text = "IP Correct";
				Indicator.AddThemeColorOverride("font_color", Colors.Green);
				Indicator.Visible=true;
				Finished();
				return;
			}
			if (!int.TryParse(Text, out int number) || number < 0 || number > 255)
			{
				GD.Print("Lathos");
				TextInput.Clear();
				Indicator.Text = "Only Numbers 0-255";
				Indicator.AddThemeColorOverride("font_color", Colors.Red);
				Indicator.Visible=true;
			}
			else
			{
				ip+=Text+".";
				GD.Print(ip);
				TextInput.Unedit();
				TextInput2.Edit();
				

			}
			
		}
	}
	public void Line2Changed(String Text)
	{
		if(Text.Length>2)
		{
			if (!int.TryParse(Text, out int number) || number < 0 || number > 255)
			{
				GD.Print("Lathos");
				TextInput2.Clear();
				Indicator.Text = "Only Numbers 0-255";
				Indicator.AddThemeColorOverride("font_color", Colors.Red);
				Indicator.Visible=true;
			}
			else
			{
				ip+=Text+".";
				GD.Print(ip);
				TextInput2.Unedit();
				TextInput3.Edit();


			}
			
		}
	}
	public void Line3Changed(String Text)
	{
		if(Text.Length>2)
		{
			if (!int.TryParse(Text, out int number) || number < 0 || number > 255)
			{
				GD.Print("Lathos");
				TextInput3.Clear();
				Indicator.Text = "Only Numbers 0-255";
				Indicator.AddThemeColorOverride("font_color", Colors.Red);
				Indicator.Visible=true;
			}
			else
			{
				ip+=Text+".";
				GD.Print(ip);
				TextInput3.Unedit();
				TextInput4.Edit();
				

			}
			
		}
	}
	public void OnText(String Text)
	{
	   
		if (!int.TryParse(Text, out int number) || number < 0 || number > 255)
		{
			GD.Print("Lathos");
			TextInput4.Clear();
		}
		else
		{
			ip+=Text;
		}
		if(CheckIP(ip))
		{
			GD.Print("OK");
			WebConnection.ip = ip;
			GD.Print(WebConnection.ip);
			Color color = new Color(1.0f,0f,0f);
			Indicator.Text = "IP Correct";
			Indicator.AddThemeColorOverride("font_color", Colors.Green);
			Indicator.Visible=true;
			Finished();
		}
		else
		{
			Indicator.Text = "incorrect ip";
			Indicator.AddThemeColorOverride("font_color", Colors.Red);
			Indicator.Visible=true;
			ip="";
			TextInput.Clear();
			TextInput2.Clear();
			TextInput3.Clear();
			TextInput4.Clear();
			TextInput.Edit();
		}
			
		
	}
	private bool CheckIP(String CheckIP)
	{
		bool isIp = Regex.IsMatch(CheckIP,@"^(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)$");
		if(isIp)
		{
		   
			return true;
		}
		else
		{
			
			return false;
		}
	}
	private async void Finished()
	{
		WebConnection.Connect();
		for (int i=0;i<3;i++)
		{
			if(!WebConnection.connected)
			{
				Indicator.Text = "waiting for device...";
				Indicator.Visible=true;
				await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
			}
			else
			{
				GetTree().ChangeSceneToFile("res://Scenes/ChooseMode.tscn");
				break;
			}
				
		}
		Indicator.Text = "Device not found";
		Indicator.AddThemeColorOverride("font_color", Colors.Red);
		
	}
	private void OnCheckedPressed()
	{
		
	}
	private void OnESP32Found(String IP)
	{
		//OnConnectDevice
		espName.Text = $"ESP32 CAR : {IP}";
		PanelContainer.Visible =true;

	}
	private void OnConnectDevice()
	{
		WebConnection.Connect();
		GetTree().ChangeSceneToFile("res://Scenes/ChooseMode.tscn");
	}
	private void onHome()
	{
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
	private void OnRefresh()
	{
		deviceDiscovery.StartDiscovery();
	}

}
