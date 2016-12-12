
namespace ILEF.States
{
    public enum UnloadLootState
    {
        Idle,
        Begin,
        MoveAmmo,
        MoveMissionGateKeys,
        MoveCommonMissionCompletionItems,
        MoveScripts,
        StackAmmoHangar,
        StackLootHangar,
        MoveLoot,
        Done,
    }
}