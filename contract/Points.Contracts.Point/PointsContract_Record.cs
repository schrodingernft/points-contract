using System;
using System.Linq;
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
        AssertDappAdmin(dappId);

        var registrant = input.Registrant;
        Assert(registrant != null, "Invalid registrant address.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(string.IsNullOrEmpty(State.RegistrationMap[dappId][registrant]),
            "A dapp can only be registered once.");
        State.RegistrationMap[dappId][registrant] = domain;

        // The user registered using an unofficial domain link.
        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var relationship = State.DomainOperatorRelationshipMap[domain];
            Assert(relationship != null, "Not exist domain.");
            // All points actions will be settled by self-increasing points.
            SettlingSelfIncreasingPoints(dappId, registrant);

            // The number of user will only be calculated during the registration process
            State.Relationships[dappId][relationship.Invitee][domain] += 1;
            if (relationship.Inviter != null) State.InviterRelationships[dappId][relationship.Inviter] += 1;
        }

        SettlingPoints(dappId, registrant, "join");
        // init first join time
        State.LastBillingUpdateTimes[dappId][registrant][IncomeSourceType.User] = Context.CurrentBlockTime;

        Context.Fire(new Joined
        {
            DappId = dappId,
            Domain = domain,
            Registrant = registrant
        });

        return new Empty();
    }

    // public override Empty Settle(SettleInput input)
    // {
    //     AssertInitialized();
    //     var dappId = input.DappId;
    //     AssertDappAdmin(dappId);
    //
    //     var userAddress = input.UserAddress;
    //     Assert(userAddress.Value != null, "User address cannot be null");
    //     Assert(!string.IsNullOrEmpty(State.RegistrationMap[dappId][userAddress]), "User has not registered yet");
    //
    //     SettlingInviterReferrerPoints(dappId, userAddress);
    //     SettlingPoints(dappId, userAddress, input.ActionName);
    //
    //     return new Empty();
    // }

    public override Empty ApplyToOperator(ApplyToOperatorInput input)
    {
        var dappId = input.DappId;
        Assert(dappId != null && State.DappInfos[dappId] != null, "Invalid dapp id.");

        var invitee = input.Invitee;
        var inviter = Context.Sender;
        Assert(invitee != null, "Invalid invitee.");
        Assert(State.ApplyCount[inviter]?[dappId] < State.MaxApplyCount.Value, "Apply count exceed the limit.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(State.DomainOperatorRelationshipMap[domain] == null, "Domain has Exist.");
        Assert(string.IsNullOrEmpty(State.ReservedDomains?.Value?.Domains.FirstOrDefault(t => t == domain)),
            "This domain name is an officially reserved domain name");
        State.DomainOperatorRelationshipMap[domain] = new DomainOperatorRelationship
        {
            Domain = domain,
            Invitee = invitee,
            Inviter = inviter != invitee ? inviter : null
        };

        var rule = State.DappInfos[dappId].DappsEarningRules.EarningRules
            .FirstOrDefault(t => t.ActionName == "apply");
        Assert(rule != null, "There is no corresponding points rule set for apply.");
        var pointName = rule.PointName;
        UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, rule.PointName, rule.KolPoints);

        var pointsDetails = new PointsDetails { PointDetailList = new PointsDetailList() };
        pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(invitee, domain, "apply",
            IncomeSourceType.Kol, pointName, rule.KolPoints, dappId));

        if (inviter != input.Invitee)
        {
            UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, rule.PointName, rule.InviterPoints);
            pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(inviter, domain, "apply",
                IncomeSourceType.Inviter, pointName, rule.InviterPoints, dappId));
        }

        State.ApplyCount[Context.Sender][input.DappId] += 1;
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
        // Calculate instantaneous integral action.
        var pointsRules = State.DappInfos[dappId].DappsEarningRules;
        var rule = pointsRules.EarningRules.FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for this action.");

        var pointName = rule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsDetails = new PointsDetails { PointDetailList = new PointsDetailList() };
        UpdatePointsPool(user, domain, IncomeSourceType.User, pointName, rule.UserPoints);
        pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName,
            IncomeSourceType.User, pointName, rule.UserPoints, dappId));

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainOperatorRelationshipMap[domain];
            var invitee = domainRelationship.Invitee;

            UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, pointName, rule.KolPoints);
            pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
                IncomeSourceType.Kol, pointName, rule.KolPoints, dappId));

            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, pointName, rule.InviterPoints);
                pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                    IncomeSourceType.Inviter, pointName, rule.InviterPoints, dappId));
            }
        }

        // Points details
        Context.Fire(pointsDetails);
    }

    private void SettlingSelfIncreasingPoints(Hash dappId, Address user)
    {
        // TODO: add exist check.
        var pointsRule = State.SelfIncreasingPointsRules[dappId];
        var pointName = pointsRule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsUpdated = new PointsUpdated { PointStateList = new() };

        // settle user
        // Only registered users can calculate self-increasing points, and only registered users have settlement time.
        var userLastBillingUpdateTimes = State.LastBillingUpdateTimes[dappId][user][IncomeSourceType.User];
        if (userLastBillingUpdateTimes != null)
        {
            UpdateSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName, pointsRule.UserPoints, domain);
            pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(user, domain,
                IncomeSourceType.User, pointName));
        }

        // settle invitee
        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainOperatorRelationshipMap[domain];
            var invitee = domainRelationship.Invitee;
            UpdateSelfIncreasingPoint(dappId, invitee, IncomeSourceType.Kol, pointName, pointsRule.KolPoints, domain);
            pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(invitee, domain,
                IncomeSourceType.Kol, pointName));

            // settle inviter
            // kol registered a domain for himself but there was no inviter
            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                UpdateSelfIncreasingPoint(dappId, inviter, IncomeSourceType.Inviter, pointName,
                    pointsRule.InviterPoints, domain);
                pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(inviter, domain,
                    IncomeSourceType.Inviter, pointName));
            }
        }

        if (pointsUpdated.PointStateList.PointStates.Count != 0) Context.Fire(pointsUpdated);
    }

    private void UpdateSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type, string pointName,
        long points, string domain)
    {
        var lastBlockTimestamp = State.LastBillingUpdateTimes[dappId][address][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            var waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                Context.CurrentBlockTime.Seconds, lastBlockTime, domain, points);

            UpdatePointsPool(address, domain, type, pointName, waitingSettledPoints);
        }

        State.LastBillingUpdateTimes[dappId][address][type] = Context.CurrentBlockTime;
    }

    private long CalculateWaitingSettledSelfIncreasingPoints(Hash dappId, Address address, IncomeSourceType type,
        long currentBlockTime, long lastBlockTime, string domain, long points)
    {
        return type switch
        {
            IncomeSourceType.Inviter => State.InviterRelationships[dappId][address] * points *
                                        (currentBlockTime - lastBlockTime),
            IncomeSourceType.Kol => State.Relationships[dappId][address][domain] * points *
                                    (currentBlockTime - lastBlockTime),
            IncomeSourceType.User => points * (currentBlockTime - lastBlockTime),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
        };
    }

    private void UpdatePointsPool(Address address, string domain, IncomeSourceType type, string pointName, long amount)
        => State.PointsPool[address][domain][type][pointName] += amount;

    private PointsState GeneratePointsState(Address address, string domain, IncomeSourceType type, string pointName)
    {
        return new PointsState
        {
            Address = address,
            Domain = domain,
            IncomeSourceType = type,
            PointName = pointName,
            Balance = State.PointsPool[address][domain][type][pointName]
        };
    }

    private PointsDetail GeneratePointsDetail(Address address, string domain, string actionName, IncomeSourceType type,
        string pointName, long amount, Hash dappId)
    {
        return new PointsDetail
        {
            DappId = dappId,
            PointerAddress = address,
            Domain = domain,
            IncomeSourceType = type,
            ActionName = actionName,
            PointsName = pointName,
            IncreaseAmount = amount,
            Balance = State.PointsPool[address][domain][type][pointName]
        };
    }
}