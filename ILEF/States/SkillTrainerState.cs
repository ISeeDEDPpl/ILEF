
namespace ILEF.States
{
    //using LavishScriptAPI;
    //using Questor.Modules.Caching;
    //using Questor.Modules.Lookup;

    public static class _State
    {

        //public static void LavishEvent_QuestorPausedState()
        //{
        //    if (Logging.UseInnerspace)
        //    {
        //        uint QuestorPausedStateEvent = LavishScript.Events.RegisterEvent("QuestorPausedState");
        //        LavishScript.Events.ExecuteEvent(QuestorPausedStateEvent, Cache.Instance.Paused.ToString());
        //    }
        //}

        public static SkillTrainerState CurrentSkillTrainerState { get; set; }
    }

    public enum SkillTrainerState
    {
        Idle,
        Begin,
        Done,
        LoadPlan,
        ReadCharacterSheetSkills,
        AreThereSkillsReadyToInject,
        CheckTrainingQueue,
        Error,
        CloseQuestor,
        GenerateInnerspaceProfile,
        BuyingSkill,
    }
}