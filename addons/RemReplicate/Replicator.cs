#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using RemSend;

namespace RemReplicate;

/// <summary>
/// A node that replicates its child <see cref="Entity"/>'s.<br/>
/// The authority remotely spawns and despawns entities, and every peer remotely changes the properties it owns.
/// </summary>
[GlobalClass]
public partial class Replicator : Node {
    /// <summary>
    /// The number of times per second to check and broadcast changed properties.
    /// </summary>
    [Export] public double ReplicateHz { get; set; } = 20;

    /// <summary>
    /// A reference to a single instance of <see cref="Replicator"/>, useful if you only have one.
    /// </summary>
    public static Replicator Singleton { get; private set; } = null!;

    /// <summary>
    /// Constructs a replicator.
    /// </summary>
    public Replicator() {
        // Set as singleton
        Singleton = this;
    }
    /// <summary>
    /// Initializes the replicator.
    /// </summary>
    public override void _Ready() {
        // Initialize replicator once
        Initialize();
    }
    /// <inheritdoc cref="SpawnEntity{TEntity}(TEntity)"/>
    public Entity SpawnEntity(Entity Entity) {
        return SpawnEntity<Entity>(Entity);
    }
    /// <summary>
    /// Adds an entity to the replicator.
    /// </summary>
    public TEntity SpawnEntity<TEntity>(TEntity Entity) where TEntity : Entity {
        // Set entity name to ID
        Entity.Name = Entity.Id.ToString();
        // Add entity
        AddChild(Entity);
        return Entity;
    }
    /// <inheritdoc cref="SpawnEntity{TEntity}(string, Action{TEntity}?)"/>
    public Entity SpawnEntity(string ScenePath, Action<Entity>? Setup = null) {
        return SpawnEntity<Entity>(ScenePath, Setup);
    }
    /// <summary>
    /// Instantiates an entity from <paramref name="ScenePath"/> and adds it to the replicator.
    /// </summary>
    public TEntity SpawnEntity<TEntity>(string ScenePath, Action<TEntity>? Setup = null) where TEntity : Entity {
        // Instantiate entity from scene
        TEntity Entity = GD.Load<PackedScene>(ScenePath).Instantiate<TEntity>();
        // Setup entity
        Setup?.Invoke(Entity);
        // Spawn entity
        return SpawnEntity(Entity);
    }
    /// <summary>
    /// Removes an entity from the replicator.
    /// </summary>
    public void DespawnEntity(Entity Entity) {
        // Remove entity
        Entity.QueueFree();
    }
    /// <summary>
    /// Removes an entity with the given ID from the replicator.
    /// </summary>
    public bool DespawnEntity(Guid Id) {
        // Find entity
        if (GetEntity(Id) is not Entity Entity) {
            return false;
        }
        // Despawn entity
        DespawnEntity(Entity);
        return true;
    }
    /// <summary>
    /// Removes every entity from the replicator.
    /// </summary>
    public void DespawnEntities() {
        foreach (Entity Entity in GetEntities()) {
            DespawnEntity(Entity);
        }
    }
    /// <summary>
    /// Finds an entity with the given ID in the replicator.
    /// </summary>
    public Entity GetEntity(Guid Id) {
        return GetEntity<Entity>(Id);
    }
    /// <inheritdoc cref="GetEntity(Guid)"/>
    public TEntity GetEntity<TEntity>(Guid Id) where TEntity : Entity {
        // Find entity by ID
        return GetNode<TEntity>(Id.ToString());
    }
    /// <summary>
    /// Tries to find an entity with the given ID in the replicator.
    /// </summary>
    public Entity? GetEntityOrNull(Guid? Id) {
        return GetEntityOrNull<Entity>(Id);
    }
    /// <inheritdoc cref="GetEntityOrNull(Guid?)"/>
    public TEntity? GetEntityOrNull<TEntity>(Guid? Id) where TEntity : Entity {
        // Ensure ID is not null
        if (Id is null) {
            return null;
        }
        // Find entity by ID
        return GetNodeOrNull<TEntity>(Id.Value.ToString());
    }
    /// <summary>
    /// Checks if there's an entity with the given ID in the replicator.
    /// </summary>
    public bool HasEntity(Guid? Id) {
        return GetEntityOrNull(Id) is not null;
    }
    /// <inheritdoc cref="HasEntity(Guid?)"/>
    public bool HasEntity<TEntity>(Guid? Id) where TEntity : Entity {
        return GetEntityOrNull<TEntity>(Id) is not null;
    }
    /// <summary>
    /// Finds every entity in the replicator.
    /// </summary>
    public IEnumerable<Entity> GetEntities() {
        return GetEntities<Entity>();
    }
    /// <inheritdoc cref="GetEntities()"/>
    public IEnumerable<TEntity> GetEntities<TEntity>() where TEntity : Entity {
        return GetChildren().OfType<TEntity>();
    }
    /// <summary>
    /// Returns the entities in the replicator nearest to the position.
    /// </summary>
    public IEnumerable<TEntity> GetNearestEntities<TEntity>(Vector3 Position, double MaxDistance = double.PositiveInfinity) where TEntity : Entity3D {
        return GetEntities<TEntity>()
            .Where(Entity => Entity.DistanceTo(Position) <= MaxDistance)
            .OrderBy(Entity => Entity.DistanceTo(Position));
    }
    /// <summary>
    /// Returns the entities in the replicator nearest to the position.
    /// </summary>
    public IEnumerable<TEntity> GetNearestEntities<TEntity>(Vector2 Position, double MaxDistance = double.PositiveInfinity) where TEntity : Entity2D {
        return GetEntities<TEntity>()
            .Where(Entity => Entity.DistanceTo(Position) <= MaxDistance)
            .OrderBy(Entity => Entity.DistanceTo(Position));
    }
    /// <summary>
    /// Returns the unique ID of the local peer.
    /// </summary>
    public int GetMultiplayerId() {
        return Multiplayer.GetUniqueId();
    }
    /// <summary>
    /// Returns the unique ID of the local peer if active.
    /// </summary>
    public int? GetMultiplayerIdOrNull() {
        // Ensure multiplayer is active
        if (!IsInstanceValid(Multiplayer.MultiplayerPeer)) {
            return null;
        }
        return GetMultiplayerId();
    }
    /// <summary>
    /// Returns whether the local peer has the given ID.
    /// </summary>
    public bool IsMultiplayerId(int PeerId) {
        return GetMultiplayerIdOrNull() == PeerId;
    }

