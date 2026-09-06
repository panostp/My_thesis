using Godot;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public partial class DeviceDiscovery : Node
{
	[Signal]
	public delegate void Esp32FoundEventHandler(String IP);
	private const int UDP_PORT = 4210;
	private UdpClient udp;
	private Thread receiveThread;
	private bool running = true;
	private WebSocketMananger WebConnection;
	public override void _Ready()
	{
		StartDiscovery();
		WebConnection = GetNode<WebSocketMananger>("/root/WebSocketMananger");
	}
	public void StartDiscovery()
	{
		udp = new UdpClient();
		udp.EnableBroadcast = true;
		udp.Client.Bind( new IPEndPoint(IPAddress.Any, 0));
		receiveThread = new Thread(ReceiveUDP);
		receiveThread.IsBackground = true;
		receiveThread.Start();
		SendDiscovery();
		GD.Print("UDP discovery started");
	}
	private void SendDiscovery()
	{
		byte[] data = Encoding.UTF8.GetBytes("DISCOVER");
		IPEndPoint broadcastEndPoint =
		new IPEndPoint(IPAddress.Parse("192.168.137.255"),UDP_PORT);
		udp.Send(data,data.Length,broadcastEndPoint);
		GD.Print("Discovery request sent");
	}

	private void ReceiveUDP()
	{
		IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

		while (running)
		{
			try
			{
				byte[] data =udp.Receive(ref remoteEndPoint);
				string message =Encoding.UTF8.GetString(data);

				CallDeferred(MethodName.HandleDevice,message,remoteEndPoint.Address.ToString());
			}
			catch (Exception e)
			{
				if (running)
					GD.PrintErr("UDP error: " + e.Message);
			}
		}
	}

	private void HandleDevice(string message,string senderIP)
	{
		string[] parts = message.Split('|');
		if (parts.Length != 2)
			return;
		string deviceName = parts[0];
		string deviceIP = parts[1];
		GD.Print("Device: " + deviceName);
		GD.Print("IP: " + deviceIP);
		WebConnection.ip = senderIP;
		EmitSignal(SignalName.Esp32Found, deviceIP);

	}

	public override void _ExitTree()
	{
		running = false;

		try
		{
			udp?.Close();
		}
		catch
		{
		}
	}
}
