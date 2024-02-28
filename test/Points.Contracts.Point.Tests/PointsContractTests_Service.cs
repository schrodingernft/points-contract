using System.Threading.Tasks;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task SetServicesEarningRulesTests()
    {
        await Initialize();
        await CreatePoint();

        var result = await PointsContractStub.SetServicesEarningRules.SendAsync(new SetServicesEarningRulesInput
        {
            Service = "abc",
            ServicesEarningRules = new EarningRuleList
            {
                EarningRules =
                {
                    new EarningRule
                    {
                        ActionName = "join",
                        PointName = "ABC-1",
                        UserPoints = 100,
                        KolPoints = 10,
                        InviterPoints = 1
                    }
                }
            }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }


    [Fact]
    public async Task SetServicesEarningRulesTests_Fail()
    {
        var input = new SetServicesEarningRulesInput
        {
            Service = "abc",
            ServicesEarningRules = new EarningRuleList
            {
                EarningRules =
                {
                    new EarningRule
                    {
                        ActionName = "join",
                        PointName = "ABC-1",
                        UserPoints = 100,
                        KolPoints = 10,
                        InviterPoints = 1
                    }
                }
            }
        };

        var result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Not initialized.");
        await Initialize();

        result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Wrong points information.");
        await CreatePoint();
        
        result = await PointsContractUserStub.SetServicesEarningRules.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(
            new SetServicesEarningRulesInput
            {
                Service = "abc",
                ServicesEarningRules = new EarningRuleList
                {
                    EarningRules =
                    {
                        new EarningRule
                        {
                            PointName = "ABC-1",
                            UserPoints = 100,
                            KolPoints = 10,
                            InviterPoints = 1
                        }
                    }
                }
            });
        result.TransactionResult.Error.ShouldContain("ActionName cannot be empty.");

        result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(
            new SetServicesEarningRulesInput
            {
                Service = "abc",
                ServicesEarningRules = new EarningRuleList
                {
                    EarningRules =
                    {
                        new EarningRule
                        {
                            ActionName = "join",
                            PointName = "ABC-1",
                            KolPoints = 10,
                            InviterPoints = 1
                        }
                    }
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must large than 0.");

        result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(
            new SetServicesEarningRulesInput
            {
                Service = "abc",
                ServicesEarningRules = new EarningRuleList
                {
                    EarningRules =
                    {
                        new EarningRule
                        {
                            ActionName = "join",
                            PointName = "ABC-1",
                            UserPoints = 100,
                            InviterPoints = 1
                        }
                    }
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must large than 0.");
        
        result = await PointsContractStub.SetServicesEarningRules.SendWithExceptionAsync(
            new SetServicesEarningRulesInput
            {
                Service = "abc",
                ServicesEarningRules = new EarningRuleList
                {
                    EarningRules =
                    {
                        new EarningRule
                        {
                            ActionName = "join",
                            PointName = "ABC-1",
                            KolPoints = 10,
                            InviterPoints = 1
                        }
                    }
                }
            });
        result.TransactionResult.Error.ShouldContain("Points must large than 0.");
    }
}