    private void Initialize() {
        // Ensure RemSendService is initialized
        RuntimeHelpers.RunClassConstructor(typeof(RemSendService).TypeHandle);

        // Server: On entity added, replicate spawn entity
        ChildEnteredTree += (Node Child) => {
            // Ensure child is an entity
            if (Child is not Entity Entity) {
                return;
            }
            // Ensure this is the server
            if (!IsMultiplayerId(1)) {
                return;
            }
            // Broadcast spawn
            BroadcastSpawnEntityRem(Entity.Id, Entity.SceneFilePath, Entity.GetPropertyValues(), Entity.GetPropertyOwners());
        };

        // Server: On entity removed, replicate despawn entity
        ChildExitingTree += (Node Child) => {
            // Ensure child is an entity
            if (Child is not Entity Entity) {
                return;
            }
            // Ensure this is the server
            if (!IsMultiplayerId(1)) {
                return;
            }
            // Broadcast despawn
            BroadcastDespawnEntityRem(Entity.Id);
        };

        // Server: On client connect, replicate spawn all entities
        Multiplayer.PeerConnected += (long PeerId) => {
            // Ensure this is the server
            if (!IsMultiplayerId(1)) {
                return;
            }
            // Replicate all entities to peer
            foreach (Entity Entity in GetEntities()) {
                // Replicate entity
                SendSpawnEntityRem((int)PeerId, Entity.Id, Entity.SceneFilePath, Entity.GetPropertyValues(), Entity.GetPropertyOwners());
            }
        };
    }

    /// <summary>
    /// Remotely spawns the given entity.
    /// </summary>
    [Rem(RemAccess.Authority)]
    private void SpawnEntityRem(Guid EntityId, string ScenePath, Dictionary<string, byte[]> PropertyValues, Dictionary<string, int> PropertyOwners) {
        // Setup entity properties
        void SetupEntity(Entity Entity) {
            // Set entity ID
            Entity.Id = EntityId;
            // Set entity property values
            Entity.SetPropertyValues(PropertyValues);
            // Set entity property owners
            foreach ((string PropertyName, int PropertyOwner) in PropertyOwners) {
                Entity.GetProperty(PropertyName).Owner = PropertyOwner;
            }
        }

        // Entity exists already
        if (GetEntityOrNull(EntityId) is Entity Entity) {
            SetupEntity(Entity);
        }
        // Spawn entity
        else {
            SpawnEntity(ScenePath, SetupEntity);
        }
    }
    /// <summary>
    /// Remotely despawns the given entity.
    /// </summary>
    [Rem(RemAccess.Authority)]
    private void DespawnEntityRem(Guid EntityId) {
        // Despawn entity by ID
        DespawnEntity(EntityId);
    }
}