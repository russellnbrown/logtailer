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
using System.Collections.Generic;

namespace LogTailer
{
    // base Reader class
    // implementation is either 'Reader' for local files or
    // 'RemoteReader' for remote files
    public abstract class BaseReader
    {

        // the following is common to all readers
        protected string fileName = "";                        // file name
        protected List<FileLine> lines = new List<FileLine>(); // the log lines
        protected Boolean restarted = false;                   // bool to indicate if file is restarted
        protected string state = "";                           // a status string for the reader
        protected string stateAlt = "";                        // supplemental status
        protected bool running = true;                         // when un-set caused reading threds to stop
        protected int lineCount = 1;                           // number of lines in file



        // acessors for above
        public string File { get { return fileName; } }
        public string State { get { return state; } }
        public string StateAlt { get { return stateAlt; } }
        public List<FileLine> Lines { get { return lines; } }
        // returns restarted state resetting in the process
        public bool Restarted { get { bool rv = restarted; restarted = false; return rv; } } 

        // these must be implemented by reader implementation
        public abstract void Close();
        public abstract void ReStart();

        // common methods
        public void EnqueueLine(String m, bool status)
        {
            FileLine curTag;
            if (!status)
            {
                lineCount++;
                curTag = FileLine.Parse(m, lineCount);
            }
            else
            {
                curTag = FileLine.Parse(m, 0);
            }

            if (curTag != null)
                lock (lines)
                    lines.Add(curTag);
        }


    }
}
