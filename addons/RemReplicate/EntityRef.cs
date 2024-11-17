#nullable enable

using System;
using MemoryPack;

namespace RemReplicate;

[MemoryPackable]
public partial record EntityRef(string Class, Guid Id) {
    public readonly string Class = Class;
    public readonly Guid Id = Id;
}