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
    // Logging. Contains a number of logging styles defined in the
    // app.config file. eg:
    //  <LEVELFORMATS>
    //   <add key = "Log4" value="10,100,DEBUG,Debug,SlateGray,INFO,Info,Black,WARN,Warn,Orange,ERROR,Error,Red" />
    //   <add key = "Custom" value="34,41,Debug,Debug,Gray,Log,Log,Blue,Stat,Stat,Orange,Error,Error,Red" />
    //  </LEVELFORMATS>
    public class Logging
    {
        // singleton to access current log style from anywhere
        private static LogLevelStyle current = null;
        public static LogLevelStyle Get() { return current; }
        public static void Set(LogLevelStyle c) {  current=c; }

        // The LogStyle. 
        // each style contains a number of LogLevels, a name, and an
        // expected location range where the log level can be found in 
        // a line in the log file
        public class LogLevelStyle
        {
            public LogLevelStyle(string name, int start, int end)
            {
                this.Name = name;
                this.Start = start;
                this.End = end;
            }
            public int Start = 0;
            public int End = 50;
            public String Name;
            public List<LogLevel> levels = new List<LogLevel>();
            public override string ToString()
            {
                return Name;
            }
        }

        // LogLevel.
        // this defines a particular log level within a log style.
        public class LogLevel
        {
            public LogLevel(string t, string n, int l, string colour)
            {
                tag = t.ToLower(); 
                name = n;
                level = l;
                this.colour = colour;
            }
            public string tag;      // string to look for in log line to identify this level
            public string name;     // a name for this level
            public string colour;   // a colour to use in the gui for this level
            public int level;       // ordinal value for this level in relation to other levels
                                    // eg debug=0, info=1, warning=2 etc.
            public override String ToString() { return name;  }
        }

        // A named list of the LogLevelStyles
        public static SortedList<String, LogLevelStyle> allStyles = new SortedList<String, LogLevelStyle>();

        // Set. Used to set the 'current' level based on the style name
        public static void Set(String levelName)
        {
            if (allStyles.ContainsKey(levelName))
            {
                current = allStyles[levelName];
                return;
            }
            // oops, name dosn't exist, go for lowest level            
            current = allStyles.Values[0];
        }

        // Add. Adds a level style to our list of styles. We are passed an name
        // for the style and a csv from the app.config file to define the various
        // levels ( see LEVELFORMATS )
        public static void Add(String sname, String csvStr)
        {
            // split string into parts based on ','
            String[] parts = csvStr.Split(new char[] { ',' });

            // first two parts are the start & end locations for the text
            int p = 0;
            int start = Int32.Parse(parts[p++]);
            int end = Int32.Parse(parts[p++]);

            // create the style
            LogLevelStyle lls = new LogLevelStyle(sname, start, end);

            // add each level based on the csv string ( we assume these are
            // in ascending order and set 'level' appropriatly
            int level = 0; // start with level 0
            for ( p=2; p<parts.Length; p+=3)
            {
                string tag = parts[p];
                string name = parts[p+1];
                string colour = parts[p+2];
                LogLevel l = new LogLevel(tag, name, level++, colour);
                lls.levels.Add(l);
            }
            // add the style to the named list of styles
            allStyles.Add(sname, lls);
        }
    }
}
