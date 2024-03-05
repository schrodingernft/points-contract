using System;
using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty Join(JoinInput input)
    {
        AssertInitialized();
        var dappId = input.DappId;
        AssertDappContractAddress(dappId);

        var registrant = input.Registrant;
        Assert(registrant != null, "Invalid registrant address.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(string.IsNullOrEmpty(State.RegistrationMap[dappId][registrant]),
            "A dapp can only be registered once.");
        State.RegistrationMap[dappId][registrant] = domain;

        // The user registered using an unofficial domain link.

        var relationship = State.DomainsMap[domain];
        Assert(domain == State.DappInfos[dappId].OfficialDomain || relationship != null, "Not exist domain.");
        SettlingPoints(dappId, registrant, nameof(Join));

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            // All points actions will be settled by self-increasing points.
            // SettlingSelfIncreasingPoints(dappId, registrant);

            // The number of user will only be calculated during the registration process
            // TODO 理清domain
            var invitee = relationship.Invitee;
            State.InvitationCount[dappId][invitee] = State.InvitationCount[dappId][invitee].Add(1);
            var inviter = relationship.Inviter;
            if (inviter != null)
            {
                State.TierTwoInvitationCount[dappId][inviter] = State.TierTwoInvitationCount[dappId][inviter].Add(1);
            }
        }

        // SettlingPoints(dappId, registrant, nameof(Join));
        // init first join time
        State.LastPointsUpdateTimes[dappId][registrant][IncomeSourceType.User] = Context.CurrentBlockTime;

        Context.Fire(new Joined
        {
            DappId = dappId,
            Domain = domain,
            Registrant = registrant
        });

        return new Empty();
    }

    public override Empty ApplyToBeAdvocate(ApplyToBeAdvocateInput input)
    {
        var invitee = input.Invitee;
        var inviter = Context.Sender;
        Assert(invitee != null, "Invalid invitee.");

        var dappId = input.DappId;
        Assert(dappId != null && State.DappInfos[dappId] != null, "Invalid dapp id.");
        Assert(State.ApplyDomainCount[inviter]?[dappId] < State.MaxApplyCount.Value, "Apply count exceed the limit.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(State.DomainsMap[domain] == null, "Domain has Exist.");
        Assert(string.IsNullOrEmpty(State.ReservedDomains?.Value?.Domains.FirstOrDefault(t => t == domain)),
            "This domain name is an officially reserved domain name");
        State.DomainsMap[domain] = new DomainRelationshipInfo
        {
            Domain = domain,
            Invitee = invitee,
            Inviter = inviter != invitee ? inviter : null
        };

        const string actionName = "Apply";
        var rule = State.DappInfos[dappId].DappsPointRules.PointsRules
            .FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for apply.");
        var pointName = rule.PointName;
        UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, rule.PointName, rule.KolPoints);

        var pointsDetails = new PointsChanged { PointsChangedDetails = new PointsChangedDetails() };
        pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
            IncomeSourceType.Kol, pointName, rule.KolPoints, dappId));

        if (inviter != invitee)
        {
            UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, rule.PointName, rule.InviterPoints);
            pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                IncomeSourceType.Inviter, pointName, rule.InviterPoints, dappId));
        }

        State.ApplyDomainCount[Context.Sender][input.DappId] =
            State.ApplyDomainCount[Context.Sender][input.DappId].Add(1);
        Context.Fire(new InviterApplied
        {
            Domain = input.Domain,
            DappId = input.DappId,
            Invitee = input.Invitee,
            Inviter = Context.Sender
        });
        return new Empty();
    }

    private void SettlingPoints(Hash dappId, Address user, string actionName)
    {
        var pointsRules = State.DappInfos[dappId].DappsPointRules;
        var rule = pointsRules.PointsRules.FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for this action.");

        var pointName = rule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsDetails = new PointsChanged { PointsChangedDetails = new PointsChangedDetails() };
        UpdatePointsPool(user, domain, IncomeSourceType.User, pointName, rule.UserPoints);
        pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName,
            IncomeSourceType.User, pointName, rule.UserPoints, dappId));

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainsMap[domain];
            var invitee = domainRelationship.Invitee;

            UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, pointName, rule.KolPoints);
            pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
                IncomeSourceType.Kol, pointName, rule.KolPoints, dappId));

            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, pointName, rule.InviterPoints);
                pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                    IncomeSourceType.Inviter, pointName, rule.InviterPoints, dappId));
            }
        }

        var details = SettlingSelfIncreasingPoints(dappId, user, actionName);
        if (details.PointsDetails.Count > 0)
            pointsDetails.PointsChangedDetails.PointsDetails.AddRange(details.PointsDetails);
        // Points details
        Context.Fire(pointsDetails);
    }

    private PointsChangedDetails SettlingSelfIncreasingPoints(Hash dappId, Address user, string actionName)
    {
        var pointsRule = State.SelfIncreasingPointsRules[dappId];
        Assert(pointsRule != null, "This Dapp has not yet set the rules for self-increasing points");
        var pointName = pointsRule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsDetails = new PointsChangedDetails();

        // settle user
        // Only registered users can calculate self-increasing points, and only registered users have settlement time.
        var userLastBillingUpdateTimes = State.LastPointsUpdateTimes[dappId][user][IncomeSourceType.User];
        if (userLastBillingUpdateTimes != null)
        {
            UpdateSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName, pointsRule.UserPoints, domain);
            pointsDetails.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName,
                IncomeSourceType.User, pointName, pointsRule.UserPoints, dappId));
        }

        // settle invitee
        if (domain == State.DappInfos[dappId].OfficialDomain) return pointsDetails;

        var domainRelationship = State.DomainsMap[domain];
        var invitee = domainRelationship.Invitee;
        UpdateSelfIncreasingPoint(dappId, invitee, IncomeSourceType.Kol, pointName, pointsRule.KolPoints, domain);
        pointsDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
            IncomeSourceType.Kol, pointName, pointsRule.KolPoints, dappId));

        // settle inviter
        // kol registered a domain for himself but there was no inviter
        var inviter = domainRelationship.Inviter;
        if (inviter == null) return pointsDetails;

        UpdateSelfIncreasingPoint(dappId, inviter, IncomeSourceType.Inviter, pointName,
            pointsRule.InviterPoints, domain);
        pointsDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
            IncomeSourceType.Inviter, pointName, pointsRule.InviterPoints, dappId));

        return pointsDetails;
    }

    private void UpdateSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type, string pointName,
        long points, string domain)
    {
        var lastBlockTimestamp = State.LastPointsUpdateTimes[dappId][address][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            var waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                Context.CurrentBlockTime.Seconds, lastBlockTime, domain, points);

            UpdatePointsPool(address, domain, type, pointName, waitingSettledPoints);
        }

        State.LastPointsUpdateTimes[dappId][address][type] = Context.CurrentBlockTime;
    }

    private long CalculateWaitingSettledSelfIncreasingPoints(Hash dappId, Address address, IncomeSourceType type,
        long currentBlockTime, long lastBlockTime, string domain, long points)
    {
        var timeGap = currentBlockTime.Sub(lastBlockTime);
        return type switch
        {
            IncomeSourceType.Inviter => points.Mul(timeGap).Mul(State.TierTwoInvitationCount[dappId][address]),
            IncomeSourceType.Kol => points.Mul(timeGap).Mul(State.InvitationCount[dappId][address]),
            IncomeSourceType.User => points.Mul(timeGap),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };
    }

    // PointsBalance
    private void UpdatePointsPool(Address address, string domain, IncomeSourceType type, string pointName, long amount)
    {
        State.PointsBalance[address][domain][type][pointName] =
            State.PointsBalance[address][domain][type][pointName].Add(amount);
    }

    private PointsChangedDetail GeneratePointsDetail(Address address, string domain, string actionName,
        IncomeSourceType type, string pointName, long amount, Hash dappId)
    {
        // pointsChanged
        return new PointsChangedDetail
        {
            DappId = dappId,
            PointsReceiver = address,
            Domain = domain,
            IncomeSourceType = type,
            ActionName = actionName,
            PointsName = pointName,
            IncreaseAmount = amount,
            Balance = State.PointsBalance[address][domain][type][pointName]
        };
    }
}