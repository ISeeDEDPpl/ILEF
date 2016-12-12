﻿namespace Questor.Modules.BackgroundTasks
{
    using System;
    using Questor.Modules.Caching;
    using Questor.Modules.Lookup;
    using Questor.Modules.States;

    public class LocalWatch
    {
        private DateTime _lastAction;

        public void ProcessState()
        {
            switch (_States.CurrentLocalWatchState)
            {
                case LocalWatchState.Idle:

                    //checking local every 5 second
                    if (DateTime.UtcNow.Subtract(_lastAction).TotalSeconds < Time.Instance.CheckLocalDelay_seconds)
                        break;

                    _States.CurrentLocalWatchState = LocalWatchState.CheckLocal;
                    break;

                case LocalWatchState.CheckLocal:

                    //
                    // this ought to cache the name of the system, and the number of people in local (or similar)
                    // and only query everyone in local for standings changes if something has changed...
                    //
                    Cache.Instance.LocalSafe(Settings.Instance.LocalBadStandingPilotsToTolerate, Settings.Instance.LocalBadStandingLevelToConsiderBad);

                    _lastAction = DateTime.UtcNow;
                    _States.CurrentLocalWatchState = LocalWatchState.Idle;
                    break;

                default:

                    // Next state
                    _States.CurrentLocalWatchState = LocalWatchState.Idle;
                    break;
            }
        }
    }
}