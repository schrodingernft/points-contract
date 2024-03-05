using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests : PointsContractTestBase
{
    [Fact]
    public async Task InitializeTests()
    {
        await Initialize();

        var admin = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(DefaultAddress);

        // initialize twice
        var result = await PointsContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Already initialized.");
    }

    [Fact]
    public async Task InitializeTests_Fail()
    {
        // empty address
        var result = await PointsContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = new Address(),
        });
        result.TransactionResult.Error.ShouldContain("Invalid input admin.");

        // sender != author
        result = await PointsContractUserStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = UserAddress,
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task SetAdminTests()
    {
        await Initialize();

        var output = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        output.ShouldBe(DefaultAddress);

        var result = await PointsContractStub.SetAdmin.SendAsync(DefaultAddress);
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        await PointsContractStub.SetAdmin.SendAsync(UserAddress);
        output = await PointsContractStub.GetAdmin.CallAsync(new Empty());
        output.ShouldBe(UserAddress);
    }

    [Fact]
    public async Task SetAdminTests_Fail()
    {
        await Initialize();

        var result = await PointsContractStub.SetAdmin.SendWithExceptionAsync(new Address());
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractUserStub.SetAdmin.SendWithExceptionAsync(UserAddress);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task SetReservedDomainListTests()
    {
        await Initialize();

        var reservedDomains = new ReservedDomainList { Domains = { "abc.com", "bca.com", "cab.com" } };
        await PointsContractStub.SetReservedDomainList.SendAsync(new SetReservedDomainListInput
        {
            ReservedDomainList = reservedDomains
        });

        var domains = await PointsContractStub.GetReservedDomainList.CallAsync(new Empty());
        domains.ReservedDomainList.ShouldBe(reservedDomains);
    }

    [Fact]
    public async Task SetReservedDomainListTests_Fail()
    {
        var result =
            await PointsContractStub.SetReservedDomainList.SendWithExceptionAsync(new SetReservedDomainListInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.SetReservedDomainList
            .SendWithExceptionAsync(new SetReservedDomainListInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await PointsContractStub.SetReservedDomainList
            .SendWithExceptionAsync(new SetReservedDomainListInput());
        result.TransactionResult.Error.ShouldContain("Invalid reserved domain list count.");
    }

    [Fact]
    public async Task CreatePointTests()
    {
        await Initialize();

        var result = await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
        {
            PointsName = DefaultPointName,
            Decimals = 8
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    [Fact]
    public async Task CreatePointTests_Fail()
    {
        var result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput());
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput());
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput());
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            PointsName = ""
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            Decimals = 8
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            PointsName = DefaultPointName,
            Decimals = -1
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            PointsName = string.Join("-", Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 4))
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            PointsName = DefaultPointName,
            Decimals = 19
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        await CreatePoint();
        result = await PointsContractStub.CreatePoint.SendWithExceptionAsync(new CreatePointInput
        {
            PointsName = DefaultPointName,
            Decimals = 8
        });
        result.TransactionResult.Error.ShouldContain("Point token already exists.");
    }

    [Fact]
    public async Task MaxApplyCountTest()
    {
        await Initialize();

        await SetMaxApplyCount();
        var getResult = await PointsContractStub.GetMaxApplyCount.CallAsync(new Empty());
        getResult.ShouldBe(DefaultMaxApply);
    }

    [Fact]
    public async Task MaxApplyCountTest_Fail()
    {
        var result = await PointsContractStub.SetApplyDomainMaxCount.SendWithExceptionAsync(DefaultMaxApply);
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractUserStub.SetApplyDomainMaxCount.SendWithExceptionAsync(DefaultMaxApply);
        result.TransactionResult.Error.ShouldContain("No permission.");

        var errorMaxApply = new Int32Value { Value = -1 };
        result = await PointsContractStub.SetApplyDomainMaxCount.SendWithExceptionAsync(errorMaxApply);
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        errorMaxApply = new Int32Value();
        result = await PointsContractStub.SetApplyDomainMaxCount.SendWithExceptionAsync(errorMaxApply);
        result.TransactionResult.Error.ShouldContain("Invalid input.");
    }

    private async Task SetMaxApplyCount() => await PointsContractStub.SetApplyDomainMaxCount.SendAsync(DefaultMaxApply);
    private async Task Initialize() => await PointsContractStub.Initialize.SendAsync(new InitializeInput());

    private async Task CreatePoint()
    {
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { PointsName = DefaultPointName, Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { PointsName = JoinPointName, Decimals = 8 });
        await PointsContractStub.CreatePoint.SendAsync(new CreatePointInput
            { PointsName = SelfIncreasingPointName, Decimals = 8 });
    }
}