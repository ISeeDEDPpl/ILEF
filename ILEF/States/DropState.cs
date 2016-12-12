namespace ILEF.States
{
    public enum DropState
    {
        Idle,
        Begin,
        ReadyItemhangar,
        OpenCargo,
        MoveItems,
        AllItems,
        WaitForMove,
        StackItemsHangar,
        WaitForStacking,
        Done,
    }
}