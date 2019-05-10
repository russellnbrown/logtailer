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
using System.IO;
using System.Threading;

namespace LogTailer
{
    // Reader. implements the BaseReader interfaqce for a local file
   public class Reader : BaseReader
    {
        private Thread rThread = null;          // the thread doing the reading
        private StreamReader logFileFile = null;     // the file of the log
        private long lastPos = 0;               // last read position
        private bool catchUp = true;            // indicates if activly reading ( or waiting )

        // Constructor. record file name & start a thread to read the file
        public Reader( string fileName)
        {
            this.fileName = fileName;
            rThread = new Thread(new ThreadStart(read));
            rThread.Start();
        }

        // Close. set sunning to false to stop reader thread & close file
        public override void Close()
        {
            running = false;
            if ( logFileFile != null )
                logFileFile.Close();
        }

        // ReStart. stop reading thread and close file, then reopen and
        // create a new reading thread
        public override void ReStart()
        {
            running = false;
            System.Threading.Thread.Sleep(50);
            logFileFile.Close();
            rThread.Join();
            rThread = new Thread(new ThreadStart(read));
            rThread.Start();
        }

        // read. The reading thread
        private void read() 
        {
            running = true;

            // we will keep running until main window tells us to stop
            while (running)
            {
                try
                {
                    // open the file & reset line queue
                    logFileFile = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    string line = "";
                    lineCount = 0;
                    restarted = true;
                    lines.Clear();

                    // loop until close indicated
                    while (running)
                    {
                        try
                        {
                            // read the line from the file
                            line = logFileFile.ReadLine();

                            // if null nothing has been written since last read of file has been re-created
                            if (line == null)
                            {
                                // set reading to false as we have caught up with end of file
                                catchUp = false;
                                state = lineCount.ToString() + " lines" + (catchUp ? " (reading)" : " (waiting)");

                                // if length of file is < aor last read position then the file has
                                // been re-created, clear the line queue & reset our read position to 0
                                if (logFileFile.BaseStream.Length < lastPos)
                                {
                                    lastPos = 0;
                                    logFileFile.BaseStream.Position = 0;
                                    lock (lines)
                                    {
                                        lines.Clear();
                                        // update status
                                        restarted = true;
                                        state = "Restart detected";
                                        EnqueueLine("Restart detected", true);
                                        // now we will go back to top and start reading from begining 
                                    }
                                }
                                else // nothing to read, record position and try again after a little nap
                                {
                                    lastPos = logFileFile.BaseStream.Length;
                                    Thread.Sleep(10);
                                }

                            }
                            else
                            {
                                // line read OK, put it into the lines queue & set state
                                EnqueueLine(line, false);
                                state = lineCount.ToString() + " lines" + (catchUp ? " (reading)" : " (waiting)");
                            }
                        }
                        catch (Exception e)
                        {
                            state = "Error occured reading file" + e.Message;
                            EnqueueLine(state, true);
                        }


                    }
                    EnqueueLine("reader has stopper", true);
                    // running was unset which means we need to close
                }
                // The file dosn't exist. set state & write dummy line to make it obvious
                catch (FileNotFoundException nfe)
                {
                    state = "File not found, waiting";
                    EnqueueLine(state, true);
                    // wait a second b4 trying again
                    System.Threading.Thread.Sleep(1000);
                }
            }

        }

  

    }
}

