namespace ILEF.States
{
    public enum SwitchShipState
    {
        Idle,
        Begin,
        OpenShipHangar,
        ActivateCombatShip,
        Done,
        NotEnoughAmmo,
        WaitForFitting,
        OpenFittingWindow,
        WaitForFittingWindow,
        ChoseFitting,
        Cleanup,
    }
}