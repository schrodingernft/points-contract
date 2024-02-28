using System.Linq;
using AElf;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    public override Empty RecordRegistration(RecordRegistrationInput input)
    {
        AssertInitialized();
        AssertAdmin();
        Assert(input.RegistrationRecordList != null
               && input.RegistrationRecordList.RegistrationRecords.Count > 0
               && input.RegistrationRecordList.RegistrationRecords.Count <= State.MaxRegistrationListCount.Value,
            "Invalid input.");

        foreach (var registrationRecord in input.RegistrationRecordList.RegistrationRecords)
        {
            var serviceName = registrationRecord.Service;
            Assert(!string.IsNullOrEmpty(serviceName), "Service cannot be empty.");

            var registrationRecords = registrationRecord.RegistrationRecordDetail.DistinctBy(i => i.Registrant);
            foreach (var record in registrationRecords)
            {
                var domain = record.Domain;
                Assert(!string.IsNullOrEmpty(domain), "Domain cannot be empty.");

                var registrant = record.Registrant;
                Assert(registrant != null && !registrant.Value.IsNullOrEmpty(), "Registrant address is empty.");

                var createTime = record.CreateTime;
                Assert(createTime != null, "CreateTime not set.");
                // Assert(createTime < Context.CurrentBlockTime, "Wrong CreateTime.");

                Assert(State.DomainOperatorRelationshipMap[domain] != null, "Domain not exist.");
                // Assert(record.Registrant != State.DomainOperatorRelationshipMap[domain].Invitee,
                //     "Cannot register your own domain name.");
                Assert(State.RegistrationMap[serviceName]?[registrant] == null,
                    $"This user has already registered in {serviceName}");

                State.RegistrationMap[serviceName][registrant] = new RegistrationInfo
                {
                    Domain = domain,
                    CreateTime = createTime
                };
            }
        }

        Context.Fire(new Registered { RegistrationRecordList = input.RegistrationRecordList });

        return new Empty();
    }
}