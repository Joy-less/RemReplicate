# Rem Replicate

A network multiplayer replication framework for Godot C#.

## Disclaimer

There are several ways to solve network replication. This implementation is designed for use in [Blood Lines](https://youtu.be/4ptBKI0cGhI), an upcoming MMORPG.

## Features

- Encapsulate your entity's properties in a record.
- Automatically spawns/despawns entities from the server to the clients.
- Automatically replicates record properties from the owner to other peers.
- You can set the owner of an individual property.
- Records are designed to be storable in document databases like [LiteDB](https://github.com/mbdavid/LiteDB).

## Dependencies

- [Rem Send](https://github.com/Joy-less/RemSend)

## Setup

1. Install [Rem Send](https://github.com/Joy-less/RemSend).
2. Add the Rem Replicate addon to your project.
3. Create a node and attach the `Replicator.cs` script.

## Sample

Look inside the Sample folder for an example project.
The server replicates a moving, coloured cube to the client.
Run the project with 2 debug instances, where one instance has a `server` feature/argument.