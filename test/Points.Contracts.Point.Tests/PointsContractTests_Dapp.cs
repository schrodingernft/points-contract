using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task SetDappInformationTests()
    {
        await Initialize();
        await CreatePoint();
        await SetDappInformation();

        var getResult = await PointsContractStub.GetDappInformation.CallAsync(new GetDappInformationInput
        {
            DappId = DefaultDappId
        });
        getResult.DappInfo.DappAdmin.ShouldBe(DefaultAddress);
        getResult.DappInfo.OfficialDomain.ShouldBe(DefaultOfficialDomain);
        getResult.DappInfo.DappsEarningRules.EarningRules.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetDappInformationTests_Fail()
    {
        var result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(new SetDappInformationInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.SetDappInformation.SendWithExceptionAsync(new SetDappInformationInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        var input = new SetDappInformationInput
        {
            DappAdmin = DefaultAddress,
            DappId = DefaultDappId,
            OfficialDomain = DefaultOfficialDomain,
            DappsEarningRules = new PointsRuleList()
        };

        input.OfficialDomain = "";
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        input.OfficialDomain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10));
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        input.OfficialDomain = DefaultOfficialDomain;
        input.DappAdmin = new Address();
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid earning rules.");

        input.DappAdmin = DefaultAddress;
        input.DappId = Hash.Empty;
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid earning rules.");

        input.DappId = DefaultDappId;
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Invalid earning rules.");

        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = DefaultPointName,
            UserPoints = 10000000,
            KolPoints = 1000000,
            InviterPoints = 100000
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = "",
            UserPoints = 10000000,
            KolPoints = 1000000,
            InviterPoints = 100000
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        await CreatePoint();
        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = "",
            PointName = DefaultPointName,
            UserPoints = 10000000,
            KolPoints = 1000000,
            InviterPoints = 100000
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("ActionName cannot be empty.");

        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = DefaultPointName,
            UserPoints = -1,
            KolPoints = 1000000,
            InviterPoints = 100000
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = DefaultPointName,
            UserPoints = 10000000,
            KolPoints = -1,
            InviterPoints = 100000
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");

        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = DefaultPointName,
            UserPoints = 10000000,
            KolPoints = 1000000,
            InviterPoints = -1
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);

        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
        input.DappsEarningRules = new PointsRuleList();
        input.DappsEarningRules.EarningRules.Add(new PointsRule
        {
            ActionName = DefaultActionName,
            PointName = DefaultPointName,
            UserPoints = 10000000,
            KolPoints = 1000000,
        });
        result = await PointsContractStub.SetDappInformation.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
    }

    [Fact]
    public async Task SetSelfIncreasingPointsRulesTests()
    {
        await Initialize();
        await CreatePoint();
        await SetSelfIncreasingPointsRules();

        var getResult = await PointsContractStub.GetSelfIncreasingPointsRule.CallAsync(
            new GetSelfIncreasingPointsRuleInput
            {
                DappId = DefaultDappId
            });
        getResult.Rule.PointName.ShouldBe(JoinPointName);
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
                SelfIncreasingEarningRule = new EarningRule
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
                SelfIncreasingEarningRule = new EarningRule
                {
                    PointName = "",
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                    InviterPoints = 100000
                }
            });
        result.TransactionResult.Error.ShouldContain("Wrong points name input.");

        await CreatePoint();

        result = await PointsContractStub.SetSelfIncreasingPointsRules.SendWithExceptionAsync(
            new SetSelfIncreasingPointsRulesInput
            {
                DappId = DefaultDappId,
                SelfIncreasingEarningRule = new EarningRule
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
                SelfIncreasingEarningRule = new EarningRule
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
                SelfIncreasingEarningRule = new EarningRule
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
                SelfIncreasingEarningRule = new EarningRule
                {
                    PointName = DefaultPointName,
                    UserPoints = 10000000,
                    KolPoints = 1000000,
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must be greater than 0.");
    }

    private async Task SetSelfIncreasingPointsRules()
    {
        await PointsContractStub.SetSelfIncreasingPointsRules.SendAsync(new SetSelfIncreasingPointsRulesInput
        {
            DappId = DefaultDappId,
            SelfIncreasingEarningRule = new EarningRule
            {
                PointName = JoinPointName,
                UserPoints = 10000000,
                KolPoints = 1000000,
                InviterPoints = 100000
            }
        });
    }

    private async Task SetDappInformation()
    {
        await PointsContractStub.SetDappInformation.SendAsync(new SetDappInformationInput
        {
            DappAdmin = DefaultAddress,
            DappId = DefaultDappId,
            OfficialDomain = DefaultOfficialDomain,
            DappsEarningRules = new PointsRuleList
            {
                EarningRules =
                {
                    new PointsRule
                    {
                        ActionName = DefaultActionName,
                        PointName = DefaultPointName,
                        UserPoints = 10000000,
                        KolPoints = 1000000,
                        InviterPoints = 100000
                    },
                    new PointsRule
                    {
                        ActionName = JoinActionName,
                        PointName = JoinPointName,
                        UserPoints = 20000000,
                        KolPoints = 2000000,
                        InviterPoints = 200000
                    }
                }
            }
        });
    }
}