
namespace ILEF.States
{
    public enum BackgroundBehaviorState
    {
        Default,
        Idle,
        Cleanup,
        Start,
        Arm,
        LocalWatch,
        WaitingforBadGuytoGoAway,
        WarpOutStation,
        GotoBase,
        BeginAfterMissionSalvaging,
        GotoSalvageBookmark,
        SalvageUseGate,
        SalvageNextPocket,
        Salvage,
        GotoNearestStation,
        Error,
        Paused,
        Panic,
        Traveler,
        BeginGotoBookmark,
        TravelToBookmark,
        SwitchToNoobShip1,
        SwitchToNoobShip2,
    }
}