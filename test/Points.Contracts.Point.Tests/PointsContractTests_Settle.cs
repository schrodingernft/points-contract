using System.Threading.Tasks;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task SettleTests()
    {
        var dappId = await JoinTests();
        await CreatePointForSettle(dappId);
        await SetDappPointsRulesForSettle(dappId);
        var domain = "abc.com";
        await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = "Adpot",
            UserAddress = User2Address,
            UserPoints = 131400
        });
        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.Balance.ShouldBe(131400);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.Balance.ShouldBe(131400*1600/10000);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = "XPSGR-5"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-5");
        getBalanceResult.Balance.ShouldBe(131400*800/10000);
        await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = "Reroll",
            UserAddress = User2Address,
            UserPoints = 191900
        });
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.Balance.ShouldBe(191900);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.Balance.ShouldBe(191900*1600/10000);
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = "XPSGR-6"
        });
        getBalanceResult.PointName.ShouldBe("XPSGR-6");
        getBalanceResult.Balance.ShouldBe(191900*800/10000);
    }
    
    private async Task SetDappPointsRulesForSettle(Hash dappId)
    {
        await PointsContractStub.SetDappPointsRules.SendAsync(new SetDappPointsRulesInput
        {
            DappId = dappId,
            DappPointsRules = new PointsRuleList
            {
                PointsRules =
                {
                    new PointsRule
                    {
                        ActionName = "Adpot",
                        PointName = "XPSGR-5",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = "Reroll",
                        PointName = "XPSGR-6",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = "Trade",
                        PointName = "XPSGR-7",
                        UserPoints = 0,
                        KolPointsPercent = 1600,
                        InviterPointsPercent = 800,
                        EnableProportionalCalculation = true
                    }
                }
            }
        });
    }
}