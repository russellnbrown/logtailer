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

using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Threading;

namespace LogTailer
{
    public class RemoteReader : BaseReader
    {

        // tailing a remote file is a tad more complicated as we cant open the file directly
        // instead we use a remote command 'tail -1000f <remote file name> and read the output
        // of that command. This is obviously assums we are talking to a unix system. 
        // TBD  - put commands into config file & make it more generic

        // To workout when a file has been re-opened we also need to be able to see the file size
        // as if this happens the tail command keeps running but returns no more data. to do this we
        // need to run a second command, ls -l <remote file name>

        // each command runs in a seperate thread. we use a memory stream to retreive data from
        // the command...

        // the tail command uses:
        private MemoryStream output = new MemoryStream();
        private Thread tailThread = null;
        private String tailCmd = ""; // see config file

        // the size command uses:
        private Thread sizeThread = null;
        private MemoryStream info = new MemoryStream();
        private String sizeCmd = ""; // see config file

        // SshClient is used to run these commands
        private SshClient ssh;
        private IAsyncResult asynch;
        private String keyFilePath;
        private String host="";
        private String user="";
        private String pass="";

        // private AutoResetEvent connected;
        // tailing is flag to stop tail thread only
        private bool tailing = true;
        // exists is flag to indicate if the file exists yet


        // RemoteReader. save connection settings and read commands from config. 
        // then start the size thread. tail thread will be started when size thread is up
        public RemoteReader(String host, String user, String path, String key, String pass)
        {
            fileName = path;
            keyFilePath = key;
            this.host = host;
            this.user = user;
            this.pass = pass;
            state = "State";
            stateAlt = "AltState";
           // connected = new AutoResetEvent(false);

            // the commands to get size & tail
            NameValueCollection tsection = ConfigurationManager.GetSection("SETTINGS") as NameValueCollection;
            sizeCmd = tsection["sizeCmd"];
            tailCmd = tsection["tailCmd"];

            // Connect to host
            Connect();

            // start the size thread
            sizeThread = new Thread(new ThreadStart(SizeRun));
            sizeThread.Start();

        }

        // Close. Set flahs to stop threads & wait for them to stop
        public override void Close()
        {
            tailing = false;
            running = false;
            if (sizeThread != null)
                sizeThread.Join();
            if (tailThread != null)
                tailThread.Join();
        }

 
        // TailRun. The tailing thread. runs a 'tail' command on the host and processes lines read
        public void TailRun()
        {
            StreamReader streamReader = null;
            tailing = true;

            // replace FILE marker in command with the actual file name
            string cmd = tailCmd.Replace("FILE", fileName);

            try
            {
                while (tailing && running)
                {
                    // create the tail command & start it. run it asynchronusly so we can read
                    // anc check for stop flags
                    SshCommand sshCommand = ssh.CreateCommand(cmd);
                    asynch = sshCommand.BeginExecute();
                    streamReader = new StreamReader(sshCommand.OutputStream);

                    // read anything that is returned and add to lines queue
                    while (!asynch.IsCompleted && running && tailing)
                    {
                        bool reading = false;
                        string line = streamReader.ReadLine();
                        if (line != null)
                        {
                            reading = true;
                            EnqueueLine(line, false);
                        }
                        else
                        {
                            // if nothing read, just wait a bit befor
                            System.Threading.Thread.Sleep(100);
                        }
                        state = lineCount.ToString() + " lines" + (reading ? " (reading)" : " (waiting)");
                    }
                }
            }
            catch (Exception e)
            {
                running = false;
                EnqueueLine(e.Message + ": Can't connect to " + host, true);
                throw e;
            }
            
            try
            {
                streamReader.Close();
            }
            catch (Exception e)
            {
                running = false;
                EnqueueLine(e.Message + ": Can't connect to " + host, true);
            }

        }

