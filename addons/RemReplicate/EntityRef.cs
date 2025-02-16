#nullable enable

using System;
using MemoryPack;

namespace RemReplicate;

[MemoryPackable]
public partial record EntityRef(string Class, Guid Id) {
    public string Class { get; } = Class;
    public Guid Id { get; } = Id;
}