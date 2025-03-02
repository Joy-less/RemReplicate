#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using RemSend;

namespace RemReplicate;

[GlobalClass]
public partial class Replicator : Node {
    [Export] public double ReplicateHz { get; set; } = 20;
    [Export] public PackedScene[] ReplicatedScenes { get; set; } = [];

    [Signal] public delegate void SpawnEventHandler(Entity Entity);
    [Signal] public delegate void DespawnEventHandler(Entity Entity);

    public static Replicator Singleton { get; private set; } = null!;

    private readonly Dictionary<string, PackedScene> Scenes = []; // { entity type, template scene }
    private readonly Dictionary<string, Node> Folders = []; // { entity type, parent node }

    public Replicator() {
        // Set as singleton
        Singleton = this;
    }
    public override void _Ready() {
        // Initialize replicator once
        Initialize();
    }
    public Entity SpawnEntity(Record Record) {
        // Get entity type from record
        string EntityType = GetEntityTypeFromTypeOfRecord(Record.GetType());
        // Create entity from record
        Entity Entity = Scenes[EntityType].Instantiate<Entity>();
        Entity.Name = Record.Id.ToString();
        Entity.SetRecord(Record);
        // Add entity to folder
        Folders[EntityType].AddChild(Entity);
        // Invoke event
        EmitSignalSpawn(Entity);
        return Entity;
    }
    public Entity SpawnEntity(string Type, Guid Id, Dictionary<string, byte[]> Properties) {
        // Create entity from scene
        Entity Entity = Scenes[Type].Instantiate<Entity>();
        Entity.Name = Id.ToString();
        Entity.SetProperties(Properties);
        // Add entity to folder
        Folders[Type].AddChild(Entity);
        // Invoke event
        EmitSignalSpawn(Entity);
        return Entity;
    }
    public bool DespawnEntity(string EntityType, Guid Id) {
        // Find and destroy entity
        if (GetEntity(EntityType, Id) is Entity Entity) {
            Entity.QueueFree();
            // Invoke event
            EmitSignalDespawn(Entity);
            return true;
        }
        return false;
    }
    public bool DespawnEntity(EntityRef Ref) {
        return DespawnEntity(Ref.Class, Ref.Id);
    }
    public bool DespawnEntity<TEntity>(Guid Id) where TEntity : Entity {
        return DespawnEntity(GetEntityTypeFromTypeOfEntity<TEntity>(), Id);
    }
    public void DespawnEntities() {
        foreach (Entity Entity in GetEntities()) {
            DespawnEntity(Entity.Ref);
        }
    }
    public Entity GetEntity(string Type, Guid Id) {
        // Find entity by ID
        return Folders[Type].GetNode<Entity>(Id.ToString());
    }
    public Entity GetEntity(EntityRef Ref) {
        return GetEntity(Ref.Class, Ref.Id);
    }
    public TEntity GetEntity<TEntity>(Guid Id) where TEntity : Entity {
        return (TEntity)GetEntity(GetEntityTypeFromTypeOfEntity<TEntity>(), Id);
    }
    public Entity? GetEntityOrNull(string? Type, Guid? Id) {
        // Ensure ID is not null
        if (Type is null || Id is null) {
            return null;
        }
        // Find entity by ID
        return Folders[Type].GetNodeOrNull<Entity>(Id.Value.ToString());
    }
    public Entity? GetEntityOrNull(EntityRef? Ref) {
        return GetEntityOrNull(Ref?.Class, Ref?.Id);
    }
    public TEntity? GetEntityOrNull<TEntity>(Guid? Id) where TEntity : Entity {
        return (TEntity?)GetEntityOrNull(GetEntityTypeFromTypeOfEntity<TEntity>(), Id);
    }
    public IEnumerable<Entity> GetEntities() {
        return Folders.Values.SelectMany(Folder => Folder.GetChildren().OfType<Entity>());
    }
    public IEnumerable<TEntity> GetEntities<TEntity>() where TEntity : Entity {
        return GetEntities().OfType<TEntity>();
    }
    public IEnumerable<TEntity> GetNearestEntities<TEntity>(Vector3 Position, double MaxDistance = double.PositiveInfinity) where TEntity : Entity3D {
        return GetEntities<TEntity>()
            .Where(Entity => Entity.DistanceTo(Position) <= MaxDistance)
            .OrderBy(Entity => Entity.DistanceTo(Position));
    }

