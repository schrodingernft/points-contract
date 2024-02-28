using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Address GetAdmin(Empty input) => State.Admin.Value;

    public override GetReservedDomainListOutput GetReservedDomainList(Empty input)
        => new() { ReservedDomainList = State.ReservedDomains.Value };

    public override Int32Value GetMaxRecordListCount(Empty input)
    {
        return new Int32Value { Value = State.MaxRecordListCount.Value };
    }

    public override Int32Value GetMaxApplyCount(Empty input)
    {
        return new Int32Value { Value = State.MaxApplyCount.Value };
    }

    public override DomainOperatorRelationship GetDomainApplyInfo(StringValue domain)
    {
        return State.DomainOperatorRelationshipMap[domain.Value];
    }
}