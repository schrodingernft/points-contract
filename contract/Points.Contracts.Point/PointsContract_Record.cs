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
            "Users cannot bind two domains on the same Dapp.");
        State.RegistrationMap[dappId][registrant] = domain;
        
        // The user registered using an unofficial domain link.
        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var relationship = State.DomainOperatorRelationshipMap[domain];
            Assert(relationship != null, "Not exist domain.");

            // The number of user will only be calculated during the registration process
            State.Relationships[dappId][relationship.Invitee][domain] += 1;
            if (relationship.Inviter != null) State.InviterRelationships[dappId][relationship.Inviter] += 1;
        }

        SettlingPoints(dappId, input.Registrant, "join");
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
        Assert(input.DappId != null && State.DappInfos[input.DappId] != null, "Invalid dapp id.");

        var invitee = input.Invitee;
        var inviter = Context.Sender;
        Assert(invitee != null, "Invalid invitee.");
        Assert(State.ApplyCount[inviter]?[input.DappId] < State.MaxApplyCount.Value, "Apply count exceed the limit.");

        var domain = input.Domain;
        AssertDomainFormat(domain);
        Assert(State.DomainOperatorRelationshipMap[domain] == null, "Domain has Exist.");

        State.DomainOperatorRelationshipMap[domain] = new DomainOperatorRelationship
        {
            Domain = domain,
            Invitee = invitee,
            Inviter = inviter != invitee ? inviter : null
        };

        var rule = State.DappInfos[input.DappId].DappsEarningRules.EarningRules
            .FirstOrDefault(t => t.ActionName == "apply");
        Assert(rule != null, "There is no corresponding points rule set for apply.");
        var pointName = rule.PointName;
        UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, rule.PointName, rule.KolPoints);

        var pointsUpdated = new PointsUpdated { PointStateList = new PointsStateList() };
        pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(invitee, domain,
            IncomeSourceType.Kol, pointName));

        if (inviter != input.Invitee)
        {
            UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, rule.PointName, rule.InviterPoints);
            pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(inviter, domain,
                IncomeSourceType.Inviter, pointName));
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
        // All points actions will be settled by self-increasing points.
        SettlingSelfIncreasingPoints(dappId, user);

        // Calculate instantaneous integral action.
        var pointsRules = State.DappInfos[dappId].DappsEarningRules;
        var rule = pointsRules.EarningRules.FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for this action.");

        var pointName = rule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsUpdated = new PointsUpdated { PointStateList = new PointsStateList() };
        var pointsDetails = new PointsDetails { PointDetailList = new PointsDetailList() };
        UpdatePointsPool(user, domain, IncomeSourceType.User, pointName, rule.UserPoints);
        pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(user, domain,
            IncomeSourceType.User, pointName));
        pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(user, domain, actionName,
            IncomeSourceType.User, pointName, rule.UserPoints, dappId));

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainOperatorRelationshipMap[domain];
            var invitee = domainRelationship.Invitee;

            UpdatePointsPool(invitee, domain, IncomeSourceType.Kol, pointName, rule.KolPoints);
            pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(invitee, domain,
                IncomeSourceType.Kol, pointName));
            pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(invitee, domain, actionName,
                IncomeSourceType.Kol, pointName, rule.KolPoints, dappId));

            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                UpdatePointsPool(inviter, domain, IncomeSourceType.Inviter, pointName, rule.InviterPoints);
                pointsUpdated.PointStateList.PointStates.Add(GeneratePointsState(inviter, domain,
                    IncomeSourceType.Inviter, pointName));
                pointsDetails.PointDetailList.PointsDetails.Add(GeneratePointsDetail(inviter, domain, actionName,
                    IncomeSourceType.Inviter, pointName, rule.InviterPoints, dappId));
            }
        }

        // Account total
        Context.Fire(pointsUpdated);
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
            var waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type, lastBlockTime,
                Context.CurrentBlockTime.Seconds, domain, points);

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
            PointerAddress = address,
            Domain = domain,
            ActionName = actionName,
            IncomeSourceType = type,
            PointsName = pointName,
            Amount = amount,
            DappId = dappId
        };
    }
}