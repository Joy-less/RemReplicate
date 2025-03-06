#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MemoryPack;
using RemSend;

namespace RemReplicate;

/// <summary>
/// A node that is spawned and updated remotely by a <see cref="Replicator"/>.
/// </summary>
public abstract partial class Entity : Node {
    /// <summary>
    /// Emitted when a property is set by the remote owner.
    /// </summary>
    [Signal] public delegate void PropertyReplicatedEventHandler(string PropertyName);

    /// <summary>
    /// The unique ID for the entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    private readonly Dictionary<string, RemoteProperty> Properties;
    private readonly Dictionary<string, byte[]> PreviousPropertyValues = [];
    private double TimeUntilReplicateProperties = 0;

    /// <summary>
    /// Constructs an entity.
    /// </summary>
    public Entity() {
        // Get initial remote properties
        Properties = RemoteProperty.GetProperties(this);

        // Hook virtual methods to events
        PropertyReplicated += _PropertyReplicated;
    }
    /// <summary>
    /// Called when ready to initialize the entity.
    /// </summary>
    public override void _Ready() {
    }
    /// <summary>
    /// Called every frame to update the entity.
    /// <list type="bullet">
    ///   <item>Changed property values are broadcasted.</item>
    /// </list>
    /// </summary>
    public override void _Process(double Delta) {
        // Replicate properties every interval
        TimeUntilReplicateProperties -= Delta;
        if (TimeUntilReplicateProperties <= 0) {
            TimeUntilReplicateProperties = 1.0 / GetReplicator().ReplicateHz;
            BroadcastChangedPropertyValues();
        }
    }
    /// <summary>
    /// Called every physics frame to update the entity.
    /// </summary>
    public override void _PhysicsProcess(double Delta) {
    }
    /// <summary>
    /// Returns the replicator controlling this entity (this entity's parent).
    /// </summary>
    public Replicator GetReplicator() {
        Node? CurrentNode = this;
        while (CurrentNode is not null) {
            if (CurrentNode is Replicator RemReplicate) {
                return RemReplicate;
            }
            CurrentNode = CurrentNode.GetParent();
        }
        return null!;
    }
    /// <summary>
    /// Immediately broadcasts any changed remote property values owned by the local peer.
    /// </summary>
    public void BroadcastChangedPropertyValues() {
        ForEachChangedPropertyValue((string PropertyName, byte[] PropertyValue) => {
            // Broadcast new property value
            BroadcastSetPropertyValueRem(PropertyName, PropertyValue);
        });
    }
    /// <summary>
    /// Returns the peer ID that owns the remote property.
    /// </summary>
    public int GetPropertyOwner(string PropertyName) {
        // Get property by name
        RemoteProperty Property = Properties[PropertyName];
        // Return property network owner peer ID
        return Property.Owner;
    }
    /// <summary>
    /// Returns whether <paramref name="PeerId"/> owns the remote property.
    /// </summary>
    public bool IsPropertyOwner(string PropertyName, int PeerId) {
        // Check if the given peer owns the property
        return GetPropertyOwner(PropertyName) == PeerId;
    }
    /// <summary>
    /// Returns whether the local peer owns the remote property.
    /// </summary>
    public bool IsPropertyOwner(string PropertyName) {
        // Check if the local peer owns the property
        return IsPropertyOwner(PropertyName, GetReplicator().Multiplayer.GetUniqueId());
    }
    /// <summary>
    /// Changes the owner of the remote property, broadcasting the new owner.
    /// </summary>
    /// <remarks>
    /// This can only be called by the multiplayer authority.
    /// </remarks>
    public void SetPropertyOwner(string PropertyName, int PeerId) {
        // Get property by name
        RemoteProperty Property = Properties[PropertyName];
        if (Property.Owner == PeerId) {
            return;
        }
        // Set new property owner
        Property.Owner = PeerId;
        // Broadcast new property owner
        BroadcastSetPropertyOwnerRem(PropertyName, PeerId);
    }
    /// <summary>
    /// Returns the owners of each remote property.<br/>
    /// If <paramref name="ExcludeDefault"/> is <see langword="true"/>, properties owned by the authority (the default owner) are excluded.
    /// </summary>
    public Dictionary<string, int> GetPropertyOwners(bool ExcludeDefault = true) {
        Dictionary<string, int> PropertyOwners = [];
        // Add each property owner
        foreach ((string Name, RemoteProperty Property) in Properties) {
            // Skip properties owned by the authority (the default owner)
            if (ExcludeDefault && Property.Owner is 1) {
                continue;
            }
            PropertyOwners[Name] = Property.Owner;
        }
        return PropertyOwners;
    }
    /// <summary>
    /// Returns the values of each remote property.
    /// </summary>
    public Dictionary<string, byte[]> GetPropertyValues() {
        Dictionary<string, byte[]> PropertyValues = [];
        // Add each property value
        foreach ((string Name, RemoteProperty Property) in Properties) {
            PropertyValues[Name] = MemoryPackSerializer.Serialize(Property.Type, Property.Get());
        }
        return PropertyValues;
    }
    /// <summary>
    /// Sets the (serialized) value of the remote property.
    /// </summary>
    public void SetPropertyValue(string Name, byte[] Value) {
        // Deserialise and set property
        RemoteProperty Property = Properties[Name];
        Property.Set(MemoryPackSerializer.Deserialize(Property.Type, Value));
        // Invoke replicated event for property
        EmitSignalPropertyReplicated(Name);
    }
    /// <summary>
    /// Sets the (serialized) values of the remote properties.
    /// </summary>
    public void SetPropertyValues(IDictionary<string, byte[]> Entries) {
        // Set each property
        foreach ((string Name, byte[] Value) in Entries) {
            // Deserialise and set property
            RemoteProperty Property = Properties[Name];
            Property.Set(MemoryPackSerializer.Deserialize(Property.Type, Value));
        }
        // Invoke replicated event for each property
        foreach (string Name in Entries.Keys) {
            EmitSignalPropertyReplicated(Name);
        }
    }
    /// <summary>
    /// Invokes <paramref name="Callback"/> for each property value changed since the last broadcast.
    /// </summary>
    public void ForEachChangedPropertyValue(Action<string, byte[]> Callback) {
        // Check each property
        foreach ((string PropertyName, RemoteProperty Property) in Properties) {
            // Ensure local peer is property owner
            if (!IsPropertyOwner(PropertyName)) {
                continue;
            }
            // Get and serialise property value
            byte[] Value = MemoryPackSerializer.Serialize(Property.Type, Property.Get());
            // Ensure property changed
            if (PreviousPropertyValues.TryGetValue(PropertyName, out byte[]? PreviousValue) && PreviousValue.SequenceEqual(Value)) {
                continue;
            }
            // Store new property value
            PreviousPropertyValues[PropertyName] = Value;
            // Report property as changed
            Callback(PropertyName, Value);
        }
    }
    /// <summary>
    /// Returns each property value changed since the last broadcast.
    /// </summary>
    public Dictionary<string, byte[]> GetChangedPropertyValues() {
        Dictionary<string, byte[]> ChangedProperties = [];
        ForEachChangedPropertyValue((string Name, byte[] Value) => {
            ChangedProperties[Name] = Value;
        });
        return ChangedProperties;
    }
    /// <summary>
    /// Returns the remote properties for this entity.
    /// </summary>
    public IReadOnlyDictionary<string, RemoteProperty> GetProperties() {
        return Properties;
    }
    /// <summary>
    /// Returns the remote property for this entity.
    /// </summary>
    public RemoteProperty GetProperty(string PropertyName) {
        return Properties[PropertyName];
    }

    /// <inheritdoc cref="PropertyReplicated"/>
    public virtual void _PropertyReplicated(string PropertyName) {
    }

    /// <summary>
    /// Remotely sets the owner of the remote property.
    /// </summary>
    [Rem(RemAccess.Authority)]
    internal void SetPropertyOwnerRem(string PropertyName, int PropertyOwner) {
        // Set property owner
        RemoteProperty Property = Properties[PropertyName];
        Property.Owner = PropertyOwner;
    }
    /// <summary>
    /// Remotely sets the value of the remote property.
    /// </summary>
    [Rem(RemAccess.Any)]
    private void SetPropertyValueRem([Sender] int SenderId, string PropertyName, byte[] PropertyValue) {
        // Ensure sender owns property
        RemoteProperty Property = Properties[PropertyName];
        if (Property.Owner != SenderId) {
            throw new InvalidOperationException($"Peer({SenderId}) tried to replicate property it doesn't own: '{Property.Name}'");
        }

        // Set property value
        SetPropertyValue(PropertyName, PropertyValue);
    }
}