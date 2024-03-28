using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace Points.Contracts.Point;

public partial class PointsContractTests
{
    private const long UserPoints = 6180000;
    private const long Period = 5;
    private const long KolPointsPercent = 1600;
    private const long InviterPointsPercent = 800;

    [Fact]
    public async Task AcceptReferralTests()
    {
        // Inviter -> null
        // Referrer -> DefaultAddress
        // Invitee -> UserAddress
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
        log.Inviter.ShouldBeNull();

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = UserAddress
            });

            output.DappId.ShouldBe(dappId);
            output.Invitee.ShouldBe(UserAddress);
            output.Referrer.ShouldBe(DefaultAddress);
            output.Inviter.ShouldBeNull();
        }

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = UserAddress,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // referrer
        {
            // accept referral
            var point = new BigIntValue(UserPoints + UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                JoinPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, DefaultAddress, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, DefaultAddress, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, DefaultAddress, DefaultOfficialDomain)
                .ShouldBe(point);
        }

        // invitee
        {
            // accept referral
            var point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, JoinPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, UserAddress, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName).Result
                .ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, UserAddress, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SelfIncreasingPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, UserAddress, DefaultOfficialDomain)
                .ShouldBe(point);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Inviter()
    {
        // Inviter -> DefaultAddress
        // Referrer -> UserAddress
        // Invitee -> User2Address
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
        log.Inviter.ShouldBeNull();

        var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = UserAddress
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(UserAddress);
        output.Referrer.ShouldBe(DefaultAddress);
        output.Inviter.ShouldBeNull();

        result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = UserAddress,
            Invitee = User2Address
        });

        log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(UserAddress);
        log.Invitee.ShouldBe(User2Address);
        log.Inviter.ShouldBe(DefaultAddress);

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = User2Address
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(User2Address);
        output.Referrer.ShouldBe(UserAddress);
        output.Inviter.ShouldBe(DefaultAddress);

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = User2Address,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // inviter
        {
            // accept referral
            var point = new BigIntValue(UserPoints + UserPoints * KolPointsPercent / 10000 +
                                        UserPoints * InviterPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                JoinPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, DefaultAddress, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints * InviterPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress, SettlePointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, DefaultAddress, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period +
                                    UserPoints * InviterPointsPercent / 10000 * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, DefaultAddress,
                SelfIncreasingPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, DefaultAddress, DefaultOfficialDomain)
                .ShouldBe(point);
        }

        // referrer
        {
            // accept referral
            var point = new BigIntValue(UserPoints + UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                JoinPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, UserAddress, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress, SettlePointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, UserAddress, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, UserAddress, DefaultOfficialDomain)
                .ShouldBe(point);
        }

        // invitee
        {
            // accept referral
            var point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                    JoinPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, User2Address, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName).Result
                .ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, User2Address, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                    SelfIncreasingPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, User2Address, DefaultOfficialDomain)
                .ShouldBe(point);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Kol()
    {
        const string domain = "user.com";

        // Inviter -> DefaultAddress, Kol
        // Referrer -> UserAddress
        // Invitee -> User2Address
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            Domain = domain,
            DappId = dappId,
            Invitee = DefaultAddress
        });

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = UserAddress
        });

        var result = await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = UserAddress,
            Invitee = User2Address
        });

        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.DappId.ShouldBe(dappId);
        log.Domain.ShouldBe(DefaultOfficialDomain);
        log.Referrer.ShouldBe(UserAddress);
        log.Invitee.ShouldBe(User2Address);
        log.Inviter.ShouldBeNull();

        var acceptLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
        {
            DappId = dappId,
            Invitee = User2Address
        });

        output.DappId.ShouldBe(dappId);
        output.Invitee.ShouldBe(User2Address);
        output.Referrer.ShouldBe(UserAddress);
        output.Inviter.ShouldBeNull();

        BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddSeconds(Period));

        result = await PointsContractStub.Settle.SendAsync(new SettleInput
        {
            DappId = dappId,
            ActionName = SettleActionName,
            UserAddress = User2Address,
            UserPointsValue = UserPoints
        });

        var settleLog = GetLogEvent<PointsChanged>(result.TransactionResult);

        // kol
        {
            // accept referral
            var point = new BigIntValue(UserPoints * KolPointsPercent / 10000 +
                                        UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress,
                JoinPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, DefaultAddress, domain).ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000);
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SettlePointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, DefaultAddress, domain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * KolPointsPercent / 10000 * Period +
                                    UserPoints * KolPointsPercent / 10000 * KolPointsPercent / 10000 * Period);
            GetPointsBalance(dappId, domain, IncomeSourceType.Kol, DefaultAddress, SelfIncreasingPointName).Result
                .ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, DefaultAddress, domain).ShouldBe(point);
        }

        // referrer
        {
            // accept referral
            var point = new BigIntValue(UserPoints + UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress, JoinPointName).Result
                .ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, UserAddress, domain).ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints * KolPointsPercent / 10000);
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress, SettlePointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, UserAddress, domain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period + UserPoints * KolPointsPercent / 10000 * Period);
            GetPointsBalance(dappId, domain, IncomeSourceType.User, UserAddress,
                SelfIncreasingPointName).Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, UserAddress, domain).ShouldBe(point);
        }

        // invitee
        {
            // accept referral
            var point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                    JoinPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(acceptLog, JoinPointName, User2Address, DefaultOfficialDomain)
                .ShouldBe(point);

            // settle
            point = new BigIntValue(UserPoints);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address, SettlePointName).Result
                .ShouldBe(point);
            CalculatePointsFromLog(settleLog, SettlePointName, User2Address, DefaultOfficialDomain).ShouldBe(point);

            // self increasing
            point = new BigIntValue(UserPoints * Period);
            GetPointsBalance(dappId, DefaultOfficialDomain, IncomeSourceType.User, User2Address,
                    SelfIncreasingPointName)
                .Result.ShouldBe(point);
            CalculatePointsFromLog(settleLog, SelfIncreasingPointName, User2Address, DefaultOfficialDomain)
                .ShouldBe(point);
        }
    }

    [Fact]
    public async Task AcceptReferralTests_Fail()
    {
        const string domain = "user.com";
        
        var dappId = await InitializeForAcceptReferralTests();

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput());
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = new Hash()
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId
            });
            result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("Referrer not joined.");
        }

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = UserAddress
        });
        
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("Invalid invitee.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid invitee.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = User2Address,
                Invitee = User2Address
            });
            result.TransactionResult.Error.ShouldContain("Referrer not joined.");
        }
        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = User2Address
        });

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = User2Address
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }
        
        await PointsContractStub.ApplyToBeAdvocate.SendAsync(new ApplyToBeAdvocateInput
        {
            DappId = dappId,
            Domain = domain,
            Invitee = DefaultAddress
        });
        
        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = domain,
            Registrant = User3Address
        });

        {
            var result = await PointsContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
            {
                DappId = dappId,
                Referrer = UserAddress,
                Invitee = User3Address
            });
            result.TransactionResult.Error.ShouldContain("A dapp can only be registered once.");
        }
    }

    [Fact]
    public async Task GetReferralRelationInfo()
    {
        var dappId = await InitializeForAcceptReferralTests();

        await PointsContractStub.Join.SendAsync(new JoinInput
        {
            DappId = dappId,
            Domain = DefaultOfficialDomain,
            Registrant = DefaultAddress
        });

        await PointsContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            DappId = dappId,
            Referrer = DefaultAddress,
            Invitee = UserAddress
        });

        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = UserAddress
            });
            output.DappId.ShouldBe(dappId);
            output.Invitee.ShouldBe(UserAddress);
            output.Referrer.ShouldBe(DefaultAddress);
            output.Inviter.ShouldBeNull();
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = DefaultAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                Invitee = UserAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = new Hash(),
                Invitee = UserAddress
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
        {
            var output = await PointsContractStub.GetReferralRelationInfo.CallAsync(new GetReferralRelationInfoInput
            {
                DappId = dappId,
                Invitee = new Address()
            });
            output.ShouldBe(new ReferralRelationInfo());
        }
    }

    private async Task<Hash> InitializeForAcceptReferralTests()
    {
        await Initialize();
        var dappId = await AddDapp();
        await CreatePoint(dappId);
        await SetMaxApplyCount();

        await PointsContractStub.SetDappPointsRules.SendAsync(new SetDappPointsRulesInput
        {
            DappId = dappId,
            DappPointsRules = new PointsRuleList
            {
                PointsRules =
                {
                    new PointsRule
                    {
                        ActionName = DefaultActionName,
                        PointName = DefaultPointName,
                        UserPoints = 10000000,
                        KolPointsPercent = 1000000,
                        InviterPointsPercent = 100000
                    },
                    new PointsRule
                    {
                        ActionName = JoinActionName,
                        PointName = JoinPointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = AcceptReferralActionName,
                        PointName = JoinPointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    },
                    new PointsRule
                    {
                        ActionName = SettleActionName,
                        PointName = SettlePointName,
                        UserPoints = UserPoints,
                        KolPointsPercent = KolPointsPercent,
                        InviterPointsPercent = InviterPointsPercent,
                        EnableProportionalCalculation = true
                    }
                }
            }
        });

        await PointsContractStub.SetSelfIncreasingPointsRules.SendAsync(new SetSelfIncreasingPointsRulesInput
        {
            DappId = dappId,
            SelfIncreasingPointsRule = new PointsRule
            {
                ActionName = SelfIncreaseActionName,
                PointName = SelfIncreasingPointName,
                UserPoints = 6180000,
                KolPointsPercent = KolPointsPercent,
                InviterPointsPercent = InviterPointsPercent,
                EnableProportionalCalculation = true
            }
        });

        return dappId;
    }

    private T GetLogEvent<T>(TransactionResult transactionResult) where T : IEvent<T>, new()
    {
        var log = transactionResult.Logs.FirstOrDefault(l => l.Name == typeof(T).Name);
        log.ShouldNotBeNull();

        var logEvent = new T();
        logEvent.MergeFrom(log.NonIndexed);

        return logEvent;
    }

    private async Task<BigIntValue> GetPointsBalance(Hash dappId, string domain, IncomeSourceType type, Address address,
        string pointName)
    {
        var output = await PointsContractStub.GetPointsBalance.CallAsync(new GetPointsBalanceInput
        {
            DappId = dappId,
            Domain = domain,
            IncomeSourceType = type,
            Address = address,
            PointName = pointName
        });

        return output.BalanceValue;
    }

    private BigIntValue CalculatePointsFromLog(PointsChanged log, string pointsName, Address address, string domain)
    {
        var result = new BigIntValue(0);

        foreach (var detail in log.PointsChangedDetails.PointsDetails)
        {
            if (detail.PointsName == pointsName && detail.PointsReceiver == address && detail.Domain == domain)
            {
                result = result > detail.BalanceValue ? result : detail.BalanceValue;
            }
        }

        return result;
    }
}