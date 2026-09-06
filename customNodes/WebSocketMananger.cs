using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class WebSocketMananger : Node
{
	[Signal]
	public delegate void ESP32DataEventHandler(
		int angleA,
		float distanceA
	);
	[Signal]
	public delegate void ESP32StopEventHandler();
	[Signal]
	public delegate void SegmentCompleteEventHandler(int index, bool pathComplete);

	private WebSocketPeer socket = new WebSocketPeer();

	private WebSocketPeer.State lastState = WebSocketPeer.State.Closed;

	public string ip = "";
	private string port = ":81";
	public bool connected = false;

	public bool IsConnected =>
		socket.GetReadyState() == WebSocketPeer.State.Open;


	public override void _Process(double delta)
	{
		socket.Poll();
		var state = socket.GetReadyState();
	 

		if (state == WebSocketPeer.State.Open)
		{
			while (socket.GetAvailablePacketCount() > 0)
			{
				byte[] packet = socket.GetPacket();
				string text = Encoding.UTF8.GetString(packet);
				//GD.Print("Received: " + text);
				connected = true;
				HandleData(text);
			}
			//lastState = state;
		}
		else if (state == WebSocketPeer.State.Closed && lastState==WebSocketPeer.State.Open)
		{
			int code = socket.GetCloseCode();
			string reason = socket.GetCloseReason();
			this.ip="";
			connected = false;
			GetTree().ChangeSceneToFile("res://Scenes/connect_to_web_socket.tscn");
			GD.Print($"WebSocket closed. Code: {code}, Reason: {reason}");
			GD.Print($"ip = {this.ip}");

			
		}
		if (state != lastState)
		{
			GD.Print($"WebSocket state changed: {lastState} -> {state}");
			lastState = state;
		}
	}


	public bool Connect()
	{
		string url = "ws://" + ip + port;
		GD.Print("Connecting to: " + url);
		Error err = socket.ConnectToUrl(url);
		if (err != Error.Ok)
		{
			GD.PrintErr("Failed to start WebSocket connection: " + err);
			return false;
		}
		GD.Print("WebSocket connection started");
		return true;
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
			var pointArr = new Godot.Collections.Array
			{
				item.X, item.Y
			};
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
		{
			GD.PrintErr($"Failed to send control points: {err}");
		}
	}


	private void HandleData(string jsonString)
	{
		try
		{
			var json = Json.ParseString(jsonString).AsGodotDictionary();
			string type = json.ContainsKey("type") ? (string)json["type"] : "sensor_data";

			switch (type)
			{
				case "sensor_data":
				{
					int angleA = (int)json["angleA"];
					float distanceA = (float)json["distanceA"];
					EmitSignal(SignalName.ESP32Data, angleA, distanceA);
					break;
				}
				case "segment_complete":
				{
					int index = (int)json["index"];
					bool pathComplete = (bool)json["pathComplete"];
					EmitSignal(SignalName.SegmentComplete, index, pathComplete);
					break;
				}
				case "emergency_stop":
					EmitSignal(SignalName.ESP32Stop);
					break;
				default:
					GD.Print($"Unknown message type from ESP32: {type}");
					break;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Failed to parse ESP32 data: " + e.Message);
			GD.PrintErr("Received JSON: " + jsonString);
		}
	}


	public override void _ExitTree()
	{
		socket.Close();
	}
}
