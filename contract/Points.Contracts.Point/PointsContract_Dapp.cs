using AElf;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty AddDapp(AddDappInput input)
    {
        AssertInitialized();
        AssertAdmin();
        AssertDomainFormat(input.OfficialDomain);

        var dappId = HashHelper.ConcatAndCompute(Context.PreviousBlockHash, Context.TransactionId,
            HashHelper.ComputeFrom(input));
        var dappInfo = new DappInfo
        {
            DappAdmin = input.DappAdmin,
            OfficialDomain = input.OfficialDomain,
            DappContractAddress = input.DappContractAddress
        };

        State.DappInfos[dappId] = dappInfo;

        Context.Fire(new DappAdded
        {
            DappId = dappId,
            DappInfo = dappInfo
        });

        return new Empty();
    }

    public override Empty SetDappPointsRules(SetDappPointsRulesInput input)
    {
        AssertInitialized();
        var dappId = input.DappId;
        AssertDappAdmin(input.DappId);

        foreach (var rule in input.DappPointsRules.PointsRules)
        {
            Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[dappId][rule.PointName] != null,
                "Wrong points name input.");
            Assert(!string.IsNullOrEmpty(rule.ActionName), "ActionName cannot be empty.");
            Assert(rule.UserPoints > 0 && rule.KolPoints > 0 && rule.InviterPoints > 0,
                "Points must be greater than 0.");
        }

        var info = State.DappInfos[dappId];
        State.DappInfos[dappId] = new DappInfo
        {
            DappAdmin = info.DappAdmin,
            OfficialDomain = info.OfficialDomain,
            DappsPointRules = new PointsRuleList { PointsRules = { input.DappPointsRules.PointsRules } },
            DappContractAddress = info.DappContractAddress
        };

        return new Empty();
    }


    public override Empty SetSelfIncreasingPointsRules(SetSelfIncreasingPointsRulesInput input)
    {
        AssertInitialized();
        AssertDappAdmin(input.DappId);
        Assert(State.SelfIncreasingPointsRules[input.DappId] == null, "Self-increasing points rules already set.");

        var rule = input.SelfIncreasingPointsRule;
        Assert(rule != null, "Invalid self-increasing points rules.");
        Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[input.DappId][rule.PointName] != null,
            "Wrong points name input.");
        Assert(rule.UserPoints > 0 && rule.KolPoints > 0 && rule.InviterPoints > 0, "Points must be greater than 0.");

        State.SelfIncreasingPointsRules[input.DappId] = rule;

        Context.Fire(new SelfIncreasingPointsRulesChanged
        {
            DappId = input.DappId,
            PointName = rule.PointName,
            UserPoints = rule.UserPoints,
            KolPoints = rule.KolPoints,
            InviterPoints = rule.InviterPoints,
            Frequency = input.Frequency
        });
        return new Empty();
    }

    public override Empty CreatePoint(CreatePointInput input)
    {
        AssertInitialized();
        AssertDappAdmin(input.DappId);
        AssertValidCreateInput(input);

        var pointsName = input.PointsName;
        var decimals = input.Decimals;
        State.PointInfos[input.DappId][pointsName] = new PointInfo
        {
            TokenName = pointsName,
            Decimals = decimals
        };

        Context.Fire(new PointCreated
        {
            DappId = input.DappId,
            TokenName = pointsName,
            Decimals = decimals
        });
        return new Empty();
    }

    private void AssertValidCreateInput(CreatePointInput input)
    {
        Assert(input.PointsName.Length is > 0 and <= PointsContractConstants.TokenNameLength
               && input.Decimals is >= 0 and <= PointsContractConstants.MaxDecimals, "Invalid input.");

        var empty = new PointInfo();
        var existing = State.PointInfos[input.DappId][input.PointsName];
        Assert(existing == null || existing.Equals(empty), "Point token already exists.");
    }
}