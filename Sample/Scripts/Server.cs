using System;
using System.Threading.Tasks;
using Godot;
using RemReplicate;
using RemSend;

namespace Sample;

public partial class Server : Node {
    public static bool IsServer { get; } = OS.HasFeature("server");

    public override async void _Ready() {
        if (IsServer) {
            // Add game version to window title
            GetWindow().Title = "Server";

            // Start server
            StartServer();

            // Spawn red cube after 1 second
            await Task.Delay(TimeSpan.FromSeconds(1));
            Replicator.Singleton.SpawnEntity<CubeEntity>(CubeEntity.ScenePath, CubeEntity => {
                CubeEntity.Color = Colors.Red;
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
        // Setup RemSend
        RemSendService.Setup((SceneMultiplayer)Multiplayer);
        // Return success
        return Error.Ok;
    }
}