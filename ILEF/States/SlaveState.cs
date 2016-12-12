
namespace ILEF.States
{
    public enum SlaveState
    {
        Idle,
        Begin,
        AddPriorityTargets,
        TravelToMasterLocationID,
        FindMaster,
        IsMasterDocked,
        Done,
    }
}