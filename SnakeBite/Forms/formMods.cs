﻿using ICSharpCode.SharpZipLib.Zip;
using SnakeBite.Forms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SnakeBite
{
    public partial class formMods : Form
    {
        private formProgress progWindow = new formProgress();
        private int countCheckedMods = 0;
        private SettingsManager manager = new SettingsManager(ModManager.GameDir);

        public formMods()
        {
            InitializeComponent();
        }

        private delegate void GoToModListDelegate();

        private void checkBoxMarkAll_Click(object sender, EventArgs e)
        {
            checkBoxMarkAll.CheckState = CheckState.Checked; // keep checked aesthetic. using _Click avoids infinite recursion.
            bool isAllChecked = true; // assume all are checked

            for (int i = 0; i < listInstalledMods.Items.Count; i++)
            {
                if (listInstalledMods.GetItemCheckState(i) == CheckState.Unchecked)
                {
                    isAllChecked = false;
                    listInstalledMods.SetItemCheckState(i, CheckState.Checked);
                }
            }
            if (isAllChecked == true) // if still true after the first loop, all boxes are checked. Second loop will uncheck all boxes.
            {
                for (int i = 0; i < listInstalledMods.Items.Count; i++)
                {
                    listInstalledMods.SetItemCheckState(i, CheckState.Unchecked);
                }
            }
        }

        private void buttonInstall_Click(object sender, EventArgs e)//todo
        {
            // Show open file dialog for mod file
            OpenFileDialog openModFile = new OpenFileDialog();
            List<string> ModNames = new List<string>();

            openModFile.Filter = "MGSV Mod Files|*.mgsv|All Files|*.*";
            openModFile.Multiselect = true;
            DialogResult ofdResult = openModFile.ShowDialog();
            if (ofdResult != DialogResult.OK) return;
            foreach (string filename in openModFile.FileNames)
                ModNames.Add(filename);

            formInstallOrder installer = new formInstallOrder();
            installer.ShowDialog(ModNames);
            RefreshInstalledMods();

            listInstalledMods.SelectedIndex = listInstalledMods.Items.Count - 1;
        }

        private void buttonUninstall_Click(object sender, EventArgs e) //todo
        {
            // Get selected mod indices and names
            CheckedListBox.CheckedIndexCollection checkedModIndices = listInstalledMods.CheckedIndices;
            CheckedListBox.CheckedItemCollection checkedModItems = listInstalledMods.CheckedItems;
            string markedModNames = "";

            foreach (object mod in checkedModItems)
            {
                markedModNames += "\n" + mod.ToString();
            }
            if (!(MessageBox.Show("The following mods will be uninstalled:\n" + markedModNames , "SnakeBite", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)) return;

            ProcessUninstallMod(checkedModIndices); // Morbid: To uninstall multiple mods at once, the method will now pass a collection of indices rather than a single modEntry.

            // Update installed mod list
            RefreshInstalledMods(true);
        } 

        private void buttonOpenLogs_Click(object sender, EventArgs e)
        {
            Process.Start(Debug.LOG_FILE_PREV);
            Process.Start(Debug.LOG_FILE);
        }

        private void labelModWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            
            var mods = manager.GetInstalledMods();
            ModEntry selectedMod = mods[listInstalledMods.SelectedIndex];
            try
            {
                Process.Start(selectedMod.Website);
            }
            catch { }
        }

        private void formMain_Load(object sender, EventArgs e)
        {

            // Refresh button state
            RefreshInstalledMods(true);

            // Show form before continuing
            this.Show();
            
        }

        private void listInstalledMods_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Populate mod details pane
            if (listInstalledMods.SelectedIndex >= 0)
            {
                var mods = manager.GetInstalledMods();
                ModEntry selectedMod = mods[listInstalledMods.SelectedIndex];
               labelModName.Text = selectedMod.Name;
               labelModAuthor.Text = "By " + selectedMod.Author;
               labelModWebsite.Text = selectedMod.Version;
               textDescription.Text = selectedMod.Description;
            }
        }

        private void listInstalledMods_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked)
            {
                countCheckedMods++;
                buttonUninstall.Enabled = true;
            }
            else
            {
                countCheckedMods--;
                if (countCheckedMods == 0)
                    buttonUninstall.Enabled = false;
            }

        }

        internal void ProcessInstallMod(string installFile, bool skipCleanup)
        { // command line install.
            var metaData = Tools.ReadMetaData(installFile);
            if (metaData == null) return;
            List<string> InstallFileList = new List<string>();
            InstallFileList.Add(installFile);

            if (!ModManager.CheckConflicts(installFile)) return;

            ProgressWindow.Show("Installing Mod", String.Format("Installing {0}...", metaData.Name), new Action((MethodInvoker)delegate { ModManager.InstallMod(InstallFileList, skipCleanup); }));

            this.Invoke((MethodInvoker)delegate { RefreshInstalledMods(); });
        }

        public void ProcessUninstallMod(CheckedListBox.CheckedIndexCollection modIndices)
        {
            ProgressWindow.Show("Uninstalling Mod(s)", "Uninstalling...\n\nNote: The uninstall time depends greatly\nonthe size and number of mods being uninstalled,\nas well as the mods that are still installed.", new Action((MethodInvoker)delegate { ModManager.UninstallMod(modIndices); }));
        }

        public void ProcessUninstallMod(ModEntry mod)
        { 
            // command line uninstall. This method only checks the mod it was passed, and puts it in a 1-item list to be uninstalled.
            for (int i = 0; i < listInstalledMods.Items.Count; i++)
            {
                listInstalledMods.SetItemCheckState(i, CheckState.Unchecked);
            }
            var mods = manager.GetInstalledMods();
            listInstalledMods.SetItemCheckState(mods.IndexOf(mod), CheckState.Checked);
            CheckedListBox.CheckedIndexCollection checkedModIndex = listInstalledMods.CheckedIndices;
            ProgressWindow.Show("Uninstalling Mod", "Uninstalling...", new Action((MethodInvoker)delegate { ModManager.UninstallMod(checkedModIndex); }));
        }

        private void RefreshInstalledMods(bool resetSelection = false)
        {
            var mods = manager.GetInstalledMods();
            listInstalledMods.Items.Clear();
            countCheckedMods = 0;
            buttonUninstall.Enabled = false;

            if (mods.Count > 0)
            {
                groupBoxNoModsNotice.Visible = false;
                panelModDescription.Visible = true;

                foreach (ModEntry mod in mods)
                {
                    listInstalledMods.Items.Add(mod.Name);
                }

                if (resetSelection)
                {
                    if (listInstalledMods.Items.Count > 0)
                    {
                        listInstalledMods.SelectedIndex = 0;
                    }
                    else
                    {
                        listInstalledMods.SelectedIndex = -1;
                    }
                }
            }
            else
            {

                groupBoxNoModsNotice.Visible = true;
                panelModDescription.Visible = false;
            }
        }

        private void showProgressWindow(string Text = "Processing...")
        {
            this.Invoke((MethodInvoker)delegate
            {
                progWindow.Owner = this;
                progWindow.StatusText.Text = Text;

                progWindow.ShowInTaskbar = false;
                progWindow.Show();
                this.Enabled = false;
            });
        }

        private void linkLabelSnakeBiteModsList_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.nexusmods.com/metalgearsolidvtpp/mods/searchresults/?src_order=7&src_sort=0&src_view=1&src_tab=1&src_language=0&src_descr=SBWM&src_showadult=1&ignoreCF=0&page=1&pUp=1"); 
        }

        private void buttonLaunchGame_Click(object sender, EventArgs e)
        {
            Process.Start("steam://run/287700/");
            Application.Exit();
        }

    }
}
