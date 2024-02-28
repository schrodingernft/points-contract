using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Points.Contracts.Point;

public partial class PointsContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }
    public SingletonState<Address> Admin { get; set; }
    public SingletonState<ReservedDomainList> ReservedDomains { get; set; }
    public MappedState<string, PointInfo> PointInfos { get; set; }
    public MappedState<string, EarningRuleList> ServicesEarningRulesMap { get; set; }
    public MappedState<string, DomainOperatorRelationship> DomainOperatorRelationshipMap { get; set; }
    public MappedState<Address, string, int> ApplyCount { get; set; }
    
    public MappedState<string, Address, RegistrationInfo> RegistrationMap { get; set; }
    public MappedState<string, int> ApplyLimitMap { get; set; }
    public SingletonState<int> MaxRecordListCount { get; set; }
    public SingletonState<int> MaxApplyCount { get; set; }
    public MappedState<string, Address, string, long> PointsPool { get; set; }
    public MappedState<string, PointInfo> PointsInfos { get; set; }
}