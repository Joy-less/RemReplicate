# Rem Replicate

A network multiplayer replication framework for Godot C#.

## Features

- Add the `[RemoteProperty]` attribute to replicate a property when changed.
- Entities spawned/despawned by the server are automatically replicated to clients.
- Properties are automatically replicated by the owner to other peers.
- Use `SetPropertyOwner(string, int)` to change the owner of a property.
- Created for use in a real [MMORPG](https://youtu.be/4ptBKI0cGhI).

## Dependencies

- [Rem Send](https://github.com/Joy-less/RemSend)

## Setup

1. Install [Rem Send](https://github.com/Joy-less/RemSend).

2. Add the Rem Replicate addon to your project and build your project.

3. Create a `Replicator` node (or create a node and attach the `Replicator.cs` script).

## Sample

Look inside the Sample folder for an example project.

The server replicates a moving, colored cube to the client.

Run the project with 2 debug instances, where one instance has a `server` feature/argument.