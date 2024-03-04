using AElf;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty SetDappInformation(SetDappInformationInput input)
    {
        AssertInitialized();
        AssertAdmin();
        AssertDomainFormat(input.OfficialDomain);
        Assert(input.DappAdmin.Value != null && input.DappId != null &&
               input.DappsEarningRules?.EarningRules?.Count > 0, "Invalid earning rules.");

        foreach (var rule in input.DappsEarningRules.EarningRules)
        {
            Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[rule.PointName] != null,
                "Wrong points name input.");
            Assert(!string.IsNullOrEmpty(rule.ActionName), "ActionName cannot be empty.");
            Assert(rule.UserPoints > 0 && rule.KolPoints > 0 && rule.InviterPoints > 0,
                "Points must be greater than 0.");
        }

        var dappInfo = new DappInfo
        {
            DappAdmin = input.DappAdmin,
            OfficialDomain = input.OfficialDomain,
            DappsEarningRules = input.DappsEarningRules
        };
        State.DappInfos[input.DappId] = dappInfo;

        Context.Fire(new DappInformationChanged
        {
            DappId = input.DappId,
            DappInfo = dappInfo
        });

        return new Empty();
    }

    public override Empty SetSelfIncreasingPointsRules(SetSelfIncreasingPointsRulesInput input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input.DappId != null, "Invalid dapp id.");
        var rule = input.SelfIncreasingEarningRule;
        Assert(rule != null, "Invalid self-increasing points rules.");
        Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[rule.PointName] != null,
            "Wrong points name input.");
        Assert(rule.UserPoints > 0 && rule.KolPoints > 0 && rule.InviterPoints > 0, "Points must be greater than 0.");

        State.SelfIncreasingPointsRules[input.DappId] = rule;

        Context.Fire(new SelfIncreasingPointsRulesChanged
        {
            DappId = input.DappId,
            PointName = rule.PointName,
            UserPoints = rule.UserPoints,
            KolPoints = rule.KolPoints,
            InviterPoints = rule.InviterPoints
        });
        return new Empty();
    }
}