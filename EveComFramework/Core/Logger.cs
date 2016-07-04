﻿#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;


namespace EveComFramework.Core
{
    /// <summary>
    /// This class manages a richtextbox for you, to use as a console output
    /// </summary>
    public class LoggerHelper
    {
        #region Instantiation

        static LoggerHelper _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static LoggerHelper Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new LoggerHelper();
                }
                return _Instance;
            }
        }

        private LoggerHelper()
        {
            CurrentBackColor = BackColor1;
        }

        #endregion

        /// <summary>
        /// A list of the Loggers currently available
        /// </summary>
        public List<Logger> Loggers = new List<Logger>();
        /// <summary>
        /// The primary back color to use (Default: Black)
        /// </summary>
        public Color BackColor1 = Color.Black;
        /// <summary>
        /// The secondary back color to use (Default: Dark gray)
        /// </summary>
        public Color BackColor2 = Color.FromArgb(50, 50, 50);
        /// <summary>
        /// The default color for text (Default: White)
        /// </summary>
        public Color DefaultForegroundColor = Color.White;
        internal Color CurrentBackColor;

        internal static DateTime RoundDown(DateTime time)
        {
            return time.Subtract(
                new TimeSpan(0, 0, 0, time.Second, time.Millisecond));
        }

        string LastUpdate = RoundDown(DateTime.Now).AddMinutes(-1).ToString("HH:mm");
        /// <summary>
        /// Use this method to update your richtextbox
        /// </summary>
        /// <param name="Console">Your richtextbox</param>
        /// <param name="Module">The name of the module the update is from</param>
        /// <param name="Message">The log message</param>
        public void RichTextboxUpdater(RichTextBox Console, string Module, string Message)
        {
            if (RoundDown(DateTime.Now).ToString("HH:mm") != LastUpdate)
            {
                Console.SelectionBackColor = Color.DarkBlue;
                LastUpdate = RoundDown(DateTime.Now).ToString("HH:mm");
                Console.AppendText(String.Format("{0}", LastUpdate));
                Console.AppendText(new string(' ', 1000) + Environment.NewLine);
            }
            if (Console.WordWrap) Console.WordWrap = false;
            Console.SelectionStart = Console.TextLength;
            Console.SelectionColor = DefaultForegroundColor;
            Console.SelectionBackColor = CurrentBackColor;
            CurrentBackColor = (CurrentBackColor == BackColor1)?BackColor2:BackColor1;
            Console.AppendText(String.Format("{0}", Module.PadRight(12)));
            Queue<char> StringReader = new Queue<char>(Message);
            while (StringReader.Any())
            {
                char a = StringReader.Dequeue();
                if (a == '|')
                {
                    if (StringReader.Peek() == '-')
                    {
                        StringReader.Dequeue();
                        char darkcolor = StringReader.Dequeue();
                        switch (darkcolor)
                        {
                            case 'w':
                                Console.SelectionColor = Color.Gray;
                                break;
                            case 'r':
                                Console.SelectionColor = Color.DarkRed;
                                break;
                            case 'b':
                                Console.SelectionColor = Color.DarkBlue;
                                break;
                            case 'o':
                                Console.SelectionColor = Color.DarkOrange;
                                break;
                            case 'y':
                                Console.SelectionColor = Color.Goldenrod;
                                break;
                            case 'g':
                                Console.SelectionColor = Color.ForestGreen;
                                break;
                        }
                        continue;
                    }
                    char color = StringReader.Dequeue();
                    switch (color)
                    {
                        case 'w':
                            Console.SelectionColor = Color.White;
                            break;
                        case 'r':
                            Console.SelectionColor = Color.Red;
                            break;
                        case 'b':
                            Console.SelectionColor = Color.Blue;
                            break;
                        case 'o':
                            Console.SelectionColor = Color.Orange;
                            break;
                        case 'y':
                            Console.SelectionColor = Color.Yellow;
                            break;
                        case 'g':
                            Console.SelectionColor = Color.Green;
                            break;
                    }
                    continue;
                }
                Console.AppendText(a.ToString());
            }
            Console.AppendText(new string(' ', 1000) + Environment.NewLine);
            if (Console.Lines.Length > 100)
            {
                Console.Select(0, Console.GetFirstCharIndexFromLine(Console.Lines.Length - 50));
                Console.SelectedText = string.Empty;
            }
            Console.SelectionStart = Console.TextLength;
            Console.ScrollToCaret();
        }

    }

    public enum LogType
    {
        INFO,
        FATAL,
        ERROR,
        WARN,
        DEBUG
    }

    /// <summary>
    /// Handles logging and feedback, allows multiple events to collect feedback info
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Name">The name of the module this will log for</param>
        public Logger(string Name)
        {
            this.Name = Name;
            LoggerHelper.Instance.Loggers.Add(this);
        }

        /// <summary>
        /// The name of the module this is logging for
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Delegate for log events
        /// </summary>
        /// <param name="Module">The module sending the message</param>
        /// <param name="Message">Message that is being logged</param>
        public delegate void LogEvent(string Module, string Message);
        /// <summary>
        /// Delegate for console events
        /// </summary>
        /// <param name="Module">The module sending the message</param>
        /// <param name="LogLevel">LogLevel for the message</param>
        /// <param name="Message">Message that is being logged</param>
        public delegate void ConsoleLogEvent(string Module, LogType LogLevel, string Message);
        /// <summary>
        /// Event using LogEvent delegate
        /// </summary>
        public event LogEvent Event;
        /// <summary>
        /// Send a log event
        /// </summary>
        /// <param name="Message">Message, may contain {0} type tokens like string.Format</param>
        /// <param name="Params">Paramters to insert into the message format string</param>
        public void Log(string Message, params object[] Params)
        {
            LogType type = Params.OfType<LogType>().FirstOrDefault();
            Params = Params.Where(a => !(a is LogType)).ToArray();

            if (type == LogType.INFO)
            {
                RichEvent?.Invoke(Name, string.Format(Message, Params));
                Event?.Invoke(Name, string.Format(Regex.Replace(Message, "\\|.", string.Empty), Params));
                ConsoleEvent?.Invoke(Name, type, string.Format(Regex.Replace(Message, "\\|.", string.Empty), Params));
            }
            Diagnostics.Instance.Post(string.Format(Regex.Replace(Message, "\\|.", string.Empty), Params), type, Name);
        }
        /// <summary>
        /// Delegate for rich log events
        /// </summary>
        /// <param name="Module">The module sending the message</param>
        /// <param name="Message">Message that is being logged</param>
        public delegate void RichLogEvent(string Module, string Message);
        /// <summary>
        /// Event using RichLogEvent delegate
        /// </summary>
        public event RichLogEvent RichEvent;

        /// <summary>
        /// Event using ConsoleLogEvent delegate
        /// </summary>
        public event ConsoleLogEvent ConsoleEvent;

    }
}
