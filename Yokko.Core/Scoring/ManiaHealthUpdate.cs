namespace Yokko.Core.Scoring;

public readonly record struct ManiaHealthUpdate(
    double PreviousHealth,
    double Health,
    bool ExtraLifeConsumed,
    ManiaFailReason FailReason)
{
    public bool Failed => FailReason != ManiaFailReason.None;
}
