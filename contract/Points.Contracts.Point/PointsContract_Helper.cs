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
        // todo: add domain protocol format
        // 格式校验，特殊字符，中文
        Assert(domain.Length is > 0 and <= PointsContractConstants.DomainNameLength, "Invalid domain.");
    }
}