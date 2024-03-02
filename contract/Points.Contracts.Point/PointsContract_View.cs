using System;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Address GetAdmin(Empty input) => State.Admin.Value;
    public override Int32Value GetMaxApplyCount(Empty input) => new() { Value = State.MaxApplyCount.Value };

    public override GetReservedDomainListOutput GetReservedDomainList(Empty input)
        => new() { ReservedDomainList = State.ReservedDomains.Value };

    public override DomainOperatorRelationship GetDomainApplyInfo(StringValue domain)
        => State.DomainOperatorRelationshipMap[domain.Value];

    public override GetPointsBalanceOutput GetPointsBalance(GetPointsBalanceInput input)
    {
        var dappId = input.DappId;
        var address = input.Address;
        var type = input.IncomeSourceType;
        var domain = input.Domain;
        var pointName = input.PointName;
        Assert(address != null && !string.IsNullOrEmpty(domain) &&
               !string.IsNullOrEmpty(pointName) && State.PointInfos[pointName] != null, "Invalid input.");
        Assert(dappId != null && State.DappInfos[dappId]?.OfficialDomain == input.Domain ||
               State.DomainOperatorRelationshipMap[domain] != null, "Invalid domain.");

        var currentPoints = GeneratePointsState(address, domain, type, pointName);
        var balance = currentPoints.Balance;
        var rule = State.SelfIncreasingPointsRules[dappId];
        var points = type switch
        {
            IncomeSourceType.User => rule.UserPoints,
            IncomeSourceType.Kol => rule.KolPoints,
            IncomeSourceType.Inviter => rule.InviterPoints,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };

        var userLastBillingUpdateTimes = State.LastBillingUpdateTimes[dappId]?[address]?[type];
        var increasingPoints = userLastBillingUpdateTimes != null
            ? CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type, Context.CurrentBlockTime.Seconds,
                userLastBillingUpdateTimes.Seconds, domain, points)
            : 0;

        return new GetPointsBalanceOutput
        {
            PointName = input.PointName,
            Owner = input.Address,
            Balance = increasingPoints + balance,
            LastUpdateTime = userLastBillingUpdateTimes
        };
    }

    public override GetDappInformationOutput GetDappInformation(GetDappInformationInput input)
        => new() { DappInfo = State.DappInfos[input.DappId] };

    public override GetSelfIncreasingPointsRuleOutput GetSelfIncreasingPointsRule(
        GetSelfIncreasingPointsRuleInput input) => new() { Rule = State.SelfIncreasingPointsRules[input.DappId] };
}