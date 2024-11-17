#nullable enable

using Godot;

namespace RemReplicate;

public abstract partial class Entity3D : Entity {
    public float PositionWeight = 0.3f;
    public float RotationWeight = 0.7f;
    public float ScaleWeight = 1f;

    public abstract override Record3D Record {get;}
    public abstract Node3D Node3D {get;}

    public override void _Ready() {
        base._Ready();

        // Teleport to record transform
        Position = Record.Position;
        Rotation = Record.Rotation;
        Scale = Record.Scale;
    }
    public override void _PhysicsProcess(double Delta) {
        base._PhysicsProcess(Delta);

        // Replicate position
        if (IsPropertyOwner(nameof(Record3D.Position))) {
            Record.Position = Position;
        }
        // Interpolate to position
        else {
            Position = Position.Lerp(Record.Position, PositionWeight);
        }

        // Replicate rotation
        if (IsPropertyOwner(nameof(Record3D.Rotation))) {
            Record.Rotation = Rotation;
        }
        // Interpolate to rotation
        else {
            Rotation = Rotation.Lerp(Record.Rotation, RotationWeight);
        }

        // Replicate scale
        if (IsPropertyOwner(nameof(Record3D.Scale))) {
            Record.Scale = Scale;
        }
        // Snap to scale
        else {
            Scale = Scale.Lerp(Record.Scale, ScaleWeight);
        }
    }
    public float DistanceTo(Vector3? OtherPosition) {
        if (OtherPosition is null) {
            return float.PositiveInfinity;
        }
        return Position.DistanceTo(OtherPosition.Value);
    }
    public float DistanceTo(Entity3D? OtherEntity) {
        return DistanceTo(OtherEntity?.Position);
    }

    public Vector3 Position {get => Node3D.Position; set => Node3D.Position = value;}
    public Vector3 Rotation {get => Node3D.Rotation; set => Node3D.Rotation = value;}
    public Vector3 Scale {get => Node3D.Scale; set => Node3D.Scale = value;}
}