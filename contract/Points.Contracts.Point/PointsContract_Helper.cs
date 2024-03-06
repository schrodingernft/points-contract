using System.Collections.Generic;
using System.Linq;
using AElf.Types;

namespace Points.Contracts.Point;

public partial class PointsContract
{
    private void AssertAdmin() => Assert(Context.Sender == State.Admin.Value, "No permission.");
    private void AssertInitialized() => Assert(State.Initialized.Value, "Not initialized.");

    private void AssertDappAdmin(Hash dappId)
    {
        Assert(dappId != null && State.DappInfos[dappId] != null && Context.Sender == State.DappInfos[dappId].DappAdmin,
            "No permission.");
    }

    private void AssertDappContractAddress(Hash dappId)
    {
        Assert(dappId != null && State.DappInfos[dappId] != null &&
               Context.Sender == State.DappInfos[dappId].DappContractAddress, "No permission.");
    }

    private void AssertDomainFormat(string domain)
    {
        var invalidChars = new List<char> { '<', '>', ':', '"', '/', '|', '?', '*' };
        Assert(domain.Length is > 0 and <= PointsContractConstants.DomainNameLength &&
               !(domain.StartsWith(".") || domain.EndsWith(".")) &&
               !domain.Any(c => invalidChars.Contains(c) || c > 126 || char.IsUpper(c)), "Invalid domain.");
    }
}