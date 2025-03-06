using Godot;
using RemReplicate;

namespace Sample;

public partial class CubeEntity : Entity3D {
    [Export] public required MeshInstance3D Mesh { get; set; }

    public const string ScenePath = "res://Sample/Scenes/Cube.tscn";
    public const double Distance = 2;
    public const double Speed = 1;

    [RemoteProperty] public Color Color { get; set; }

    private double Counter = 0;
    private bool Direction = true;

    public override void _PhysicsProcess(double Delta) {
        base._PhysicsProcess(Delta);

        if (IsPropertyOwner(nameof(RemotePosition))) {
            if (Direction) {
                Counter += Delta * Speed;

                if (Counter >= Distance) {
                    Counter = Distance;
                    Direction = !Direction;
                }
            }
            else {
                Counter -= Delta * Speed;

                if (Counter <= -Distance) {
                    Counter = -Distance;
                    Direction = !Direction;
                }
            }

            Position = new Vector3(0, (float)Counter, 0);
        }

        if (IsPropertyOwner(nameof(Color))) {
            if (Counter == 0) {
                Color = Color.FromHsv(GD.Randf(), 0.5f, 0.5f);
            }
        }
    }
    public override void _PropertyReplicated(string PropertyName) {
        if (PropertyName is nameof(Color)) {
            ((StandardMaterial3D)Mesh.MaterialOverride).AlbedoColor = Color;
        }
    }

    public override Node3D Node3D => Mesh;
}