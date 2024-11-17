#nullable enable
#pragma warning disable IDE0130

using Godot;

namespace RemReplicate;

public abstract partial record Record3D : Record {
    public Vector3 Position = new();
    public Vector3 Rotation = new();
    public Vector3 Scale = new(1, 1, 1);
}