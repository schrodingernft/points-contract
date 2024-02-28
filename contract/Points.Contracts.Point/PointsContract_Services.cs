using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty SetServicesEarningRules(SetServicesEarningRulesInput input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input.ServicesEarningRules != null, "Invalid input.");
        foreach (var rule in input.ServicesEarningRules.EarningRules)
        {
            Assert(!string.IsNullOrEmpty(rule.PointName) && State.PointInfos[rule.PointName] != null,
                "Wrong points information.");
            Assert(!string.IsNullOrEmpty(rule.ActionName), "ActionName cannot be empty.");
            Assert(rule.UserPoints > 0 && rule.KolPoints > 0 && rule.InviterPoints > 0,
                "Points must large than 0.");
        }

        State.ServicesEarningRulesMap[input.Service] = input.ServicesEarningRules;

        Context.Fire(new ServicesEarningRulesChanged
        {
            Service = input.Service,
            ServicesEarningRules = input.ServicesEarningRules
        });

        return new Empty();
    }

    public override GetServicesEarningRulesOutput GetServicesEarningRules(GetServicesEarningRulesInput input)
    {
        Assert(State.ServicesEarningRulesMap[input.Service] != null, "Service not registry yet.");

        return new GetServicesEarningRulesOutput { ServiceEarningRules = State.ServicesEarningRulesMap[input.Service] };
    }
}