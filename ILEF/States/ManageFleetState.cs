
namespace ILEF.States
{
    public enum ManageFleetState
    {
        Idle,
        HowManyConfiguredFleetMembers,
        HowManyConfiguredFleetMembersInLocal,
        HowManyFleetMembers,
        //HowManyFleetMembersInLocal //why would we ever need to know this?
        InviteConfiguredFleetMembersInLocal,
        //InviteCharactersThatXup,
        //MakeMoreSquads,
        //MakeMoreWings,
        PassBossToMaster,
        LeaveFleet,
        KickUnauthorizedFleetMembers,
        KickSpecificFleetMember,
        //NameFleet,
        //MoveFleetMembersToGetBonuses
        Done,
    }
}