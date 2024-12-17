#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;

namespace RemReplicate;

public sealed class Property {
    public readonly object Target;
    public readonly string Name;
    public readonly Type Type;
    public readonly Func<object?> Get;
    public readonly Action<object?> Set;
    public int Owner = 1;

    private const BindingFlags Bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    public Property(object Target, FieldInfo Field) {
        this.Target = Target;
        Name = Field.Name;
        Type = Field.FieldType;
        Get = () => Field.GetValue(Target);
        Set = Value => Field.SetValue(Target, Value);
    }
    public Property(object Target, PropertyInfo Property) {
        this.Target = Target;
        Name = Property.Name;
        Type = Property.PropertyType;
        Get = () => Property.GetValue(Target);
        Set = Value => Property.SetValue(Target, Value);
    }

    public static Dictionary<string, Property> GetProperties(object Target) {
        Type TargetType = Target.GetType();
        Dictionary<string, Property> Properties = [];

        // Add fields
        foreach (FieldInfo Field in TargetType.GetFields(Bindings)) {
            // Ignore read-only fields
            if (Field.IsInitOnly) {
                continue;
            }
            // Ignore properties which MemoryPack would ignore
            if (Field.GetCustomAttribute<MemoryPackIgnoreAttribute>() is not null) {
                continue;
            }
            // Add field
            Properties[Field.Name] = new Property(Target, Field);
        }

        // Add properties
        foreach (PropertyInfo Property in TargetType.GetProperties(Bindings)) {
            // Ignore read-only and write-only properties
            if (!Property.CanRead || !Property.CanWrite) {
                continue;
            }
            // Ignore properties which MemoryPack would ignore
            if (Property.GetCustomAttribute<MemoryPackIgnoreAttribute>() is not null) {
                continue;
            }
            // Add property
            Properties[Property.Name] = new Property(Target, Property);
        }

        return Properties;
    }
}