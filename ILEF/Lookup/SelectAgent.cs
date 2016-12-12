namespace ILEF.Lookup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml.Linq;
    using ILEF.Logging;

    public class AgentsList
    {
        private sealed class AgentsDeclineTimers
        {
            private static readonly Lazy<AgentsDeclineTimers> lazy = new Lazy<AgentsDeclineTimers>(() => new AgentsDeclineTimers());
            public static AgentsDeclineTimers Instance { get { return lazy.Value; } }

            private Dictionary<string,DateTime> _timers;
            private string _agentMissionDeclineTimesFilePath;

            private AgentsDeclineTimers()
            {
                _agentMissionDeclineTimesFilePath = Logging.SessionDataCachePath + "agents__mission_decline_times.csv";

                _loadFromCacheFile();
            }

            private void _loadFromCacheFile()
            {
                try
                {
                    _timers = new Dictionary<string, DateTime>();

                    if (File.Exists(_agentMissionDeclineTimesFilePath))
                    {
                        Logging.Log("AgentsDeclineTimes", String.Format("Loading agents decline times from cache file : {0}", _agentMissionDeclineTimesFilePath), Logging.White);

                        System.IO.StreamReader file = new System.IO.StreamReader(_agentMissionDeclineTimesFilePath);

                        string line;
                        // Read and display lines from the file until the end of
                        // the file is reached.
                        while ((line = file.ReadLine()) != null)
                        {
                            string[] lineValues = line.Split(';');

                            string agentName = lineValues[0];
                            DateTime declineTime = new DateTime(long.Parse(lineValues[1]));

                            _timers.Add(agentName, declineTime);

                            Logging.Log("AgentsDeclineTimes", String.Format("Found agent decline time in cache file : {0}, {1}", agentName, declineTime.ToString()), Logging.White);
                        }
                    }
                    else
                    {
                        Logging.Log("AgentsDeclineTimes", String.Format("No decline times loaded because cache file does not exist : {0}", _agentMissionDeclineTimesFilePath), Logging.White);
                    }
                }
                catch (Exception exception)
                {
                    Logging.Log("AgentsDeclineTimes", "Exception [" + exception + "]", Logging.Teal);
                }
            }

            private void _writeToCacheFile()
            {
                try
                {
                    StreamWriter file = null;
                    Logging.Log("AgentsDeclineTimes", String.Format("Writing agents decline times to cache file : {0}", _agentMissionDeclineTimesFilePath), Logging.White);

                    try
                    {
                        file = new StreamWriter(_agentMissionDeclineTimesFilePath);
                        if (file != null)
                        {
                            foreach (var entry in _timers)
                            {
                                file.WriteLine("{0};{1};{2};{3}", entry.Key, entry.Value.Ticks, entry.Value.ToString(), DateTime.UtcNow.ToString());
                            }
                        }
                    }
                    catch (IOException)
                    {
                        //Logging.Log("AgentsDeclineTimes", "IOException: [" + ex + "]", Logging.Debug);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("AgentsDeclineTimes", "Exception: [" + ex + "]", Logging.Debug);

                    }
                    finally
                    {
                        if (file != null)
                        {
                            file.Flush();
                            file.Close();
                            file.Dispose();
                        }
                    }

                }
                catch (Exception exception)
                {
                    Logging.Log("AgentsDeclineTimes", "Exception [" + exception + "]", Logging.Teal);
                }
            }

            public DateTime getDeclineTimer(string agentName)
            {
                DateTime declineTimer;
                if(!_timers.TryGetValue(agentName, out declineTimer))
                {
                    declineTimer = DateTime.UtcNow;
                }

                return declineTimer;
            }

            public void setDeclineTimer(string agentName, DateTime declineTimer)
            {
                if (_timers.ContainsKey(agentName))
                    _timers[agentName] = declineTimer;
                else
                    _timers.Add(agentName, declineTimer);

                _writeToCacheFile();
            }
        }

        public AgentsList()
        {
        }

        public AgentsList(XElement agentList)
        {
            Name = (string)agentList.Attribute("name") ?? "";
            Priorit = (int)agentList.Attribute("priority");
        }

        public string Name { get; private set; }

        public int Priorit { get; private set; }

        public DateTime DeclineTimer
        {
            get
            {
                return AgentsDeclineTimers.Instance.getDeclineTimer(Name);
            }
            set
            {
                AgentsDeclineTimers.Instance.setDeclineTimer(Name, value);
            }
        }
    }
}