        // StopTail. Stops the tail thread only
        public void StopTail()
        {
            tailing = false;
            EnqueueLine("Stopping tail", true);
            tailThread.Join();
            tailThread = null;
        }

        // StartTail. Starts the tail thread only
        public void StartTail()
        {
            tailing = true;
            EnqueueLine("Starting tail",true);
            tailThread = new Thread(new ThreadStart(TailRun));
            tailThread.Start();
        }

        // Connect. Connects to the host using ssh key/user
        public bool Connect()
        {
            try
            {
                Console.WriteLine("Connecting ....");


                List<AuthenticationMethod> paml = new List<AuthenticationMethod>();
                if (pass != null && pass.Length > 0)
                    paml.Add(new PasswordAuthenticationMethod(user, pass));
                if (keyFilePath != null && keyFilePath.Length > 0)
                    paml.Add(new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile[] { new PrivateKeyFile(keyFilePath, "") }));

                if (paml.Count < 1)
                {
                    running = false;
                    EnqueueLine("No remote access methods defines", true);
                    return false;
                }

                ConnectionInfo conn = new ConnectionInfo(host, 22, user, paml.ToArray());

                ssh = new SshClient(conn);
                ssh.Connect();
            }
            catch (Exception e)
            {
                running = false;
                EnqueueLine("Failed to connect, " + e.Message, true);
            }
            return true;
        }

        // SizeRun. this is a seperate thread to the tail thread. we run an appropriate command to get the size
        // of the file being tailed. If we detect the file has shrunk, we bounce the tail thread to start at 
        // the begining again. We first check if the file exists and if not wait for it to appear before continuing.
        public void SizeRun()
        {
            string cmd = sizeCmd.Replace("FILE", fileName);
            bool warned = false;
            bool exists = false;
            Int64 lastLength = 0;

            try
            {
                while (running)
                {
                    // Run the command and await the result
                    SshCommand sshCommand = ssh.CreateCommand(cmd);
                    string result = sshCommand.Execute();
                    result = result.TrimEnd();

                    // Get the size of the file, if line is empty the file dosnt exist set length to -1
                    Int64 length = -1;
                    if (result.Length > 0)
                    {
                        try
                        {
                            length = Int64.Parse(result);
                        }
                        catch (Exception e)
                        {
                            length = -1;
                        }
                    }

                    // if length < 0 then file dosnt exist, if it did exist previously then stop everything, otherwise
                    // just warn and try again later
                    if ( length < 0 )
                    {
                        Console.WriteLine("not there");
                        if (exists)
                        {
                            EnqueueLine("File " + fileName + " dosn't exist", true);
                            exists = false;
                            StopTail();
                        }
                        else
                        {
                            if (!warned)
                            {
                                warned = true;
                                EnqueueLine("File " + fileName + " dosn't exist, wait for creation...", true);
                            }
                        }

                    }
                    else
                    {
                        // file has a size. if it previously didnt exist ( which is default at start ) then
                        // write a message & start the tail thread
                        if (!exists) // new file
                        {
                            EnqueueLine("Opening " + fileName, true);
                            exists = true;
                            StartTail();
                        }
                        else
                        {
                            // if file has shrunk since last check, bounce the tail thread to get it to start
                            // from the begining
                            if (length < lastLength) // restarted file
                            {
                                EnqueueLine("File size is smaller, reopen from start " + fileName, true);
                                StopTail();
                                StartTail();
                            }
                            // otherwise everything is tickety boo
                            // record last position
                            lastLength = length;
                        }
                    }
                    // Wait a couple of seconds before re-testing but do it in a loop so we can 
                    // check running state at the same time
                    for(int d=0; d<200 && running; d++)
                        System.Threading.Thread.Sleep(100);
                }

            }

            catch (Exception e)
            {
                running = false;
                EnqueueLine("Error connecting:"+e.Message, true);
                throw e;
            }
            
        }

        public override void ReStart()
        {
            StopTail();
            StartTail();
        }

    }
}
