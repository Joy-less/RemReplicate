#nullable enable

using System;
using MemoryPack;

namespace RemReplicate;

[MemoryPackable]
public readonly partial record struct EntityRef(string Class, Guid Id) {
    public string Class { get; } = Class;
    public Guid Id { get; } = Id;
}