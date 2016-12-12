
namespace ILEF.States
{
    public enum DedicatedBookmarkSalvagerBehaviorState
    {
        Default,
        Idle,
        MissionStatistics,
        DelayedStart,
        Cleanup,
        Start,
        Arm,
        LocalWatch,
        WaitingforBadGuytoGoAway,
        WarpOutStation,
        DelayedGotoBase,
        GotoBase,
        UnloadLoot,
        CheckBookmarkAge,
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
    }
}