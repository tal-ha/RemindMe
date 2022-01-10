using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace RemindMe
{
    public partial class MainFrm : Form
    {
        #region Fields
        
#if DEBUG
        private const double SNOOZE_TIME = 0.1;
#else
        private const double SNOOZE_TIME = 5;
#endif

        #endregion Fields

        #region Constructor

        public MainFrm()
        {
            InitializeComponent();
            this.ConfigureFileWatcher();
            this.RefreshFormLocation();
            this.Init();
        }

        #endregion Constructor

        #region Properties

        private List<Reminder> Reminders { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Initializes reminders list by parsing the xml file and starts the timer
        /// </summary>
        private void Init()
        {
            this.Reminders = this.GetRemindersFromXML();
            this.timer1.Interval = Constants.DefaultTimerInterval;
            this.timer1.Start();
        }

        /// <summary>
        /// Clears the reminders list and stops the timer. Optionally shows halt message on the form
        /// </summary>
        /// <param name="msg"></param>
        private void Halt(string msg = null)
        {
            this.Reminders.Clear();
            this.timer1.Stop();
            if (msg != null)
            {
                this.textBox1.Text = msg;
                this.ShowForm();
            }
        }

        /// <summary>
        /// Sets file watcher properties
        /// </summary>
        private void ConfigureFileWatcher()
        {
            this.fileSystemWatcher1.Filter = Constants.ReminderXmlFileName;
            this.fileSystemWatcher1.Path = Path.GetFullPath(Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Sets location of the form according to the screen size
        /// </summary>
        private void RefreshFormLocation()
        {
            Rectangle workingArea = Screen.GetWorkingArea(this);
            int newX = workingArea.Right - this.Size.Width;
            int newY = workingArea.Bottom - this.Size.Height;
            if (this.Location.X != newX || this.Location.Y != newY)
            {
                this.Location = new Point(newX, newY);
            }

#if DEBUG
            this.Log($"New Height: {workingArea.Height}, New Width: {workingArea.Width}");
            this.Log($"New X: {newX}, New Y: {newX}");
            this.Log($"Location X: {Location.X}, Location Y: {Location.Y}");
#endif
        }

        //protected override bool ShowWithoutActivation
        //{
        //    get
        //    {
        //        return true;
        //    }
        //}

        /// <summary>
        /// Reads reminders from the xml file
        /// </summary>
        /// <returns>List of reminders</returns>
        private List<Reminder> GetRemindersFromXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Reminder));

            XmlDocument xmlDoc = new XmlDocument();
            List<Reminder> remindersList = new List<Reminder>();
            try
            {
                if (!File.Exists(Constants.ReminderXmlFileName))
                {
                    throw new FileNotFoundException($"Cannot find file {Constants.ReminderXmlFileName}");
                }

                xmlDoc.Load(Constants.ReminderXmlFileName);
                XmlNode remindersRoot = xmlDoc.FirstChild;

                foreach (XmlNode reminder in remindersRoot.ChildNodes)
                {
                    if (!reminder.IsElementType())
                    {
                        continue;
                    }

                    Reminder r = new Reminder();
                    r.Title = reminder.Attributes.GetNamedItem("title")?.Value ?? Constants.NoTitle;
                    r.DateTimeList = new List<On>();

                    foreach (XmlNode on in reminder.ChildNodes)
                    {
                        if (!on.IsElementType())
                        {
                            continue;
                        }

                        string date = on.Attributes.GetNamedItem("date")?.Value;
                        string time = on.Attributes.GetNamedItem("time")?.Value;
                        string subtitle = on.Attributes.GetNamedItem("subtitle")?.Value;

                        DateTime dt = new DateTime();
                        if (string.IsNullOrEmpty(date))
                        {
                            dt = DateTime.Now.Date;
                        }
                        else
                        {
                            dt = DateTime.Parse(date);
                        }

                        TimeSpan ts = new TimeSpan();
                        if (string.IsNullOrEmpty(time))
                        {
                            ts = DateTime.Now.TimeOfDay;
                        }
                        else
                        {
                            ts = TimeSpan.Parse(time);
                        }
                        
                        r.DateTimeList.Add(new On { DtTime = dt + ts, Shown = false, SubTitle = subtitle });
                    }
                    remindersList.Add(r);
                }

            }
            catch (Exception ex)
            {
                throw new XmlException(ex.Message);
            }

            return remindersList;
        }

        /// <summary>
        /// Displays the form after adjusting location
        /// </summary>
        private void ShowForm()
        {
            this.RefreshFormLocation();
            this.Show();
        }

        /// <summary>
        /// Snoozes the current reminders by adding SNOOZE_TIME
        /// </summary>
        private void Snooze()
        {
            List<string> allRows = new List<string>(this.textBox1.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            List<string> currentReminders = new List<string>();
            foreach (string row in allRows.Where(r => Constants.ReminderLineIdentifiers.All(r.Contains)))
            {
                string[] timeArr = row.Split(new string[] { Constants.ReminderTextSplitter }, StringSplitOptions.RemoveEmptyEntries);
                if (timeArr.Length > 1)
                {
                    currentReminders.Add(timeArr[1].Trim());
                }
            }

            foreach (Reminder r in this.Reminders)
            {
                List<On> onList = r.DateTimeList.Where(o => currentReminders.Contains(o.DtTime.ToShortTimeString()) && o.Shown).ToList();
                foreach (On on in onList)
                {
                    on.SnoozedDtTime = DateTime.Now.AddMinutes(SNOOZE_TIME);
                    on.Shown = false;
                    on.Snoozed = true;
                }
            }

            textBox1.Text = Constants.ReminderSnoozedText;
            textBox1.Text += Environment.NewLine;
            this.Hide();
        }

        /// <summary>
        /// Logs given message on to the text box
        /// </summary>
        /// <param name="msg">Message to display</param>
        private void Log(string msg)
        {
            textBox1.Text += $"[LOG] {msg}{Environment.NewLine}";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            int remindersToShow = 0;
            foreach (Reminder r in this.Reminders)
            {
                // display regular and snoozed reminders
                IList<On> combinedOns = r.DateTimeList.Where(o => !o.Snoozed && !o.Shown && DateTime.Now >= o.DtTime)
                                .Concat(r.DateTimeList.Where(o => o.Snoozed && !o.Shown && DateTime.Now >= o.SnoozedDtTime))
                                .ToList();

                foreach (On on in combinedOns)
                {
                    textBox1.Text += string.Format(Constants.ReminderTextFormat, r.Title, on.SubTitle, on.DtTime.ToShortTimeString());
                    textBox1.Text += Environment.NewLine;
                    on.Shown = true;
                    on.Snoozed = false;
                    remindersToShow++;
                }
            }

            if (remindersToShow > 0)
            {
                this.timer1.Interval = Constants.DefaultTimerInterval;
                this.ShowForm();
            }
            else
            {
                //sleep till next reminder, will wake up if xml is modified
                //IList<On> ons = this.Reminders.SelectMany(x => x.DateTimeList).ToList();
                //ons = ons.OrderBy(x => x.SnoozedDtTime).OrderBy(x => x.DtTime).ToList();
                //On upComing = ons.FirstOrDefault();
                //if (upComing != null)
                //{
                //    TimeSpan ts = DateTime.Now.Subtract(upComing.DtTime);
                //    this.timer1.Interval = ts.Milliseconds;
                //}
            }
        }
        
        private void okBtn_Click(object sender, EventArgs e)
        {
            this.textBox1.Clear();
            this.Hide();
        }

        private void snoozeBtn_Click(object sender, EventArgs e)
        {
            this.Snooze();
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Snooze();
        }
        
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            this.textBox1.SelectionStart = this.textBox1.Text.Length;
            this.textBox1.ScrollToCaret();
        }
        
        private void fileSystemWatcher1_Changed(object sender, FileSystemEventArgs e)
        {
            this.Init();
        }

        private void fileSystemWatcher1_Created(object sender, FileSystemEventArgs e)
        {
            this.Init();
        }

        private void fileSystemWatcher1_Deleted(object sender, FileSystemEventArgs e)
        {
            string msg = $"{Constants.ReminderXmlFileName} is deleted. No reminders will be displayed.";
            this.Halt(msg);
        }

        private void fileSystemWatcher1_Renamed(object sender, RenamedEventArgs e)
        {
            if (e.OldName == Constants.ReminderXmlFileName && e.Name != Constants.ReminderXmlFileName)
            {
                string msg = $"{Constants.ReminderXmlFileName} is renamed. No reminders will be displayed.";
                this.Halt(msg);
            }
            else if (e.OldName != Constants.ReminderXmlFileName && e.Name == Constants.ReminderXmlFileName)
            {
                this.Init();
            }
        }
        
        #endregion Methods
    }

    #region DTOs

    public sealed class Reminder
    {
        #region Fields

        private string _title;

        #endregion Fields

        #region Properties

        public string Title
        {
            get { return this._title; }
            set { this._title = (value ?? string.Empty).Trim(); }
        }

        public List<On> DateTimeList { get; set; }

        #endregion Properties
    }

    public sealed class On
    {
        #region Fields

        private string _subTitle;

        #endregion Fields

        #region Properties

        public string SubTitle
        {
            get { return this._subTitle; }
            set { this._subTitle = (value ?? string.Empty).Trim(); }
        }
        
        public DateTime DtTime { get; set; }
        public bool Shown { get; set; }

        public DateTime SnoozedDtTime { get; set; }
        public bool Snoozed { get; set; }

        #endregion Properties
    }

    public static class Constants
    {
        public const string ReminderXmlFileName = "reminders.xml";
        public const string NoTitle = "No title";
        public const string ReminderTextChevronDblRight = ">>";
        public const string ReminderTextSplitter = "@";
        public const string ReminderTextFormat = ReminderTextChevronDblRight + " {0} ({1}) " + ReminderTextSplitter + " {2}"; // >> Title (SubTitle) @ DtTime
        public const string ReminderSnoozedText = ReminderTextChevronDblRight + " SNOOZED"; // >> SNOOZED
        public const int DefaultTimerInterval = 1000;
        public static readonly string[] ReminderLineIdentifiers = new string[]
        {
            Constants.ReminderTextChevronDblRight,
            Constants.ReminderTextSplitter
        };
    }

    public static class Helpers
    {
        public static bool IsElementType(this XmlNode node)
        {
            return node.NodeType == XmlNodeType.Element;
        }
    }

    #endregion DTOs
}
