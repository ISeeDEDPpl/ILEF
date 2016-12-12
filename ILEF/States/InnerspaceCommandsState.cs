namespace Questor.Modules.States
{
    public enum InnerspaceCommandsState
    {
        Idle,
        LogAllEntities,
        ListEntitiesThatHaveUsLocked,
        ListPrimaryWeaponPriorityTargets,
        AddPWPT,
        AddDPT,
        ListCachedPocketInfo,
        Done
    }
}