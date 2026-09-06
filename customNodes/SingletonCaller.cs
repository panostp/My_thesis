using Godot;
using System;

public partial class SingletonCaller : Node2D
{
    private AutoLoadSingleton WebConnection;
    public override void _Ready()
    {
        WebConnection = GetNode<AutoLoadSingleton>("/root/AutoLoadSingleton");
        GD.Print(WebConnection.isConnected);
    }


}
