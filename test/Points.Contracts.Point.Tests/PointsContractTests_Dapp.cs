using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Kernel.Blockchain.Application;
using AElf.Types;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task AddDappTests()
    {
        await Initialize();
        var dappId = await AddDapp();

        var getResult = await PointsContractStub.GetDappInformation.CallAsync(new GetDappInformationInput
        {
            DappId = dappId
        });
        getResult.DappInfo.DappAdmin.ShouldBe(DefaultAddress);
        getResult.DappInfo.OfficialDomain.ShouldBe(DefaultOfficialDomain);
        // getResult.DappInfo.DappsPointRules.PointsRules.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddDappTests_Fail()
    {
        var result = await PointsContractStub.AddDapp.SendWithExceptionAsync(new AddDappInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.AddDapp.SendWithExceptionAsync(new AddDappInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        var input = new AddDappInput
        {
            DappAdmin = DefaultAddress,
            OfficialDomain = DefaultOfficialDomain,
        };

        input.OfficialDomain = "";
        result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        input.OfficialDomain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10));
        result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        // input.OfficialDomain = DefaultOfficialDomain;
        // input.DappAdmin = new Address();
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Invalid earning rules.");
        //
        // input.DappAdmin = DefaultAddress;
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Invalid earning rules.");
        //
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Invalid earning rules.");

        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = DefaultPointName,
        //     UserPoints = 10000000,
        //     KolPoints = 1000000,
        //     InviterPoints = 100000
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = "",
        //     UserPoints = 10000000,
        //     KolPoints = 1000000,
        //     InviterPoints = 100000
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        // await CreatePoint();
        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = "",
        //     PointName = DefaultPointName,
        //     UserPoints = 10000000,
        //     KolPoints = 1000000,
        //     InviterPoints = 100000
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("ActionName cannot be empty.");

        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = DefaultPointName,
        //     UserPoints = -1,
        //     KolPoints = 1000000,
        //     InviterPoints = 100000
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = DefaultPointName,
        //     UserPoints = 10000000,
        //     KolPoints = -1,
        //     InviterPoints = 100000
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = DefaultPointName,
        //     UserPoints = 10000000,
        //     KolPoints = 1000000,
        //     InviterPoints = -1
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        //
        // result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
        // input.DappsEarningRules = new PointsRuleList();
        // input.DappsEarningRules.EarningRules.Add(new PointsRule
        // {
        //     ActionName = DefaultActionName,
        //     PointName = DefaultPointName,
        //     UserPoints = 10000000,
        //     KolPoints = 1000000,
        // });
        // result = await PointsContractStub.AddDapp.SendWithExceptionAsync(input);
        // result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
    }

    [Fact]
    public async Task SetSelfIncreasingPointsRulesTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetSelfIncreasingPointsRules(dappId);

        var getResult = await PointsContractStub.GetSelfIncreasingPointsRule.CallAsync(
            new GetSelfIncreasingPointsRuleInput
            {
                DappId = dappId
            });
        getResult.Rule.PointName.ShouldBe(SelfIncreasingPointName);
        getResult.Rule.UserPoints.ShouldBe(10000000);
        getResult.Rule.KolPoints.ShouldBe(1000000);
        getResult.Rule.InviterPoints.ShouldBe(100000);
    }

    [Fact]
    public async Task SetSelfIncreasingPointsRulesTests_Fail()
    {
        var result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput());
        result.TransactionResult.Error.ShouldContain("Invalid dapp id.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput { DappId = DefaultDappId });
        result.TransactionResult.Error.ShouldContain("Invalid self-increasing points rules.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                    InviterPoints = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = "",
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                    InviterPoints = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        // await CreatePoint();

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = -1,
                    KolPoints = 1000000,
                    InviterPoints = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPoints = -1,
                    InviterPoints = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                    InviterPoints = -1
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new PointsRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
    }

    private async Task SetSelfIncreasingPointsRules(Hash dappId)
    {
        await PointsContractStub.SetSelfIncreasingPointsRules.SendAsync(new SetSelfIncreasingPointsRulesInput
        {
            DappId = dappId,
            SelfIncreasingEarningRule = new PointsRule
            {
                PointName = SelfIncreasingPointName,
                UserPoints = 10000000,
                KolPoints = 1000000,
                InviterPoints = 100000
            }
        });
    }

    private async Task<Hash> AddDapp()
    {
        var input = new AddDappInput
        {
            DappAdmin = DefaultAddress,
            OfficialDomain = DefaultOfficialDomain,
        };
        var result = await PointsContractStub.AddDapp.SendAsync(input);
        var blockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
        var previousBlockHash = (await blockchainService.GetBlockByHashAsync(result.TransactionResult.BlockHash)).Header
            .PreviousBlockHash;
        return HashHelper.ConcatAndCompute(previousBlockHash, result.TransactionResult.TransactionId,
            HashHelper.ComputeFrom(input));
    }
}