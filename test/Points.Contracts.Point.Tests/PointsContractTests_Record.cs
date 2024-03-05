using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task ApplyToOperatorTests()
    {
        await Initialize();
        await CreatePoint();
        await SetDappInformation();
        await SetSelfIncreasingPointsRules();
        await SetMaxApplyCount();

        const string domain = "user.com";
        var result = await PointsContractStub.ApplyToOperator.SendAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var getResult = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain });
        getResult.Domain.ShouldBe(domain);
        getResult.Invitee.ShouldBe(UserAddress);
        getResult.Inviter.ShouldBe(DefaultAddress);

        const string domain1 = "user1.com";
        result = await PointsContractStub.ApplyToOperator.SendAsync(new ApplyToOperatorInput
        {
            Domain = domain1,
            DappId = DefaultDappId,
            Invitee = DefaultAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        getResult = await PointsContractStub.GetDomainApplyInfo.CallAsync(new StringValue { Value = domain1 });
        getResult.Domain.ShouldBe(domain1);
        getResult.Invitee.ShouldBe(DefaultAddress);
        getResult.Inviter.ShouldBeNull();

        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = DefaultPointName
        });
        getBalanceResult.PointName.ShouldBe(DefaultPointName);
        getBalanceResult.Balance.ShouldBe(100000);

        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = domain1,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = DefaultPointName
        });
        getBalanceResult.PointName.ShouldBe(DefaultPointName);
        getBalanceResult.Balance.ShouldBe(1000000);
    }

    [Fact]
    public async Task ApplyToOperatorTests_Fail()
    {
        await Initialize();
        await CreatePoint();
        await SetDappInformation();
        await SetMaxApplyCount();

        const string domain = "example.com";
        var result = await PointsContractStub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            Domain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10)),
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await PointsContractStub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = HashHelper.ComputeFrom("NotExistDappName"),
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid dapp id.");

        result = await PointsContractStub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = DefaultDappId,
        });
        result.TransactionResult.Error.ShouldContain("Invalid invitee.");

        await PointsContractStub.ApplyToOperator.SendAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = DefaultDappId,
            Invitee = UserAddress
        });

        result = await PointsContractStub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Domain has Exist.");

        for (var i = 0; i < DefaultMaxApply.Value; i++)
        {
            await PointsContractUser2Stub.ApplyToOperator.SendAsync(new ApplyToOperatorInput
            {
                Domain = $"user{i}.com",
                DappId = DefaultDappId,
                Invitee = UserAddress
            });
        }

        result = await PointsContractUser2Stub.ApplyToOperator.SendWithExceptionAsync(new ApplyToOperatorInput
        {
            Domain = "exceedMaxApply.com",
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Apply count exceed the limit.");
    }

    [Fact]
    public async Task JoinTests()
    {
        await Initialize();
        await CreatePoint();
        await SetDappInformation();
        await SetSelfIncreasingPointsRules();
        await SetMaxApplyCount();

        var currentBlockTime = BlockTimeProvider.GetBlockTime();
        // official domain
        var result = await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(5));
        var getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = DefaultOfficialDomain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.Balance.ShouldBe(20000000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = DefaultOfficialDomain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.Balance.ShouldBe(5 * 10000000); // join + 20000000, increasing + 10000000*5

        // create domain
        const string domain = "abc.com";
        await PointsContractStub.ApplyToOperator.SendAsync(new ApplyToOperatorInput
        {
            Domain = domain,
            DappId = DefaultDappId,
            Invitee = UserAddress
        });
        result = await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = domain,
            Registrant = User2Address
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(10));

        // user balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.Balance.ShouldBe(20000000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = User2Address,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.User,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.Balance.ShouldBe(5 * 10000000); // join + 20000000, increasing + 10000000*5

        // invitee balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.Balance.ShouldBe(2000000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = UserAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Kol,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.Balance.ShouldBe(5 * 1000000); // join + 20000000, increasing + 10000000*5

        // inviter balance
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = JoinPointName
        });
        getBalanceResult.PointName.ShouldBe(JoinPointName);
        getBalanceResult.Balance.ShouldBe(200000); // join + 20000000, increasing + 10000000*5
        getBalanceResult = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = DefaultDappId,
            Address = DefaultAddress,
            Domain = domain,
            IncomeSourceType = IncomeSourceType.Inviter,
            PointName = SelfIncreasingPointName
        });
        getBalanceResult.PointName.ShouldBe(SelfIncreasingPointName);
        getBalanceResult.Balance.ShouldBe(5 * 100000); // join + 20000000, increasing + 10000000*5
    }

    [Fact]
    public async Task JoinTests_Fail()
    {
        await Initialize();
        await CreatePoint();
        await SetDappInformation();
        await SetSelfIncreasingPointsRules();
        await SetMaxApplyCount();

        // not dapp admin
        var result = await PointsContractUserStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("No permission.");

        // register is null
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = DefaultOfficialDomain,
        });
        result.TransactionResult.Error.ShouldContain("Invalid registrant address.");

        // invalid domain 
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = "",
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = string.Join(".", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 10)),
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        // join not exist domain
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = "not-exist.com",
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Not exist domain.");

        // register twice
        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result = await PointsContractStub.Join.SendWithExceptionAsync(new JoinInput
        {
            DappId = DefaultDappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
    }
}