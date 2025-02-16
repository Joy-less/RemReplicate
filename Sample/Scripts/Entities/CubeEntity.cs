using Godot;
using RemReplicate;

public partial class CubeEntity : Entity3D {
    [Export] public required MeshInstance3D Mesh { get; set; }

    private CubeRecord _Record = new();

    private double Counter = 0;
    private bool Direction = true;

    private const double Distance = 2;
    private const double Speed = 1;

    public override void _PhysicsProcess(double Delta) {
        base._PhysicsProcess(Delta);

        if (IsPropertyOwner("Position")) {
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

        if (IsPropertyOwner("Color")) {
            if (Counter == 0) {
                Record.Color = Color.FromHsv(GD.Randf(), 0.5f, 0.5f);
            }
        }
    }
    public override void _Replicate(string PropertyName) {
        if (PropertyName == "Color") {
            ((StandardMaterial3D)Mesh.MaterialOverride).AlbedoColor = Record.Color;
        }
    }

    public override CubeRecord Record => _Record;
    public override void SetRecord(Record Value) => _Record = (CubeRecord)Value;
    public override Node3D Node3D => Mesh;
}