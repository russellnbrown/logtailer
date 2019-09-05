
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
using static LogTailer.Logging;

namespace LogTailer
{
    // FileLine
    //
    // Used to store information about one line in the logfile.
    // 
    // Provides acessors for logtailerwindow 
    // Provides a parser to return a FileLine if it is valid.
    //

    public class FileLine  
    {
        // Information about the line: text, line number & logging level
        // all private to force use of acessors
        private LogLevel level;
        private string text;
        private int number;

        // The acessors
        public String Number { get { return number.ToString(); } }
        public String Text { get { return text; } }
        public String Level { get { return level.ToString(); } }
        public LogLevel GetLevel() { return level; }
        public String ForegroundColor { get { return level.colour; } }

        // Constructor. make private to force use of 'Parse'
        private FileLine(string line, int number, LogLevel level)
        {
            this.text = line;
            this.number = number;
            this.level = level;
        }

        // Parse. parse the log line. If valid returns a 'FileLine'
        // otherwise null.
        public static FileLine Parse(string text, int number)
        {
            // Set the log level to the lowest level ( typically 'debug' )
            // if we can't determin the level from the text in the line
            // this will be the default
            LogLevel level = Logging.Get().levels[0];

            // if no text in line, nothing we can do, dont bother creating 
            // a FileLine, just return a null.
            if (text == null || text.Length == 0)
                return null;
 
            // if line is long enough, extract the section where we expect to 
            // find the logging level text
            string testLevel="";
            if (text.Length > Logging.Get().End)
            {
                testLevel = text.Substring(Logging.Get().Start, Logging.Get().End - Logging.Get().Start).ToLower();
                // see if we can find a level string in the log line extract.
                // if not it will remain at lowest level
                foreach (var l in Logging.Get().levels)
                    if (testLevel.Contains(l.tag))
                        level = l;
            }

            // create and return a file level
            return new FileLine(text, number, level);

        }
    }
}
