using Godot;

public partial class Client : Node {
    public static readonly bool IsClient = !Server.IsServer;

    public override void _Ready() {
        if (IsClient) {
            // Add game version to window title
            GetWindow().Title = "Client";

            // Connect to server
            ConnectToServer();
        }
    }
    public Error ConnectToServer() {
        // Create peer
        ENetMultiplayerPeer Peer = new();
        // Try to create client
        Error Error = Peer.CreateClient("localhost", 5123);
        if (Error is not Error.Ok) {
            return Error;
        }
        // Set peer
        Multiplayer.MultiplayerPeer = Peer;
        return Error.Ok;
    }
}