#nullable enable

using Godot;
using RemSend;

namespace RemReplicate;

/// <summary>
/// A node that is spawned and updated remotely by a <see cref="Replicator"/> with a 3D transform.
/// </summary>
public abstract partial class Entity3D : Entity {
    /// <summary>
    /// The weight for interpolating to the remote position.
    /// </summary>
    [Export] public float PositionWeight { get; set; } = 0.3f;
    /// <summary>
    /// The weight for interpolating to the remote rotation.
    /// </summary>
    [Export] public float RotationWeight { get; set; } = 0.7f;
    /// <summary>
    /// The weight for interpolating to the remote scale.
    /// </summary>
    [Export] public float ScaleWeight { get; set; } = 1f;

    /// <summary>
    /// The position remotely sent or received.
    /// </summary>
    [RemoteProperty] public Vector3 RemotePosition { get; set; } = Vector3.Zero;
    /// <summary>
    /// The rotation remotely sent or received.
    /// </summary>
    [RemoteProperty] public Vector3 RemoteRotation { get; set; } = Vector3.Zero;
    /// <summary>
    /// The scale remotely sent or received.
    /// </summary>
    [RemoteProperty] public Vector3 RemoteScale { get; set; } = Vector3.One;

    /// <summary>
    /// The 3D node.
    /// </summary>
    public abstract Node3D Node3D { get; }

    /// <inheritdoc cref="Node3D.Position"/>
    public Vector3 Position { get => Node3D.Position; set => Node3D.Position = value; }
    /// <inheritdoc cref="Node3D.Rotation"/>
    public Vector3 Rotation { get => Node3D.Rotation; set => Node3D.Rotation = value; }
    /// <inheritdoc cref="Node3D.Scale"/>
    public Vector3 Scale { get => Node3D.Scale; set => Node3D.Scale = value; }

    /// <inheritdoc/>
    public override void _Ready() {
        base._Ready();

        // Set local transform to initial remote transform
        Position = RemotePosition;
        Rotation = RemoteRotation;
        Scale = RemoteScale;
    }
    /// <inheritdoc/>
    public override void _PhysicsProcess(double Delta) {
        base._PhysicsProcess(Delta);

        // Replicate position
        if (IsPropertyOwner(nameof(RemotePosition))) {
            RemotePosition = Position;
        }
        // Interpolate to position
        else {
            Position = Position.Lerp(RemotePosition, PositionWeight);
        }

        // Replicate rotation
        if (IsPropertyOwner(nameof(RemoteRotation))) {
            RemoteRotation = Rotation;
        }
        // Interpolate to rotation
        else {
            Rotation = Rotation.Lerp(RemoteRotation, RotationWeight);
        }

        // Replicate scale
        if (IsPropertyOwner(nameof(RemoteScale))) {
            RemoteScale = Scale;
        }
        // Interpolate to scale
        else {
            Scale = Scale.Lerp(RemoteScale, ScaleWeight);
        }
    }
    /// <summary>
    /// Returns the distance from the entity to <paramref name="OtherPosition"/>.
    /// </summary>
    public float DistanceTo(Vector3 OtherPosition) {
        return Position.DistanceTo(OtherPosition);
    }
    /// <summary>
    /// Returns the distance from the entity to <paramref name="OtherEntity"/>.
    /// </summary>
    public float DistanceTo(Entity3D OtherEntity) {
        return DistanceTo(OtherEntity.Position);
    }
    /// <summary>
    /// Immediately moves the entity to the given transform remotely without interpolating.
    /// </summary>
    public void Teleport(Vector3? Position = null, Vector3? Rotation = null, Vector3? Scale = null) {
        BroadcastTeleportRem(Position, Rotation, Scale);
    }

    /// <summary>
    /// Immediately moves the entity to the given transform remotely without interpolating.
    /// </summary>
    [Rem(RemAccess.Any, CallLocal = true)]
    private void TeleportRem([Sender] int SenderId, Vector3? Position, Vector3? Rotation, Vector3? Scale) {
        // Set properties to arguments if authorized
        if (Position is not null && (SenderId is 0 || SenderId == Multiplayer.GetUniqueId() || IsPropertyOwner(nameof(RemotePosition), SenderId))) {
            this.Position = RemotePosition = Position.Value;
        }
        if (Rotation is not null && (SenderId is 0 || SenderId == Multiplayer.GetUniqueId() || IsPropertyOwner(nameof(RemoteRotation), SenderId))) {
            this.Rotation = RemoteRotation = Rotation.Value;
        }
        if (Scale is not null && (SenderId is 0 || SenderId == Multiplayer.GetUniqueId() || IsPropertyOwner(nameof(RemoteScale), SenderId))) {
            this.Scale = RemoteScale = Scale.Value;
        }
    }
}