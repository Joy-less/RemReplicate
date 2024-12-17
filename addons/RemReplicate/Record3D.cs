#nullable enable
#pragma warning disable IDE0130

using Godot;

namespace RemReplicate;

public abstract partial record Record3D : Record {
    public Vector3 Position { get; set; } = new();
    public Vector3 Rotation { get; set; } = new();
    public Vector3 Scale { get; set; } = new(1, 1, 1);
}