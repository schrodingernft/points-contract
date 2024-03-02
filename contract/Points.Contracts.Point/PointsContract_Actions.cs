using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract : PointsContractContainer.PointsContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
        Assert(input.Admin == null || !input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");
        State.Admin.Value = input.Admin ?? Context.Sender;
        State.Initialized.Value = true;

        return new Empty();
    }

    public override Empty SetAdmin(Address input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

        State.Admin.Value = input;

        return new Empty();
    }

    public override Empty SetReservedDomainList(SetReservedDomainListInput input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input.ReservedDomainList?.Domains?.Count > 0, "Invalid reserved domain list count.");

        State.ReservedDomains.Value = input.ReservedDomainList;

        return new Empty();
    }

    public override Empty SetMaxApplyCount(Int32Value input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input is { Value: > 0 }, "Invalid input.");

        State.MaxApplyCount.Value = input.Value;
        return new Empty();
    }

    public override Empty CreatePoint(CreatePointInput input)
    {
        AssertInitialized();
        AssertAdmin();
        AssertValidCreateInput(input);

        var tokenName = input.TokenName;
        var decimals = input.Decimals;
        State.PointInfos[tokenName] = new PointInfo
        {
            TokenName = tokenName,
            Decimals = decimals
        };

        Context.Fire(new PointCreated
        {
            TokenName = tokenName,
            Decimals = decimals
        });
        return new Empty();
    }

    private void AssertValidCreateInput(CreatePointInput input)
    {
        Assert(input.TokenName.Length is > 0 and <= PointsContractConstants.TokenNameLength
               && input.Decimals is >= 0 and <= PointsContractConstants.MaxDecimals, "Invalid input.");

        var empty = new PointInfo();
        var existing = State.PointInfos[input.TokenName];
        Assert(existing == null || existing.Equals(empty), "Point token already exists.");
    }
}