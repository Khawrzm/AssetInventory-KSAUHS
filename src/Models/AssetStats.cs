namespace AssetInventory.Models;

public record AssetStats(int Total, int Verified, int Pending, int Disposed, int Transferred)
{
    public double VerifiedPct   => Total == 0 ? 0 : Math.Round(Verified   * 100.0 / Total, 1);
    public double PendingPct    => Total == 0 ? 0 : Math.Round(Pending    * 100.0 / Total, 1);
    public double DisposedPct   => Total == 0 ? 0 : Math.Round(Disposed   * 100.0 / Total, 1);
    public double TransferredPct=> Total == 0 ? 0 : Math.Round(Transferred* 100.0 / Total, 1);
}
