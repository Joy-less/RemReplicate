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
    [Export] public bool DestroyEntitiesWhenDisconnected { get; set; } = true;

    [Signal] public delegate void SpawnEventHandler(Entity Entity);
    [Signal] public delegate void DespawnEventHandler(Entity Entity);

    public static Replicator Main { get; private set; } = null!;

    private readonly Dictionary<string, PackedScene> Scenes = []; // { entity type, template scene }
    private readonly Dictionary<string, Node> Folders = []; // { entity type, parent node }

    public override void _EnterTree() {
        Main = this;
    }
    public override void _Ready() {
        Setup();
    }
    public override void _Process(double Delta) {
        // Destroy all entities when disconnected
        if (DestroyEntitiesWhenDisconnected) {
            if (!IsInstanceValid(Multiplayer.MultiplayerPeer)) {
                DestroyEntities();
            }
        }
    }
    public void Setup() {
        // Ensure the RemSendService static constructor is run
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

            // Spawn added entities with properties
            Folder.ChildEnteredTree += Child => {
                if (Child is Entity Entity) {
                    _EntityAdded(Entity, EntityType);
                }
            };

            // Despawn removed entities
            Folder.ChildExitingTree += Child => {
                if (Child is Entity Entity) {
                    _EntityRemoved(Entity, EntityType);
                }
            };

            // Add entity type to lookup tables
            Scenes[EntityType] = Scene;
            Folders[EntityType] = Folder;
        }

        // Replicate all entities to new peers
        Multiplayer.PeerConnected += PeerId => _PeerAdded((int)PeerId);
    }
    public Entity SpawnEntity(Record Record) {
        // Get entity type from record
        string EntityType = GetEntityTypeFromTypeOfRecord(Record.GetType());
        // Create entity from record
        Entity Entity = Scenes[EntityType].Instantiate<Entity>();
        Entity.SetRecord(Record);
        Entity.Name = Record.Id.ToString();
        // Add entity to folder
        Folders[EntityType].AddChild(Entity);
        // Invoke event
        EmitSignalSpawn(Entity);
        return Entity;
    }
    public bool DestroyEntity(string EntityType, Guid Id) {
        // Find and destroy entity
        if (GetEntity(EntityType, Id) is Entity Entity) {
            Entity.QueueFree();
            // Invoke event
            EmitSignalDespawn(Entity);
            return true;
        }
        return false;
    }
    public bool DestroyEntity(EntityRef Ref) {
        return DestroyEntity(Ref.Class, Ref.Id);
    }
    public bool DestroyEntity<TEntity>(Guid Id) where TEntity : Entity {
        return DestroyEntity(GetEntityTypeFromTypeOfEntity<TEntity>(), Id);
    }
    public void DestroyEntities() {
        foreach (Entity Entity in GetEntities()) {
            DestroyEntity(Entity.Ref);
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
        Entity Entity = Scenes[EntityType].Instantiate<Entity>();
        Entity.Name = EntityId.ToString();
        Entity.SetProperties(Properties);
        Folders[EntityType].AddChild(Entity);
    }
    [Rem(RemAccess.Authority)]
    protected void DespawnRem(string EntityType, Guid EntityId) {
        Entity Entity = GetEntity(EntityType, EntityId);
        Entity.QueueFree();
    }

    private void _EntityAdded(Entity Entity, string EntityType) {
        // Ensure this is the multiplayer authority
        if (!IsActiveMultiplayerAuthority()) {
            return;
        }
        // Replicate entity spawn
        BroadcastSpawnRem(EntityType, Entity.Record.Id, Entity.GetChangedProperties());
    }
    private void _EntityRemoved(Entity Entity, string EntityType) {
        // Ensure this is the multiplayer authority
        if (!IsActiveMultiplayerAuthority()) {
            return;
        }
        // Replicate entity despawn
        BroadcastDespawnRem(EntityType, Entity.Record.Id);
    }
    private void _PeerAdded(int PeerId) {
        // Ensure this is the multiplayer authority
        if (!IsActiveMultiplayerAuthority()) {
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
    private bool IsActiveMultiplayerAuthority() {
        return Multiplayer?.MultiplayerPeer is not null && IsMultiplayerAuthority();
    }
}