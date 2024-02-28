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
        Assert(input.MaxRecordListCount > 0, "Invalid MaxRecordListCount.");
        Assert(input.MaxApplyCount > 0, "Invalid MaxApplyCount.");

        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");

        Assert(input.Admin == null || !input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");
        State.MaxRecordListCount.Value = input.MaxRecordListCount;
        State.MaxApplyCount.Value = input.MaxApplyCount;
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

    public override Empty SetMaxRecordListCount(Int32Value input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input is { Value: > 0 }, "Invalid input.");

        State.MaxRecordListCount.Value = input.Value;
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

    public override Empty ApplyToOperator(ApplyToOperatorInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.Domain != null, "Invalid domain.");
        Assert(input.Service != null, "Invalid service name.");
        Assert(input.Invitee != null, "Invalid invitee.");
        var registrationInfo = State.ServicesEarningRulesMap[input.Service];
        Assert(registrationInfo != null, "Service not found.");
        var domain = State.DomainOperatorRelationshipMap[input.Domain];
        Assert(domain == null, "Domain has Exist.");
        var applyCount = State.ApplyCount[Context.Sender][input.Service];
        Assert(applyCount < State.MaxApplyCount.Value, "Apply count exceed the limit.");
        State.DomainOperatorRelationshipMap[input.Domain] = new DomainOperatorRelationship
        {
            Domain = input.Domain,
            Invitee = input.Invitee,
            Inviter = Context.Sender
        };
        State.ApplyCount[Context.Sender][input.Service] += 1;
        Context.Fire(new InviterApplyed
        {
            Domain = input.Domain,
            Service = input.Service,
            Invitee = input.Invitee,
            Inviter = Context.Sender
        });
        return new Empty();
    }

    public override Empty PointsSettlement(PointsSettlementInput input)
    {
        AssertAdmin();
        Assert(input != null, "Invalid input.");
        Assert(input.PointsRecords != null && input.PointsRecords.Count <= State.MaxRecordListCount.Value,
            "Invalid PointsRecords.");
        foreach (var pointRecord in input.PointsRecords)
        {
            var pointInfo = State.PointsInfos[pointRecord.PointsName];
            Assert(pointInfo != null, $"invalid PointsName:{pointRecord.PointsName}");
            State.PointsPool[pointRecord.DappName][pointRecord.PointerAddress][pointRecord.PointsName] += pointRecord.Amout;
        }

        Context.Fire(new PointsRecorded
        {
            PointsRecordList = new PointsRecordList()
            {
                PointsRecords = { input.PointsRecords }
            }
        });
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