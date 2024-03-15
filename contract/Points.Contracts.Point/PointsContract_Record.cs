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
            // The number of user will only be calculated during the registration process
            var invitee = relationship.Invitee;
            State.InvitationCount[dappId][invitee][domain] = State.InvitationCount[dappId][invitee][domain].Add(1);
            var inviter = relationship.Inviter;
            if (inviter != null)
            {
                State.TierTwoInvitationCount[dappId][inviter][domain] =
                    State.TierTwoInvitationCount[dappId][inviter][domain].Add(1);
            }
        }

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

        const string actionName = nameof(ApplyToBeAdvocate);
        var rule = State.DappInfos[dappId].DappsPointRules.PointsRules
            .FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for apply.");
        var pointName = rule.PointName;
        var kolPoints = rule.UserPoints.Mul(rule.KolPointsPercent).Div(PointsContractConstants.Denominator);
        UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, rule.PointName, kolPoints);

        var pointsDetails = new PointsChanged { PointsChangedDetails = new PointsChangedDetails() };
        pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
            IncomeSourceType.Kol, pointName, kolPoints, dappId));

        if (inviter != invitee)
        {
            var inviterPoints = rule.UserPoints.Mul(rule.InviterPointsPercent).Div(PointsContractConstants.Denominator);
            UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, rule.PointName, inviterPoints);
            pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                IncomeSourceType.Inviter, pointName, inviterPoints, dappId));
        }

        State.ApplyDomainCount[Context.Sender][input.DappId] =
            State.ApplyDomainCount[Context.Sender][input.DappId].Add(1);
        Context.Fire(pointsDetails);
        Context.Fire(new InviterApplied
        {
            Domain = input.Domain,
            DappId = input.DappId,
            Invitee = input.Invitee,
            Inviter = Context.Sender
        });
        return new Empty();
    }

    private void SettlingPoints(Hash dappId, Address user, string actionName, long sourceUserPoints = 0)
    {
        var pointsRules = State.DappInfos[dappId].DappsPointRules;
        var rule = pointsRules.PointsRules.FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for this action.");

        var pointName = rule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsDetails = new PointsChanged { PointsChangedDetails = new PointsChangedDetails() };

        var userPoints = GetPoints(rule, sourceUserPoints, out var kolPoints, out var inviterPoints);
        UpdatePointsBalance(user, domain, IncomeSourceType.User, pointName, userPoints);

        pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName,
            IncomeSourceType.User, pointName, userPoints, dappId));
        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainsMap[domain];
            var invitee = domainRelationship.Invitee;

            UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, pointName, kolPoints);
            pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
                IncomeSourceType.Kol, pointName, kolPoints, dappId));

            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, pointName, inviterPoints);
                pointsDetails.PointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                    IncomeSourceType.Inviter, pointName, inviterPoints, dappId));
            }
        }

        // All points actions will be settled by self-increasing points.
        var details = SettlingSelfIncreasingPoints(dappId, user);
        if (details.PointsDetails.Count > 0)
            pointsDetails.PointsChangedDetails.PointsDetails.AddRange(details.PointsDetails);
        // Points details
        Context.Fire(pointsDetails);
    }

    private long GetPoints(PointsRule rule, long sourceUserPoints, out long kolPoints,
        out long inviterPoints)
    {
        var userPoints = rule.UserPoints == 0 ? sourceUserPoints : rule.UserPoints;
        kolPoints = userPoints.Mul(rule.KolPointsPercent).Div(PointsContractConstants.Denominator);
        inviterPoints = userPoints.Mul(rule.InviterPointsPercent).Div(PointsContractConstants.Denominator);
        return userPoints;
    }

    private PointsChangedDetails SettlingSelfIncreasingPoints(Hash dappId, Address user)
    {
        var pointsRule = State.SelfIncreasingPointsRules[dappId];
        Assert(pointsRule != null, "This Dapp has not yet set the rules for self-increasing points");
        var pointName = pointsRule.PointName;
        var actionName = pointsRule.ActionName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsDetails = new PointsChangedDetails();

        // settle user
        // Only registered users can calculate self-increasing points, and only registered users have settlement time.
        var userIncreasingPoint = UpdateSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName,
            pointsRule.UserPoints, domain);
        pointsDetails.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName, IncomeSourceType.User, pointName,
            userIncreasingPoint, dappId));

        // settle invitee
        if (domain == State.DappInfos[dappId].OfficialDomain) return pointsDetails;

        var domainRelationship = State.DomainsMap[domain];
        var invitee = domainRelationship.Invitee;
        var kolPoints = pointsRule.UserPoints.Mul(pointsRule.KolPointsPercent).Div(PointsContractConstants.Denominator);
        var kolIncreasingPoint = UpdateSelfIncreasingPoint(dappId, invitee, IncomeSourceType.Kol, pointName,
            kolPoints, domain);
        pointsDetails.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
            IncomeSourceType.Kol, pointName, kolIncreasingPoint, dappId));

        // settle inviter
        // kol registered a domain for himself but there was no inviter
        var inviter = domainRelationship.Inviter;
        if (inviter == null) return pointsDetails;

        var inviterPoints = pointsRule.UserPoints.Mul(pointsRule.InviterPointsPercent).Div(PointsContractConstants.Denominator);
        var inviterIncreasingPoint = UpdateSelfIncreasingPoint(dappId, inviter, IncomeSourceType.Inviter, pointName,
            inviterPoints, domain);
        pointsDetails.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
            IncomeSourceType.Inviter, pointName, inviterIncreasingPoint, dappId));

        return pointsDetails;
    }

    private long UpdateSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type, string pointName,
        long points, string domain)
    {
        var lastBlockTimestamp = State.LastPointsUpdateTimes[dappId][address][domain][type];
        long waitingSettledPoints = 0;
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                Context.CurrentBlockTime.Seconds, lastBlockTime, domain, points);

            UpdatePointsBalance(address, domain, type, pointName, waitingSettledPoints);
        }

        State.LastPointsUpdateTimes[dappId][address][domain][type] = Context.CurrentBlockTime;
        return waitingSettledPoints;
    }

    private long CalculateWaitingSettledSelfIncreasingPoints(Hash dappId, Address address, IncomeSourceType type,
        long currentBlockTime, long lastBlockTime, string domain, long points)
    {
        var timeGap = currentBlockTime.Sub(lastBlockTime);
        return type switch
        {
            IncomeSourceType.Inviter => points.Mul(timeGap).Mul(State.TierTwoInvitationCount[dappId][address][domain]),
            IncomeSourceType.Kol => points.Mul(timeGap).Mul(State.InvitationCount[dappId][address][domain]),
            IncomeSourceType.User => points.Mul(timeGap),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };
    }

    private void UpdatePointsBalance(Address address, string domain, IncomeSourceType type, string pointName,
        long amount)
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

    public override Empty Settle(SettleInput input)
    {
        CheckSettleParam(input.DappId, input.ActionName);
        var dappId = input.DappId;
        var userAddress = input.UserAddress;
        Assert(userAddress.Value != null, "User address cannot be null");
        Assert(!string.IsNullOrEmpty(State.RegistrationMap[dappId][userAddress]), "User has not registered yet");
        SettlingPoints(dappId, userAddress, input.ActionName, input.UserPoints);

        return new Empty();
    }

    public override Empty BatchSettle(BatchSettleInput input)
    {
        CheckSettleParam(input.DappId, input.ActionName);
        var dappId = input.DappId;
        Assert(
            input.UserPointsList.Count > 0 &&
            input.UserPointsList.Count <= PointsContractConstants.MaxBatchSettleListCount, "Invalid user point list.");
        foreach (var userPoints in input.UserPointsList)
        {
            Assert(userPoints.UserAddress.Value != null, "User address cannot be null");
            Assert(!string.IsNullOrEmpty(State.RegistrationMap[dappId][userPoints.UserAddress]),
                "User has not registered yet");
            SettlingPoints(dappId, userPoints.UserAddress, input.ActionName, userPoints.UserPoints_);
        }

        return new Empty();
    }

    private void CheckSettleParam(Hash dappId, string actionName)
    {
        AssertInitialized();
        AssertDappContractAddress(dappId);
        Assert(IsStringValid(actionName), "Invalid action name.");
    }
}