    public static string GetEntityTypeFromTypeOfEntity(Type EntityType) {
        return EntityType.Name.TrimSuffix("Entity");
    }
    public static string GetEntityTypeFromTypeOfRecord(Type RecordType) {
        return RecordType.Name.TrimSuffix("Record");
    }
    public static string GetEntityTypeFromTypeOfEntity<T>() where T : Entity {
        return GetEntityTypeFromTypeOfEntity(typeof(T));
    }
    public static string GetEntityTypeFromTypeOfRecord<T>() where T : Record {
        return GetEntityTypeFromTypeOfRecord(typeof(T));
    }
    public static string GetEntityTypeFromScene(PackedScene Scene) {
        return Scene.ResourcePath.GetFile().GetBaseName();
    }

    [Rem(RemAccess.Authority)]
    protected void SpawnRem(string EntityType, Guid EntityId, Dictionary<string, byte[]> Properties) {
        SpawnEntity(EntityType, EntityId, Properties);
    }
    [Rem(RemAccess.Authority)]
    protected void DespawnRem(string EntityType, Guid EntityId) {
        DespawnEntity(EntityType, EntityId);
    }

    private void Initialize() {
        // Ensure RemSendService is initialized
        RuntimeHelpers.RunClassConstructor(typeof(RemSendService).TypeHandle);

        // Setup each scene for replication
        foreach (PackedScene Scene in ReplicatedScenes) {
            // Get entity type from scene
            string EntityType = GetEntityTypeFromScene(Scene);

            // Create folder for entity type
            Node Folder = new() {
                Name = EntityType
            };
            AddChild(Folder);

            // Server: On entity added, replicate spawn entity
            Folder.ChildEnteredTree += (Node Child) => {
                if (Child is Entity Entity) {
                    _EntityAdded(Entity, EntityType);
                }
            };
            // Server: On entity removed, replicate despawn entity
            Folder.ChildExitingTree += (Node Child) => {
                if (Child is Entity Entity) {
                    _EntityRemoved(Entity, EntityType);
                }
            };

            // Add entity type to lookup tables
            Scenes[EntityType] = Scene;
            Folders[EntityType] = Folder;
        }

        // Server: On client connect, replicate spawn all entities
        Multiplayer.PeerConnected += (long PeerId) => {
            _PeerAdded((int)PeerId);
        };
        // Client: On server disconnect, locally destroy all entities
        Multiplayer.ServerDisconnected += _ServerDisconnected;
    }
    private void _EntityAdded(Entity Entity, string EntityType) {
        // Ensure this is the server
        if (!IsMultiplayerAuthority()) {
            return;
        }
        // Replicate entity spawn
        BroadcastSpawnRem(EntityType, Entity.Record.Id, Entity.GetChangedProperties());
    }
    private void _EntityRemoved(Entity Entity, string EntityType) {
        // Ensure this is the server
        if (!IsMultiplayerAuthority()) {
            return;
        }
        // Replicate entity despawn
        BroadcastDespawnRem(EntityType, Entity.Record.Id);
    }
    private void _PeerAdded(int PeerId) {
        // Ensure this is the server
        if (!IsMultiplayerAuthority()) {
            return;
        }
        // Replicate all entities to peer
        foreach (Entity Entity in GetEntities()) {
            // Replicate entity
            SendSpawnRem(PeerId, Entity.GetEntityType(), Entity.Record.Id, Entity.GetProperties());
            // Replicate property owners
            foreach (Property Property in Entity.Properties.Values) {
                Entity.SendSetPropertyOwnerRem(PeerId, Property.Name, Property.Owner);
            }
        }
    }
    private void _ServerDisconnected() {
        // Locally destroy all entities
        DespawnEntities();
    }
}