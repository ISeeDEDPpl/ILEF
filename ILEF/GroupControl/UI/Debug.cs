﻿#pragma warning disable 1591
using System;
using System.Windows.Forms;

namespace EveComFramework.GroupControl.UI
{
    [Obsolete("Please stop using this. It is no longer supported and will be nuked with CCPs upcoming november release.")]
    public partial class Debug : Form
    {
        public Debug()
        {
            InitializeComponent();
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            foreach (ActiveMember x in EveComFramework.GroupControl.GroupControl.Instance.CurrentGroup.ActiveMembers)
            {
                ListViewItem y = new ListViewItem();
                y.Text = x.CharacterName;
                y.SubItems.Add(x.Available.ToString());
                y.SubItems.Add(x.Active.ToString());
                y.SubItems.Add(x.InFleet.ToString());
                y.SubItems.Add(x.LeadershipValue.ToString());
                y.SubItems.Add(x.Role.ToString());
                listView1.Items.Add(y);
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

    }
}
