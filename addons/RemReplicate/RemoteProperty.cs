#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace RemReplicate;

/// <summary>
/// A property in <see cref="RemReplicate.Entity"/> for remote replication.
/// </summary>
public sealed class RemoteProperty {
    /// <summary>
    /// The entity which has the property.
    /// </summary>
    public Entity Entity { get; }
    /// <summary>
    /// The name of the property.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// The type of the property.
    /// </summary>
    public Type Type { get; }
    /// <summary>
    /// A method that gets the value of the property.
    /// </summary>
    public Func<object?> Get { get; }
    /// <summary>
    /// A method that sets the value of the property.
    /// </summary>
    public Action<object?> Set { get; }
    /// <summary>
    /// The remote owner of the property.
    /// </summary>
    public int Owner { get; set; } = 1;

    /// <summary>
    /// The binding flags used to find fields/properties.
    /// </summary>
    private const BindingFlags Bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Constructs a remote property over a field.
    /// </summary>
    public RemoteProperty(Entity Entity, PropertyInfo PropertyInfo) {
        this.Entity = Entity;
        Name = PropertyInfo.Name;
        Type = PropertyInfo.PropertyType;
        Get = () => PropertyInfo.GetValue(Entity);
        Set = Value => PropertyInfo.SetValue(Entity, Value);
    }
    /// <summary>
    /// Constructs a remote property over a property.
    /// </summary>
    public RemoteProperty(Entity Entity, FieldInfo FieldInfo) {
        this.Entity = Entity;
        Name = FieldInfo.Name;
        Type = FieldInfo.FieldType;
        Get = () => FieldInfo.GetValue(Entity);
        Set = Value => FieldInfo.SetValue(Entity, Value);
    }

    /// <summary>
    /// Finds and returns the remote properties of the entity.
    /// </summary>
    public static Dictionary<string, RemoteProperty> GetProperties(Entity Entity) {
        Type EntityType = Entity.GetType();
        Dictionary<string, RemoteProperty> Properties = [];

        // Add properties
        foreach (PropertyInfo PropertyInfo in EntityType.GetProperties(Bindings)) {
            // Ignore properties without remote attribute
            if (PropertyInfo.GetCustomAttribute<RemotePropertyAttribute>() is null) {
                continue;
            }
            // Ignore read-only and write-only properties
            if (!PropertyInfo.CanRead || !PropertyInfo.CanWrite) {
                continue;
            }
            // Add property
            Properties[PropertyInfo.Name] = new RemoteProperty(Entity, PropertyInfo);
        }

        // Add fields
        foreach (FieldInfo FieldInfo in EntityType.GetFields(Bindings)) {
            // Ignore fields without remote attribute
            if (FieldInfo.GetCustomAttribute<RemotePropertyAttribute>() is null) {
                continue;
            }
            // Ignore read-only fields
            if (FieldInfo.IsInitOnly) {
                continue;
            }
            // Add field
            Properties[FieldInfo.Name] = new RemoteProperty(Entity, FieldInfo);
        }

        return Properties;
    }
}