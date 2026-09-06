using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class WebSocket : Node
{
	[Signal]
	public delegate void ESP32DataEventHandler(float x, float y, float angle, float distance);
	private WebSocketPeer socket = new WebSocketPeer();
	private WebSocketPeer.State lastState = WebSocketPeer.State.Closed;
	private String ip= "";
	private String port = ":81";
	[Export] public string url = "ws://10.213.72.79:81";
	public override void _Ready()
	{
		Error err = socket.ConnectToUrl(url);
		if (err != Error.Ok)
		{
			GD.Print("Failed to connect to WebSocket");
			return;
		}
		GD.Print("WebSocket connected");
	}

	public override void _Process(double delta)
	{
		socket.Poll();
		var state = socket.GetReadyState();
		if (state != lastState)
		{
			GD.Print($"WebSocket state changed: {lastState} to {state}");
			lastState = state;
		}
		if (state == WebSocketPeer.State.Open)
		{
			while (socket.GetAvailablePacketCount() > 0)
			{
				byte[] packet = socket.GetPacket();
				string text = Encoding.UTF8.GetString(packet);
				HandleData(text);
			}
		}
		else if (state == WebSocketPeer.State.Closed)
		{
			int code = socket.GetCloseCode();
			string reason = socket.GetCloseReason();
			GD.Print($"WebSocket closed. Code: {code}, Reason: {reason}");
			SetProcess(false); 
		}
		/*while (socket.GetAvailablePacketCount() > 0)
		{
			string msg = socket.GetPacket().GetStringFromUtf8();
			HandleData(msg);
		}*/
	}
	public void SendControlPoints(List<Vector2> controlPoints)
	{
		if (socket.GetReadyState() != WebSocketPeer.State.Open)
		{
			GD.Print("WebSocket not open, cannot send control points.");
			return;
		}
		var pointsList = new Godot.Collections.Array();
		foreach (Vector2 item in controlPoints)
		{
			 // control points are packed as Vector2(x, y)
			var pointArr = new Godot.Collections.Array { item.X, item.Y};
			pointsList.Add(pointArr);
		}

		var payload = new Godot.Collections.Dictionary
		{
			{ "type", "path" },
			{ "points", pointsList }
		};

		string json = Json.Stringify(payload);
		Error err = socket.SendText(json);
		GD.Print($"Sent control points: {json}");

		if (err != Error.Ok)
			GD.PrintErr($"Failed to send control points: {err}");
	}

	private void HandleData(string jsonString)
	{
		var json = Json.ParseString(jsonString).AsGodotDictionary();

		int angleA = (int)json["angleA"];
		int angleB = (int)json["angleB"];

		float distanceA = (float)json["distanceA"];
		float distanceB = (float)json["distanceB"];


		//GD.Print($"Robot: {x}, {y}, {angle}, {distance}");
		EmitSignal(SignalName.ESP32Data, angleA,angleB, distanceA, distanceB);
		//GetNode<DrawingAppGrid>("./drawingAppGrid").OnSensorData(x, y, angle, distance);
	}
	public override void _ExitTree()
	{
		socket.Close();
	}
}
