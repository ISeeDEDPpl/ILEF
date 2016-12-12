#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace ILEF.Core
{
    public class Diagnostics
    {
        #region Instantiation

        static Diagnostics _Instance;
        public static Diagnostics Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Diagnostics();
                }
                return _Instance;
            }
        }

        private Diagnostics()
        {
            LogDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\logs\\";

            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            if (file == null)
            {
                file = LogDirectory + "evecom" + Process.GetCurrentProcess().Id + "-" + DateTime.Now.Ticks + ".txt";
            }
            if (isLogFile == null)
            {
                isLogFile = LogDirectory.Replace("\\", "\\\\") + "innerspace" + Process.GetCurrentProcess().Id + "-" + DateTime.Now.Ticks + ".txt";
                //LavishScript.ExecuteCommand("log \""+isLogFile+"\"");
            }

            StreamWriter oWriter = new StreamWriter(file, true);
            oWriter.Write("Diagnostics log started: {0}" + Environment.NewLine + Environment.NewLine, DateTime.Now);
            oWriter.Close();

        }

        public List<State> States = new List<State>();

        #endregion

        public string file { get; set; }
        public string isLogFile { get; set; }
        public string LogDirectory { get; set; }

        public void Post(string message, LogType logtype, string Module="")
        {
            StreamWriter oWriter = new StreamWriter(file, true);
            oWriter.Write("{0}\t{1}\t{2}: {3}"+Environment.NewLine, DateTime.Now.ToString("HH:mm"), logtype, Module, message);
            oWriter.Close();
        }

        public bool Upload(string uploadFile)
        {
            return Stats.Stats.Instance.UploadLog(uploadFile);
        }
    }
}
