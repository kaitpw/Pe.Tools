using Pe.FamilyFoundry.Snapshots;
using System.ComponentModel.DataAnnotations;

namespace Pe.FamilyFoundry.OperationSettings;

public class MakeRefPlaneAndDimsSettings : IOperationSettings {
    [Required] public List<RefPlaneSpec> Specs { get; init; } = [];

    public bool Enabled { get; init; } = true;
}