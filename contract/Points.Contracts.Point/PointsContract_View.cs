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

    public override DomainRelationshipInfo GetDomainApplyInfo(StringValue domain) => State.DomainsMap[domain.Value];

    public override GetPointsBalanceOutput GetPointsBalance(GetPointsBalanceInput input)
    {
        var dappId = input.DappId;
        var address = input.Address;
        var type = input.IncomeSourceType;
        var domain = input.Domain;
        var pointName = input.PointName;
        Assert(address != null && !string.IsNullOrEmpty(domain) &&
               !string.IsNullOrEmpty(pointName) && State.PointInfos[dappId][pointName] != null, "Invalid input.");
        Assert(dappId != null && State.DappInfos[dappId]?.OfficialDomain == input.Domain ||
               State.DomainsMap[domain] != null, "Invalid domain.");

        var balance = State.PointsBalance[address][domain][type][pointName];
        var userLastBillingUpdateTimes = State.LastPointsUpdateTimes[dappId]?[address]?[type];
        long increasingPoints = 0;

        var rule = State.SelfIncreasingPointsRules[dappId];
        if (pointName == rule?.PointName)
        {
            var points = type switch
            {
                IncomeSourceType.User => rule.UserPoints,
                IncomeSourceType.Kol => rule.KolPoints,
                IncomeSourceType.Inviter => rule.InviterPoints,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
            };

            // An account that has not been bound to a domain has no update time.
            if (userLastBillingUpdateTimes != null)
            {
                increasingPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                    Context.CurrentBlockTime.Seconds, userLastBillingUpdateTimes.Seconds, domain, points);
            }
        }

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