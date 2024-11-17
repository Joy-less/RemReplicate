#nullable enable
#pragma warning disable IDE0130

using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Reflection;
using MemoryPack;
using ImmediateReflection;

namespace RemReplicate;

public sealed class Property {
    public readonly object Target;
    public readonly string Name;
    public readonly Type Type;
    public readonly Func<object?> Get;
    public readonly Action<object?> Set;
    public int Owner = 1;

    private const BindingFlags Bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    public Property(object Target, ImmediateField Field) {
        this.Target = Target;
        Name = Field.Name;
        Type = Field.FieldType;
        Get = () => Field.GetValue(Target);
        Set = Value => Field.SetValue(Target, Value);
    }
    public Property(object Target, ImmediateProperty Property) {
        this.Target = Target;
        Name = Property.Name;
        Type = Property.PropertyType;
        Get = () => Property.GetValue(Target);
        Set = Value => Property.SetValue(Target, Value);
    }

    public static FrozenDictionary<string, Property> GetProperties(object Target) {
        // Get fast reflection interface for target
        ImmediateType TargetType = TypeAccessor.Get(Target.GetType(), Bindings);
        // Create properties dictionary
        Dictionary<string, Property> Properties = [];

        // Add fields
        foreach (ImmediateField Field in TargetType.GetFields()) {
            // Ignore read-only fields
            if (Field.FieldInfo.IsInitOnly) {
                continue;
            }
            // Ignore properties which MemoryPack would ignore
            if (Field.IsDefined<MemoryPackIgnoreAttribute>()) {
                continue;
            }
            // Add field
            Properties[Field.Name] = new Property(Target, Field);
        }

        // Add properties
        foreach (ImmediateProperty Property in TargetType.GetProperties()) {
            // Ignore read-only and write-only properties
            if (!Property.CanRead || !Property.CanWrite) {
                continue;
            }
            // Ignore properties which MemoryPack would ignore
            if (Property.IsDefined<MemoryPackIgnoreAttribute>()) {
                continue;
            }
            // Add property
            Properties[Property.Name] = new Property(Target, Property);
        }

        // Freeze properties lookup for performance
        return Properties.ToFrozenDictionary();
    }
}