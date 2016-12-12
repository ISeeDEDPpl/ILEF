namespace ILEF.States
{
    public enum SellState
    {
        Idle,
        Done,
        Begin,
        StartQuickSell,
        WaitForSellWindow,
        InspectOrder,
        WaitingToFinishQuickSell,
    }
}