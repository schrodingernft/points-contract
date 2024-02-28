namespace Points.Contracts.Point;

public partial class PointsContract
{
    private void AssertAdmin() => Assert(Context.Sender == State.Admin.Value, "No permission.");
    private void AssertInitialized() => Assert(State.Initialized.Value, "Not initialized.");
}