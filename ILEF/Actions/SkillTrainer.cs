
using LavishScriptAPI;
using Questor.Modules.BackgroundTasks;
using Questor.Modules.Caching;
using Questor.Modules.Lookup;
using Questor.Modules.States;

namespace Questor.Modules.Actions
{
	using System;
	//using System.Collections.Generic;
	//using System.Windows.Forms;
	using Questor.Modules.Logging;
	
	public static class SkillTrainerClass
	{
		static SkillTrainerClass()
		{
			Logging.Log("SkillTrainer", "Starting SkillTrainer", Logging.Orange);
			_State.CurrentSkillTrainerState = SkillTrainerState.Idle;
		}

		public static void ProcessState()
		{
			// Only pulse state changes every .5s
			if (DateTime.UtcNow < Time.Instance.NextSkillTrainerProcessState || DateTime.UtcNow < Time.Instance.QuestorStarted_DateTime.AddSeconds(15))
			{
				return;
			}
			
			Time.Instance.NextSkillTrainerProcessState = DateTime.UtcNow.AddMilliseconds(Time.Instance.SkillTrainerPulse_milliseconds);

			switch (_State.CurrentSkillTrainerState)
			{
				case SkillTrainerState.Idle:
					if (Cache.Instance.InStation && DateTime.UtcNow > Time.Instance.NextSkillTrainerAction)
					{
						Logging.Log("SkillTrainer", "It is Time to Start SkillTrainer again...", Logging.White);
						_State.CurrentSkillTrainerState = SkillTrainerState.Begin;
					}
					break;

				case SkillTrainerState.Begin:
					_State.CurrentSkillTrainerState = SkillTrainerState.LoadPlan;
					SkillPlan.doneWithAllPlannedSKills = false;
					break;

				case SkillTrainerState.LoadPlan:
					Logging.Log("SkillTrainer", "LoadPlan", Logging.Debug);
					if (!SkillPlan.ImportSkillPlan())
					{
						_State.CurrentSkillTrainerState = SkillTrainerState.Error;
						return;
					}

					SkillPlan.ReadySkillPlan();
					_State.CurrentSkillTrainerState = SkillTrainerState.ReadCharacterSheetSkills;
					break;
					
				case SkillTrainerState.BuyingSkill:
					Logging.Log("SkillTrainer", "BuyingSkill", Logging.Debug);
					if (!SkillPlan.BuySkill(SkillPlan.buyingSkillTypeID, SkillPlan.buyingSkillTypeName)) return;
					_State.CurrentSkillTrainerState = SkillTrainerState.ReadCharacterSheetSkills;
					break;

				case SkillTrainerState.ReadCharacterSheetSkills:
					Logging.Log("SkillTrainer", "ReadCharacterSheetSkills", Logging.Debug);
					if (!SkillPlan.ReadMyCharacterSheetSkills()) return;
					
					_State.CurrentSkillTrainerState = SkillTrainerState.CheckTrainingQueue;
					break;

					
				case SkillTrainerState.CheckTrainingQueue:
					if (!SkillPlan.RetrieveSkillQueueInfo()) return;
					if (!SkillPlan.CheckTrainingQueue("SkillTrainer")) return;

					_State.CurrentSkillTrainerState = SkillTrainerState.Done;
					break;

				case SkillTrainerState.CloseQuestor:
					Logging.Log("Startup", "Done Training: Closing EVE", Logging.Orange);
					Cache.Instance.CloseQuestorCMDLogoff = false;
					Cache.Instance.CloseQuestorCMDExitGame = true;
					Cache.Instance.CloseQuestorEndProcess = true;
					Settings.Instance.AutoStart = false;
					Cleanup.ReasonToStopQuestor = "Done Processing Skill Training Plan and adding skills as needed to the training queue";
					Cleanup.SignalToQuitQuestorAndEVEAndRestartInAMoment = true;
					Cleanup.CloseQuestor(Cleanup.ReasonToStopQuestor);
					break;

				case SkillTrainerState.GenerateInnerspaceProfile:
					Logging.Log("SkillTrainer", "Generating Innerspace Profile for this toon: running [GenerateInnerspaceProfile.iss] from your innerspace scripts directory", Logging.Teal);
                    if (Logging.UseInnerspace) LavishScript.ExecuteCommand("echo runscript GenerateInnerspaceProfile \"" + Settings.Instance.CharacterName + "\"");
                    if (Logging.UseInnerspace) LavishScript.ExecuteCommand("runscript GenerateInnerspaceProfile \"" + Settings.Instance.CharacterName + "\"");
					_State.CurrentSkillTrainerState = SkillTrainerState.Idle;
					break;

				case SkillTrainerState.Error:
					Logging.Log("SkillTrainer", "Note: SkillTrainer just entered the Error State. There is likely a missing skill plan or broken contents of the skillplan!", Logging.Teal);
					_States.CurrentSkillTrainerState = SkillTrainerState.Done;
					break;

				case SkillTrainerState.Done:
					SkillPlan.attemptsToDoSomethingWithNonInjectedSkills = 0;
					SkillPlan.doneWithAllPlannedSKills = false;
					SkillPlan.injectSkillBookAttempts = 0;
					Time.Instance.NextSkillTrainerAction = DateTime.UtcNow.AddHours(Cache.Instance.RandomNumber(3, 4));
					_State.CurrentSkillTrainerState = SkillTrainerState.Idle;
					_States.CurrentQuestorState = QuestorState.Idle;
					break;
					
					
			}
		}
	}
}