
namespace ILEF.States
{
    public enum CombatHelperBehaviorState
    {
        Default,
        Idle,
        CombatHelper,
        Salvage,
        Arm,
        LocalWatch,
        DelayedGotoBase,
        GotoBase,
        UnloadLoot,
        WarpOutStation,
        GotoNearestStation,
        Error,
        Paused,
        Panic,
        Traveler,
        LogCombatTargets,
        LogDroneTargets,
        LogStationEntities,
        LogStargateEntities,
        LogAsteroidBelts,
    }
}
