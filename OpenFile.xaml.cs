/*
 * Copyright (C) 2018 Russell Brown
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Input;
using System.ComponentModel;
using Renci.SshNet;
using System.IO;

namespace LogTailer
{
    /// <summary>
    /// Interaction logic for OpenFile.xaml
    /// </summary>
    /// 

    // OpenFile - use a substitute OpenFileDialog GUI to enable remote file processing
    public partial class OpenFile : Window
    {

        // FileSelection. returns selection information to main app. in here we either
        // have a local files details or a remote one. Needs to implement INotifyPropertyChanged
        // so GUI fields update with new values
        public class FileSelection: INotifyPropertyChanged
        {
            public FileSelection()
            {
            }
            // getters/setters - set new value & fire property changed
            private string sSHKey;
            private string remoteFile;
            private string remoteUser;
            private string remoteHost;
            private string localFile;

            public string SSHKey { get { return sSHKey; } set { sSHKey = value;  OnPropertyChanged("SSHKey"); } }
            public string RemoteFile { get { return remoteFile; } set { remoteFile = value; OnPropertyChanged("RemoteFile"); } }
            public string RemoteUser { get { return remoteUser; } set { remoteUser = value; OnPropertyChanged("RemoteUser"); } }
            public string RemoteHost { get { return remoteHost; } set { remoteHost = value; OnPropertyChanged("RemoteHost"); } }
            public string LocalFile { get { return localFile; } set { localFile = value; OnPropertyChanged("LocalFile"); } }
            public Boolean useRemote = false;

            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
        public FileSelection Result { get; set; }

        // Create a file selection instance 
        public FileSelection gi = new FileSelection();

        public OpenFile()
        {
            Result = null;
            gi = new FileSelection();
            gi.SSHKey = Tailer.Properties.Settings.Default.lastSSHKey;
            gi.RemoteUser = Tailer.Properties.Settings.Default.lastUser;
            gi.RemoteHost = Tailer.Properties.Settings.Default.lastHost;
            gi.RemoteFile = Tailer.Properties.Settings.Default.lastRemoteFile;
            gi.LocalFile = Tailer.Properties.Settings.Default.lastFile;
            DataContext = gi;
            InitializeComponent();
        }

        // force update of source - lostfocus problem
        private static void MoveFocus()
        {
            UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
            if (elementWithFocus != null)
                elementWithFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }

        // saveGI. Save the new selection to properties 
        private void saveGI()
        {
            MoveFocus();// force update of source - lostfocus problem
            Tailer.Properties.Settings.Default.lastSSHKey = gi.SSHKey;
            Tailer.Properties.Settings.Default.lastUser = gi.RemoteUser;
            Tailer.Properties.Settings.Default.lastHost = gi.RemoteHost;
            Tailer.Properties.Settings.Default.lastRemoteFile = gi.RemoteFile;
            Tailer.Properties.Settings.Default.lastFile = gi.LocalFile;
            Tailer.Properties.Settings.Default.Save();
        }

        // SelBtn_Click. User is selecting a ssh ppk key file
        private void SelBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            // dlg.FileName = "Log"; // Default file name
            dlg.DefaultExt = "*.log"; // Default file extension
            dlg.Filter = "PPK Files|*.ppk|All Files|*.*"; // Filter files by extension
            dlg.CheckFileExists = true;

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                gi.SSHKey = dlg.FileName;
                
            }           
        }

        // SelBtnLocal_Click. User is selecting a new local file
        private void SelBtnLocal_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            // dlg.FileName = "Log"; // Default file name
            dlg.DefaultExt = "*.log"; // Default file extension
            dlg.Filter = "Log Files|*.log|All Files|*.*"; // Filter files by extension
            dlg.CheckFileExists = true;

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                gi.LocalFile = dlg.FileName;

            }
        }

        //TestBtn_Click. User wants to test ssh connectivity to their remote host...
        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ( !File.Exists(gi.SSHKey) )
                {
                    MessageBox.Show("SSH Key file dosnt exist", "Connection failed");
                    return;
                }
                PrivateKeyFile pkf = new PrivateKeyFile(gi.SSHKey);
                SshClient ssh = new SshClient(gi.RemoteHost, gi.RemoteUser, pkf);
                ssh.Connect();
                ssh.Disconnect();
                MessageBox.Show("Connection OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection failed" );
            }
        }


        //OKLocalBtn_Click. OK selectd from 'local' tab, save settings & close
        private void OKLocalBtn_Click(object sender, RoutedEventArgs e)
        {
            gi.useRemote = false;
            Result = gi;
            saveGI();
            this.Close();
        }

        //OKLocalBtn_Click. OK selectd from 'remote' tab, save settings & close
        private void OKRemoteBtn_Click(object sender, RoutedEventArgs e)
        {
            gi.useRemote = true;
            Result = gi;
            saveGI();
            this.Close();
        }

    }
}
