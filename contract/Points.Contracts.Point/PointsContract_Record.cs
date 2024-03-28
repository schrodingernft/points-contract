using System;
using System.Linq;
using AElf;
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

        // The user registered using an unofficial domain link.

        var relationship = State.DomainsMap[domain];
        Assert(domain == State.DappInfos[dappId].OfficialDomain || relationship != null, "Not exist domain.");

        Register(dappId, registrant, domain, nameof(Join));

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
        Assert(input.Inviter == null || !input.Inviter.Value.IsNullOrEmpty(), "Invalid inviter.");

        var invitee = input.Invitee;
        var inviter = input.Inviter ?? Context.Sender;
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
        var kolPoints = GetKolPoints(rule);

        var pointsChangeDetails = new PointsChangedDetails();
        pointsChangeDetails = UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, pointName, kolPoints, dappId,
            actionName, pointsChangeDetails);

        if (inviter != invitee)
        {
            var inviterPoints = GetInviterPoints(rule);
            pointsChangeDetails = UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, rule.PointName,
                inviterPoints, dappId, actionName, pointsChangeDetails);
        }

        State.ApplyDomainCount[inviter][input.DappId] = State.ApplyDomainCount[inviter][input.DappId].Add(1);
        Context.Fire(new PointsChanged { PointsChangedDetails = pointsChangeDetails });
        Context.Fire(new InviterApplied
        {
            Domain = input.Domain,
            DappId = input.DappId,
            Invitee = input.Invitee,
            Inviter = inviter
        });
        return new Empty();
    }

    private void SettlingPoints(Hash dappId, Address user, string actionName, BigIntValue sourceUserPoints = null)
    {
        var pointsRules = State.DappInfos[dappId].DappsPointRules;
        var rule = pointsRules.PointsRules.FirstOrDefault(t => t.ActionName == actionName);
        Assert(rule != null, "There is no corresponding points rule set for this action.");

        var pointName = rule.PointName;
        var domain = State.RegistrationMap[dappId][user];
        var pointsChangeDetails = new PointsChangedDetails();

        var userPoints = GetPoints(rule, sourceUserPoints, out var kolPoints, out var inviterPoints);
        pointsChangeDetails = UpdatePointsBalance(user, domain, IncomeSourceType.User, pointName, userPoints, dappId,
            actionName, pointsChangeDetails);

        if (domain != State.DappInfos[dappId].OfficialDomain)
        {
            var domainRelationship = State.DomainsMap[domain];
            var invitee = domainRelationship.Invitee;

            pointsChangeDetails = UpdatePointsBalance(invitee, domain, IncomeSourceType.Kol, pointName, kolPoints,
                dappId, actionName, pointsChangeDetails);

            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                pointsChangeDetails = UpdatePointsBalance(inviter, domain, IncomeSourceType.Inviter, pointName,
                    inviterPoints, dappId, actionName, pointsChangeDetails);
            }
        }
        else
        {
            var relationInfo = State.ReferralRelationInfoMap[dappId][user];
            if (relationInfo != null)
            {
                var referrerDomain = State.RegistrationMap[dappId][relationInfo.Referrer];
                pointsChangeDetails = UpdatePointsBalance(relationInfo.Referrer, referrerDomain, IncomeSourceType.User,
                    pointName, kolPoints, dappId, actionName, pointsChangeDetails);

                if (relationInfo.Inviter != null)
                {
                    pointsChangeDetails = UpdatePointsBalance(relationInfo.Inviter, domain, IncomeSourceType.User,
                        pointName, inviterPoints, dappId, actionName, pointsChangeDetails);
                }

                if (referrerDomain != domain)
                {
                    var domainRelationship = State.DomainsMap[referrerDomain];
                    var invitee = domainRelationship.Invitee;
                    var points = kolPoints.Mul(rule.KolPointsPercent).Div(PointsContractConstants.Denominator);

                    pointsChangeDetails = UpdatePointsBalance(invitee, referrerDomain, IncomeSourceType.Kol, pointName,
                        points, dappId, actionName, pointsChangeDetails);
                }
            }
        }

        // All points actions will be settled by self-increasing points.
        var details = SettlingSelfIncreasingPoints(dappId, user);
        if (details.PointsDetails.Count > 0)
            pointsChangeDetails.PointsDetails.AddRange(details.PointsDetails);
        // Points details
        Context.Fire(new PointsChanged { PointsChangedDetails = pointsChangeDetails });
    }

    private BigIntValue GetPoints(PointsRule rule, BigIntValue sourceUserPoints, out BigIntValue kolPoints,
        out BigIntValue inviterPoints)
    {
        sourceUserPoints ??= new BigIntValue(rule.UserPoints);

        var userPoints = rule.EnableProportionalCalculation ? sourceUserPoints : rule.UserPoints;
        kolPoints = GetKolPoints(rule, sourceUserPoints);
        inviterPoints = GetInviterPoints(rule, sourceUserPoints);
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
        pointsDetails = UpdateSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName,
            pointsRule.UserPoints, domain, actionName, pointsDetails);

        var domainRelationship = State.DomainsMap[domain];
        if (domainRelationship != null)
        {
            // settle kol
            var invitee = domainRelationship.Invitee;
            var kolPoints = GetKolPoints(pointsRule);
            pointsDetails = UpdateSelfIncreasingPoint(dappId, invitee, IncomeSourceType.Kol, pointName,
                kolPoints, domain, actionName, pointsDetails);

            // settle inviter
            // kol registered a domain for himself but there was no inviter
            var inviter = domainRelationship.Inviter;
            if (inviter != null)
            {
                var inviterPoints = GetInviterPoints(pointsRule);
                pointsDetails = UpdateSelfIncreasingPoint(dappId, inviter, IncomeSourceType.Inviter, pointName,
                    inviterPoints, domain, actionName, pointsDetails);
            }
        }

        // settle user referral points
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, user, IncomeSourceType.User, pointName,
            pointsRule, domain, actionName, pointsDetails);

        // settle referrer referral points
        var relationInfo = State.ReferralRelationInfoMap[dappId][user];
        if (relationInfo == null) return pointsDetails;

        var referrerDomain = State.RegistrationMap[dappId][relationInfo.Referrer];
        pointsDetails = UpdateSelfIncreasingPoint(dappId, relationInfo.Referrer, IncomeSourceType.User, pointName,
            pointsRule.UserPoints, referrerDomain, actionName, pointsDetails);
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, relationInfo.Referrer, IncomeSourceType.User,
            pointName, pointsRule, referrerDomain, actionName, pointsDetails);

        // settle inviter referral points
        if (relationInfo.Inviter != null)
        {
            pointsDetails = UpdateSelfIncreasingPoint(dappId, relationInfo.Inviter, IncomeSourceType.User, pointName,
                pointsRule.UserPoints, domain, actionName, pointsDetails);
            pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, relationInfo.Inviter,
                IncomeSourceType.User, pointName, pointsRule, domain, actionName, pointsDetails);
        }

        // settle kol referral points
        var referrerDomainRelationship = State.DomainsMap[referrerDomain];

        if (referrerDomainRelationship == null) return pointsDetails;

        var kol = referrerDomainRelationship.Invitee;
        if (IsStringValid(State.RegistrationMap[dappId][kol]))
        {
            pointsDetails = UpdateSelfIncreasingPoint(dappId, kol, IncomeSourceType.Kol, pointName, pointsRule.UserPoints,
                referrerDomain, actionName, pointsDetails);
        }
        
        pointsDetails = UpdateSelfIncreasingPoint(dappId, kol, IncomeSourceType.Kol, pointName,
            GetKolPoints(pointsRule), referrerDomain, actionName, pointsDetails);
        pointsDetails = UpdateReferralSelfIncreasingPoint(dappId, kol, IncomeSourceType.Kol, pointName,
            pointsRule, referrerDomain, actionName, pointsDetails);

        return pointsDetails;
    }

    private BigIntValue GetKolPoints(PointsRule pointsRule, BigIntValue sourceUserPoints = null)
    {
        var userPoints = sourceUserPoints ?? pointsRule.UserPoints;
        return pointsRule.EnableProportionalCalculation
            ? userPoints.Mul(pointsRule.KolPointsPercent).Div(PointsContractConstants.Denominator)
            : new BigIntValue(pointsRule.KolPointsPercent);
    }

    private BigIntValue GetInviterPoints(PointsRule pointsRule, BigIntValue sourceUserPoints = null)
    {
        var userPoints = sourceUserPoints ?? pointsRule.UserPoints;
        return pointsRule.EnableProportionalCalculation
            ? userPoints.Mul(pointsRule.InviterPointsPercent).Div(PointsContractConstants.Denominator)
            : new BigIntValue(pointsRule.InviterPointsPercent);
    }

    private PointsChangedDetails UpdateSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type,
        string pointName, BigIntValue points, string domain, string actionName,
        PointsChangedDetails pointsChangedDetails)
    {
        var lastBlockTimestamp = State.LastPointsUpdateTimes[dappId][address][domain][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            var waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPoints(dappId, address, type,
                Context.CurrentBlockTime.Seconds, lastBlockTime, domain, points);

            pointsChangedDetails = UpdatePointsBalance(address, domain, type, pointName, waitingSettledPoints, dappId,
                actionName, pointsChangedDetails);
        }

        State.LastPointsUpdateTimes[dappId][address][domain][type] = Context.CurrentBlockTime;

        return pointsChangedDetails;
    }

    private BigIntValue CalculateWaitingSettledSelfIncreasingPoints(Hash dappId, Address address, IncomeSourceType type,
        long currentBlockTime, long lastBlockTime, string domain, BigIntValue points)
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

    private PointsChangedDetails UpdatePointsBalance(Address address, string domain, IncomeSourceType type,
        string pointName, BigIntValue amount, Hash dappId, string actionName, PointsChangedDetails pointsChangedDetails)
    {
        var balance = State.PointsBalance[address][domain][type][pointName];
        var pointsBalance = State.PointsBalanceValue[address][domain][type][pointName] ?? new BigIntValue(balance);
        pointsBalance = pointsBalance.Add(amount);
        State.PointsBalanceValue[address][domain][type][pointName] = pointsBalance;

        pointsChangedDetails.PointsDetails.Add(GeneratePointsDetail(address, domain, actionName, type, pointName,
            amount, dappId));

        return pointsChangedDetails;
    }

    private PointsChangedDetail GeneratePointsDetail(Address address, string domain, string actionName,
        IncomeSourceType type, string pointName, BigIntValue amount, Hash dappId)
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
            IncreaseValue = amount,
            BalanceValue = State.PointsBalanceValue[address][domain][type][pointName]
        };
    }

    public override Empty Settle(SettleInput input)
    {
        CheckSettleParam(input.DappId, input.ActionName);
        var dappId = input.DappId;
        var userAddress = input.UserAddress;
        CheckAndSettlePoints(dappId, userAddress, input.UserPointsValue, input.UserPoints, input.ActionName);
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
            CheckAndSettlePoints(dappId, userPoints.UserAddress, userPoints.UserPointsValue, userPoints.UserPoints_,
                input.ActionName);
        }

        return new Empty();
    }

    private void CheckAndSettlePoints(Hash dappId, Address userAddress, BigIntValue userPointsValue, long userPoints,
        string actionName)
    {
        Assert(userAddress.Value != null, "User address cannot be null");
        Assert(!string.IsNullOrEmpty(State.RegistrationMap[dappId][userAddress]),
            "User has not registered yet");
        var userPoint = userPointsValue ?? new BigIntValue(userPoints);
        SettlingPoints(dappId, userAddress, actionName, userPoint);
    }

    private void CheckSettleParam(Hash dappId, string actionName)
    {
        AssertInitialized();
        AssertDappContractAddress(dappId);
        Assert(IsStringValid(actionName), "Invalid action name.");
    }

    private void Register(Hash dappId, Address sender, string domain, string actionName)
    {
        Assert(string.IsNullOrEmpty(State.RegistrationMap[dappId][sender]), "A dapp can only be registered once.");
        State.RegistrationMap[dappId][sender] = domain;

        SettlingPoints(dappId, sender, actionName);
    }

    public override Empty AcceptReferral(AcceptReferralInput input)
    {
        Assert(input != null, "Invalid input.");

        var dappId = input.DappId;
        AssertDappContractAddress(dappId);

        var officialDomain = State.DappInfos[dappId].OfficialDomain;

        var referrer = input.Referrer;
        Assert(IsAddressValid(referrer), "Invalid referrer.");
        var referrerDomain = State.RegistrationMap[dappId][referrer];
        Assert(IsStringValid(referrerDomain), "Referrer not joined.");

        var invitee = input.Invitee;
        Assert(IsAddressValid(invitee), "Invalid invitee.");

        var inviter = State.ReferralRelationInfoMap[dappId][referrer]?.Referrer;
        State.ReferralRelationInfoMap[dappId][invitee] = new ReferralRelationInfo
        {
            DappId = dappId,
            Invitee = invitee,
            Referrer = referrer,
            Inviter = inviter
        };

        State.ReferralFollowerCountMap[dappId][invitee] = new ReferralFollowerCount();

        Register(dappId, invitee, officialDomain, nameof(AcceptReferral));

        SetFollowerCount(dappId, referrer, 1, 0);
        SetFollowerCount(dappId, inviter, 0, 1);

        var kol = State.DomainsMap[referrerDomain]?.Invitee;
        SetFollowerCount(dappId, kol, 0, 1);

        Context.Fire(new ReferralAccepted
        {
            DappId = dappId,
            Domain = officialDomain,
            Referrer = referrer,
            Invitee = invitee,
            Inviter = inviter
        });

        return new Empty();
    }

    private void SetFollowerCount(Hash dappId, Address address, long followerCount, long subFollowerCount)
    {
        if (address == null) return;

        var referralFollowerCount = State.ReferralFollowerCountMap[dappId][address];

        if (referralFollowerCount == null)
        {
            State.ReferralFollowerCountMap[dappId][address] = new ReferralFollowerCount
            {
                FollowerCount = followerCount,
                SubFollowerCount = subFollowerCount
            };
        }
        else
        {
            referralFollowerCount.FollowerCount = referralFollowerCount.FollowerCount.Add(followerCount);
            referralFollowerCount.SubFollowerCount = referralFollowerCount.SubFollowerCount.Add(subFollowerCount);
        }
    }

    private PointsChangedDetails UpdateReferralSelfIncreasingPoint(Hash dappId, Address address, IncomeSourceType type,
        string pointName, PointsRule pointsRule, string domain, string actionName,
        PointsChangedDetails pointsChangedDetails)
    {
        var lastBlockTimestamp = State.ReferralPointsUpdateTimes[dappId][address][domain][type] ??
                                 State.LastPointsUpdateTimes[dappId][address][domain][type];
        if (lastBlockTimestamp != null)
        {
            var lastBlockTime = lastBlockTimestamp.Seconds;
            var waitingSettledPoints = CalculateWaitingSettledSelfIncreasingPointsForReferral(dappId, address,
                Context.CurrentBlockTime.Seconds, lastBlockTime, pointsRule, domain);

            pointsChangedDetails = UpdatePointsBalance(address, domain, type, pointName, waitingSettledPoints, dappId,
                actionName, pointsChangedDetails);
        }

        State.ReferralPointsUpdateTimes[dappId][address][domain][type] = Context.CurrentBlockTime;

        return pointsChangedDetails;
    }

    private BigIntValue CalculateWaitingSettledSelfIncreasingPointsForReferral(Hash dappId, Address address,
        long currentBlockTime, long lastBlockTime, PointsRule pointsRule, string domain)
    {
        var totalPoints = new BigIntValue(0);

        var timeGap = currentBlockTime.Sub(lastBlockTime);

        var followerCount = State.ReferralFollowerCountMap[dappId][address];
        if (followerCount == null) return totalPoints;

        var domainRelationshipInfo = State.DomainsMap[domain];

        // kol
        if (domainRelationshipInfo != null && address == domainRelationshipInfo.Invitee)
        {
            totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.KolPointsPercent,
                    followerCount.SubFollowerCount, timeGap)
                .Mul(pointsRule.KolPointsPercent)
                .Div(PointsContractConstants.Denominator));
        }
        else
        {
            // calculate follower points
            totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.KolPointsPercent,
                followerCount.FollowerCount, timeGap));

            // calculate subFollower points
            totalPoints = totalPoints.Add(CalculatePoints(pointsRule.UserPoints, pointsRule.InviterPointsPercent,
                followerCount.SubFollowerCount, timeGap));
        }

        return totalPoints;
    }

    private BigIntValue CalculatePoints(BigIntValue points, long percent, long count, long timeGap)
    {
        return points.Mul(percent).Div(PointsContractConstants.Denominator).Mul(count).Mul(timeGap);
    }
}