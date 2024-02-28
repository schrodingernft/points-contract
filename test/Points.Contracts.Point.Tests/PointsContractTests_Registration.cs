using System.Threading.Tasks;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    [Fact]
    public async Task RecordRegistrationTests()
    {
        await Initialize();

        var result = await PointsContractStub.RecordRegistration.SendAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    },
                    new RegistrationRecords
                    {
                        Service = "BCD",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "bcd.com",
                                Registrant = User2Address,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });

        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    [Fact]
    public async Task RecordRegistrationTests_Fail()
    {
        var input = new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        };
        var result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Not initialized.");

        await Initialize();

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("Domain not exist.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput());
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
            { RegistrationRecordList = new RegistrationRecordList() });
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Service cannot be empty.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Service cannot be empty.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "",
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Domain cannot be empty.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Registrant = UserAddress,
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Domain cannot be empty.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                CreateTime = BlockTimeProvider.GetBlockTime()
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Registrant address is empty.");

        result = await PointsContractStub.RecordRegistration.SendWithExceptionAsync(new RecordRegistrationInput
        {
            RegistrationRecordList = new RegistrationRecordList
            {
                RegistrationRecords =
                {
                    new RegistrationRecords
                    {
                        Service = "ABC",
                        RegistrationRecordDetail =
                        {
                            new RegistrationRecordDetail
                            {
                                Domain = "abc.com",
                                Registrant = UserAddress
                            }
                        }
                    }
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("CreateTime not set.");

        result = await PointsContractUserStub.RecordRegistration.SendWithExceptionAsync(input);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
}