
namespace ILEF.States
{
    public enum ArmState
    {
        Idle,
        Begin,
        OpenShipHangar,
        ActivateCombatShip,
        RepairShop,
        MoveDrones,
        MoveBringItems,
        MoveOptionalBringItems,
        MoveCapBoosters,
        MoveAmmo,
        MoveMiningCrystals,
        StackAmmoHangar,
        ActivateSalvageShip,
        ActivateTransportShip,
        ActivateMiningShip,
        LoadSavedFitting,
        Cleanup,
        Done,
        NotEnoughAmmo,
        NotEnoughDrones,
        ActivateNoobShip,
    }
}