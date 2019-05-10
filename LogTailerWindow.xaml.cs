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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Text.RegularExpressions;

using static LogTailer.Logging;

namespace LogTailer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class TailerWindow : Window
    {
        // 'lines' is a list of log lines in our file. this is an observablelist so the acessors in 'FileList' 
        // map to a particular column. A basic ObservableList can only be added to one line at a time
        // which creates quite an overhead so we will use an 'ObservableRangeCollection' which 
        // allows adding of a range. See ObservableRangeCollection.cs for credits.
        public ObservableRangeCollection<FileLine> lines = new ObservableRangeCollection<FileLine>();

        // tailing. if true, keep the current line at the bottom when an update arrives
        public bool tailing = true;

        // StateMessage. Used to display an appropriate message in the 'Status' box on the GUI
        private string StateMessage = "";

        // reader. The reader provides updates from the log file. These updates get processed in 
        // 'onTimer' 
        private BaseReader reader = null;

        // our selected logging level
        private LogLevel currentLevel = null;

        // 'rex' an optional filter to display only lines matching this reg exp.
        private Regex rex = null;

        // levelFilter. A test used by the CollectonView to determin if a line should be shown 
        // based on current display level & text filter
        private bool levelFilter(object item)
        {
            FileLine fl = (FileLine)item;
            bool passLevel = fl.GetLevel().level >= currentLevel.level;
            bool passTextFilter = true;
            if (rex != null)
                passTextFilter = rex.IsMatch(fl.Text);
            return passTextFilter && passLevel;
        }

        // filterChanged. called if the filter changes in someway, refresh the display
        private void filterChanged()
        {
            CollectionViewSource.GetDefaultView(listView.ItemsSource).Refresh();
        }

        // TailerWindow. Main class.
        public TailerWindow()
        {
            InitializeComponent();

            // Set up the logging schemes based on the LEVELFORMATS config items in app.config
            NameValueCollection tsection = ConfigurationManager.GetSection("LEVELFORMATS") as NameValueCollection;
            foreach (string key in tsection)
            {
                string attr = tsection[key];
                Logging.Add(key, attr);
            }
 
            // If a previous style was selected, reload it. 
            Logging.Set(Properties.Settings.Default.lastLogStyle);            

            // Set a handler for window closing ( stops threads etc )
            Closing += OnWindowClosing;

            // set the 'Tail' GUI checkbox
            scrollCB.IsChecked = tailing;

            // add logging styles to the 'Style' pulldown
            foreach (LogLevelStyle lls in Logging.allStyles.Values)
                levelSelectionCombo.Items.Add(lls);
            levelSelectionCombo.SelectedItem = Logging.Get();

            // add levels in the loging style to the 'Level' pulldown
            foreach (LogLevel ll in Logging.Get().levels)
                levelCombo.Items.Add(ll);
            levelCombo.SelectedItem = Logging.Get().levels[0];
            currentLevel = (LogLevel)levelCombo.SelectedItem;

            // set the source for the lines observable collection
            listView.ItemsSource = lines;
            // set up a filter for the lines using the collectionview
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
            view.Filter = levelFilter;

            // set the text reg exp filter if set
            textFilterTB.Text = Properties.Settings.Default.lastTextFilter;
            SetTextFilter();


            // Get the file to be tailed....
            OnOpenNewFileClicked(null, null);
            if ( reader == null )
            {
                this.Close();
                System.Environment.Exit(0);
            }

            // set up a timer to process reader updates
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += onTimer;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            dispatcherTimer.Start();

        }

        // onTimer.  The reader thread is putting any updates onto its 'Lines' queue, here
        // we will check the queue and add any new lines to the 'lines' observable collection
        // which will result in a GUI update. 
        private void onTimer(object sender, EventArgs e)
        {
            // no reader ? nowt to do...
            if (reader == null)
                return;
            
            // lock the readers lines queue while we grab the new items from it
            lock(reader.Lines)
            {
                // if the reader restarted, we clear all our lines as they are
                // no longer valid
                if (reader.Restarted)
                {
                    lines.Clear();
                    Console.WriteLine("Detected a restart of logfile ");
                }

                // copy readers lines to our list & clear readers queue
                if (reader.Lines.Count > 0 )
                {
                    lines.AddRange(reader.Lines);
                    Console.WriteLine("Added " + reader.Lines.Count + " lines");
                    reader.Lines.Clear();
                    // update file status text box with new line count etc
                    SetFileState();
                }
            }

            // if we are tailing, move the last line read to bottom of GUI
            if (tailing)
                updateScroll();

            
        }

        // OnWindowClosing. Catch the close event & stop reader thread
        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if ( reader != null )
                reader.Close();
            Environment.Exit(0);
        }

        // scrollCB_*. Set tailing to the state of the check
        private void scrollCB_Unchecked(object sender, RoutedEventArgs e)
        {
            tailing = scrollCB.IsChecked.Value;
        }
        private void scrollCB_Checked(object sender, RoutedEventArgs e)
        {
            tailing = scrollCB.IsChecked.Value;
            updateScroll();
        }

        // updateScroll. ensure the last line is at the bottom of the listView grid
        // ( used when tailing )
        private void updateScroll()
        {
            listView.SelectedIndex = listView.Items.Count - 1;
            listView.ScrollIntoView(listView.SelectedItem);
        }

        // OnOpenNewFileClicked. User wants a new file or program started. Display
        // the open file dialog & get a file to look at ( may be local or remote ) 
        private void OnOpenNewFileClicked(object sender, RoutedEventArgs e)
        {
            OpenFile s = new OpenFile();
            s.ShowDialog();
            if (s.Result != null)
            {
                // something new selected, get rid of anything we currently use
                if (reader != null)
                {
                    reader.Close();
                    lines.Clear();
                }

                // If a remote file, create a remote reader with specified key file/user
                if (s.Result.useRemote)
                {
                    reader = new RemoteReader( s.Result.RemoteHost,s.Result.RemoteUser, s.Result.RemoteFile, s.Result.SSHKey);
                    SetFileText(s.Result.RemoteFile,s.Result.RemoteHost);
                }
                else // otherwise it is a local file. 
                {  
                    reader = new Reader( Properties.Settings.Default.lastFile); // RemoteReader();// DReader();// Reader(Properties.Settings.Default.lastFile);
                    SetFileText();

                }

                // Change title to indicate new file
                this.Title = "LogTailer " + StateMessage;

                return;
            }
 
        }

        // SetFileText. Update the file text box & tool tip from the reader
        private void SetFileText()
        {
            fileName.Text = Path.GetFileName(reader.File);
            fileName.ToolTip = reader.File;
        }

        // SetFileText. Update the file text box & tool tip explicitly
        private void SetFileText(String remote, String remoteTT)
        {
            fileName.Text = remote;
            fileName.ToolTip = remoteTT;
        }

        // SetFileState. Update file state text box
        private void SetFileState()
        {
            fileState.Text = reader.State;
            fileState.ToolTip = reader.StateAlt;
        }

        //levelCombo_DropDownClosed. A new log level is selected
        private void levelCombo_DropDownClosed(object sender, EventArgs e)
        {
            // record new level
            currentLevel = (LogLevel)levelCombo.SelectedValue;
            // and call filterChanged to refresh GUI with new selection
            filterChanged();
        }

        // listView_PreviewMouseWheel. Called when user used the scroll wheel, clear tailing so user 
        // can do whatever it is they are doing without scrolling back to bottom
        private void listView_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            tailing = false;
            scrollCB.IsChecked = false;
        }

        // levelSelectionCombo_DropDownClosed. A new logging style is seleted
        private void levelSelectionCombo_DropDownClosed(object sender, EventArgs e)
        {
            // tell logging the new level
            Logging.Set((LogLevelStyle)levelSelectionCombo.SelectedValue);

            // clear & reload the new logging levels into pulldown
            levelCombo.Items.Clear();
            foreach (LogLevel ll in Logging.Get().levels)
                levelCombo.Items.Add(ll);
            // select the first level by default
            levelCombo.SelectedItem = Logging.Get().levels[0];
            currentLevel = (LogLevel)levelCombo.SelectedItem;
            // save new selection to properties
            Properties.Settings.Default.lastLogStyle = Logging.Get().Name;
            Properties.Settings.Default.Save();
            // reset filter to new values & recreate the GUI from scratch
            filterChanged();
            lines.Clear();
            reader.ReStart();
        }

        // SetTextFilter. User has set a new text filter, update our rex for the filter
        // and recreate the GUI with new filter
        private void SetTextFilter()
        {
            if (Properties.Settings.Default.lastTextFilter != null && Properties.Settings.Default.lastTextFilter.Length > 0)
            {
                rex = new Regex(Properties.Settings.Default.lastTextFilter);
            }
            else
                rex = null;
            filterChanged();
        }

        // textFilterTB_KeyDown. User is seting a new text filter
        private void textFilterTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // if return is pressed, user is finished
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // put text filter textbox back to normal colour
                textFilterTB.Foreground = System.Windows.Media.Brushes.Black;
                // save new value
                Properties.Settings.Default.lastTextFilter = textFilterTB.GetLineText(0);
                Properties.Settings.Default.Save();
                // and get line filter to use new text filter
                SetTextFilter();
            }
            else // user has started to enter new test or is continuing, change
                 // text box colour to orange so they know they have started
                textFilterTB.Foreground = System.Windows.Media.Brushes.DarkOrange;

        }


    }

   
}
