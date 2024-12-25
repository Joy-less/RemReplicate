#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MemoryPack;

namespace RemReplicate;

public abstract partial class Entity : Node {
    [Signal] public delegate void ReplicateEventHandler(string PropertyName);

    public abstract Record Record { get; }
    public abstract void SetRecord(Record Value);
    public EntityRef Ref => CachedRef ??= new EntityRef(GetEntityType(), Record.Id);

    internal Dictionary<string, Property> Properties => CachedProperties ??= Property.GetProperties(Record);

    private readonly Dictionary<string, byte[]> PreviousProperties = [];
    private Dictionary<string, Property>? CachedProperties;
    private EntityRef? CachedRef;
    private double TimeUntilReplicate;

    public virtual void _Replicate(string PropertyName) {
    }

    public override void _Process(double Delta) {
        // Replicate every interval
        TimeUntilReplicate -= Delta;
        if (TimeUntilReplicate <= 0) {
            TimeUntilReplicate = 1.0 / GetReplicator().ReplicateHz;
            ReplicateChangedProperties();
        }
    }
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
    public string GetEntityType() {
        return Replicator.GetEntityTypeFromTypeOfEntity(GetType());
    }
    public void ReplicateChangedProperties() {
        ForEachChangedProperty((string Name, byte[] Value) => {
            Rem(() => SetPropertyRem(Name, Value));
        });
    }
    public int GetPropertyOwner(string PropertyName) {
        // Get property by name
        Property Property = Properties[PropertyName];
        // Return property network owner peer ID
        return Property.Owner;
    }
    public bool IsPropertyOwner(string PropertyName, int PeerId) {
        // Check if the given peer owns the property
        return GetPropertyOwner(PropertyName) == PeerId;
    }
    public bool IsPropertyOwner(string PropertyName) {
        // Ensure local peer exists
        if (GetReplicator()?.Multiplayer?.MultiplayerPeer is not MultiplayerPeer LocalPeer) {
            return false;
        }
        // Check if the local peer owns the property
        return IsPropertyOwner(PropertyName, LocalPeer.GetUniqueId());
    }
    public void SetPropertyOwner(string PropertyName, int PeerId) {
        // Get property by name
        Property Property = Properties[PropertyName];
        if (Property.Owner == PeerId) {
            return;
        }
        Rem(() => SetPropertyOwnerRem(PropertyName, PeerId));
    }
    public Dictionary<string, byte[]> GetProperties() {
        Dictionary<string, byte[]> All = [];
        // Check each property
        foreach ((string Name, Property Property) in Properties) {
            // Get and serialise property value
            byte[] Value = MemoryPackSerializer.Serialize(Property.Type, Property.Get());
            // Add property to delta
            All[Name] = Value;
        }
        return All;
    }
    public void SetProperty(string Name, byte[] Value) {
        // Deserialise and set property
        Property Property = Properties[Name];
        Property.Set(MemoryPackSerializer.Deserialize(Property.Type, Value));
        // Invoke replicated event for each property
        EmitSignal(SignalName.Replicate, Name);
        _Replicate(Name);
    }
    public void SetProperties(IDictionary<string, byte[]> Entries) {
        // Set each property
        foreach ((string Name, byte[] Value) in Entries) {
            // Deserialise and set property
            Property Property = Properties[Name];
            Property.Set(MemoryPackSerializer.Deserialize(Property.Type, Value));
        }
        // Invoke replicated event for each property
        foreach (string Name in Entries.Keys) {
            EmitSignal(SignalName.Replicate, Name);
            _Replicate(Name);
        }
    }
    public void ForEachChangedProperty(Action<string, byte[]> Callback) {
        // Check each property
        foreach ((string PropertyName, Property Property) in Properties) {
            // Ensure local peer is property owner
            if (!IsPropertyOwner(PropertyName)) {
                continue;
            }
            // Get and serialise property value
            byte[] Value = MemoryPackSerializer.Serialize(Property.Type, Property.Get());
            // Ensure property changed
            if (PreviousProperties.TryGetValue(PropertyName, out byte[]? PreviousValue) && PreviousValue.SequenceEqual(Value)) {
                continue;
            }
            // Store new property value
            PreviousProperties[PropertyName] = Value;
            // Report property as changed
            Callback(PropertyName, Value);
        }
    }
    public Dictionary<string, byte[]> GetChangedProperties() {
        Dictionary<string, byte[]> ChangedProperties = [];
        ForEachChangedProperty((string Name, byte[] Value) => {
            ChangedProperties[Name] = Value;
        });
        return ChangedProperties;
    }

    [Rem(RemAccess.Authority, CallLocal = true)]
    internal void SetPropertyOwnerRem(string PropertyName, int Owner) {
        Property Property = Properties[PropertyName];
        Property.Owner = Owner;
    }
    [Rem(RemAccess.Any)]
    internal void SetPropertyRem(string Name, byte[] Value) {
        int SenderId = Multiplayer.GetRemoteSenderId();

        // Ensure sender owns property
        Property Property = Properties[Name];
        // Note: The authority can't replicate properties it doesn't own, because the owner will overwrite it
        if (Property.Owner != SenderId) {
            throw new InvalidOperationException($"Peer tried to replicate property it doesn't own: '{Property.Name}' ({SenderId})");
        }

        // Apply property value
        SetProperty(Name, Value);
    }
}