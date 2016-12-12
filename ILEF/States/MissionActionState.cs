
namespace ILEF.States
{
    public enum ActionState
    {
        LogWhatIsOnGrid,
        MoveTo,
        OrbitEntity,
        MoveToBackground,
        Activate,
        WaitUntilTargeted,
        WaitUntilAggressed,
        ClearPocket,
        ClearAggro,
        ClearWithinWeaponsRangeOnly,
        ClearWithinWeaponsRangewAggroOnly,
        AggroOnly,
        AddWarpScramblerByName,
        AddWebifierByName,
        AddEcmNpcByName,
        Kill,
        KillOnce,
        UseDrones,
        KillClosestByName,
        KillClosest,
        Ignore,
        Loot,
        LootItem,
        Salvage,
        Analyze,
        PutItem,
        DropItem,
        Done,
        SalvageBookmark,
        DebuggingWait,
        ActivateBastion,
    }
}