#nullable enable

using Godot;
using RemSend;

namespace RemReplicate;

/// <summary>
/// A node that is spawned and updated remotely by a <see cref="Replicator"/> with a 2D transform.
/// </summary>
public abstract partial class Entity2D : Entity {
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
    [RemoteProperty] public Vector2 RemotePosition { get; set; } = Vector2.Zero;
    /// <summary>
    /// The rotation remotely sent or received.
    /// </summary>
    [RemoteProperty] public float RemoteRotation { get; set; } = 0;
    /// <summary>
    /// The scale remotely sent or received.
    /// </summary>
    [RemoteProperty] public Vector2 RemoteScale { get; set; } = Vector2.One;

    /// <summary>
    /// The 2D node.
    /// </summary>
    public abstract Node2D Node2D { get; }

    /// <inheritdoc cref="Node2D.Position"/>
    public Vector2 Position { get => Node2D.Position; set => Node2D.Position = value; }
    /// <inheritdoc cref="Node2D.Rotation"/>
    public float Rotation { get => Node2D.Rotation; set => Node2D.Rotation = value; }
    /// <inheritdoc cref="Node2D.Scale"/>
    public Vector2 Scale { get => Node2D.Scale; set => Node2D.Scale = value; }

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
            
            Rotation = Mathf.Lerp(Rotation, RemoteRotation, RotationWeight);
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
    public float DistanceTo(Vector2 OtherPosition) {
        return Position.DistanceTo(OtherPosition);
    }
    /// <summary>
    /// Returns the distance from the entity to <paramref name="OtherEntity"/>.
    /// </summary>
    public float DistanceTo(Entity2D OtherEntity) {
        return DistanceTo(OtherEntity.Position);
    }
    /// <summary>
    /// Immediately moves the entity to the given transform remotely without interpolating.
    /// </summary>
    public void Teleport(Vector2? Position = null, float? Rotation = null, Vector2? Scale = null) {
        BroadcastTeleportRem(Position, Rotation, Scale);
    }

    /// <summary>
    /// Immediately moves the entity to the given transform remotely without interpolating.
    /// </summary>
    [Rem(RemAccess.Any, CallLocal = true)]
    private void TeleportRem([Sender] int SenderId, Vector2? Position, float? Rotation, Vector2? Scale) {
        // Set properties to arguments if authorized
        if (Position is not null && IsPropertyOwner(nameof(RemotePosition), SenderId)) {
            this.Position = RemotePosition = Position.Value;
        }
        if (Rotation is not null && IsPropertyOwner(nameof(RemoteRotation), SenderId)) {
            this.Rotation = RemoteRotation = Rotation.Value;
        }
        if (Scale is not null && IsPropertyOwner(nameof(RemoteScale), SenderId)) {
            this.Scale = RemoteScale = Scale.Value;
        }
    }
}