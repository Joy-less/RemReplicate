using System;
using System.Threading.Tasks;
using Godot;
using RemReplicate;

public partial class Server : Node {
    public static bool IsServer { get; } = OS.HasFeature("server");

    public override async void _Ready() {
        if (IsServer) {
            // Add game version to window title
            GetWindow().Title = "Server";

            // Start server
            StartServer();
            
            // Spawn cube after 1 second
            await Task.Delay(TimeSpan.FromSeconds(1));
            Replicator.Main.SpawnEntity(new CubeRecord() {
                Color = Colors.Red
            });
        }
    }
    public Error StartServer() {
        // Create peer
        ENetMultiplayerPeer Peer = new();
        // Try to create server
        Error Error = Peer.CreateServer(5123);
        if (Error is not Error.Ok) {
            return Error;
        }
        // Set peer
        Multiplayer.MultiplayerPeer = Peer;
        // Return success
        return Error.Ok;
    }
}