using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Diagnostics;

//Known Problems
//ReadToEnd() will crash the program. Used own reader.
//left to do:
//C# File Access -> For input, Logging and Saving.
//Add some better comments too...
namespace locationserver
{
    class MultiThreadNetCode
    {
        static bool testmode = true;
        //Server Set_up
        static int port = 43;
        static int MAX_THREADS = 120;
        //Server Data
        static string VERSION_NUMBER = "0.25";
        static int connectionID = 0;
        static List<Handler> Handlers = new List<Handler>();
        public static List<Handler> deadHandlers = new List<Handler>();
        public static logging globalLog;

        public static string log_path = "";
        public static string perm_path = "";

        static void Main(string[] args)
        {
            InitOptions(args);
            globalLog = new logging(log_path);
            //Creditation and Initialise Call.
            if (!testmode)
            {
                Console.Title = ("locationserver, multi-threaded (v" + VERSION_NUMBER + ")");
            }
            
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Location Look up Server (v{0})\n(c)AndyWM, 2015\nACW Project, University of Hull - 1415",VERSION_NUMBER);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("-----------------------------------------------");
            if (log_path != "")
            {
                Console.WriteLine("Sever is logging : " + log_path);
            }
            if (perm_path != "")
            {
                Console.WriteLine("Sever is using datastore : " + perm_path);
            }
            MultiThreadNetCode self = new MultiThreadNetCode();
            
            self.listeningLoop();
        }
        static void InitOptions(string[] args)
        {
            bool next_is_log = false;
            bool next_is_store = false;
            foreach (string arg in args)
            { 
                switch(arg)
                {
                    case "-l":
                        next_is_log = true;
                        continue;
                    case "-f":
                        next_is_store = true;
                        continue;
                }
                if (next_is_log == true)
                {
                    log_path = arg;
                    next_is_log = false;
                }
                if (next_is_store == true)
                {
                    perm_path = @arg;
                    next_is_store= false;
                }
            }
        }
        void listeningLoop()
        {
            TcpListener listener;
            int current=0;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Console.WriteLine("Pending Connections...\n");
                while (true)
                {
                        if (listener.Pending()) //anyone trying to connect?
                        {
                            //reuse dead handlers.
                            if (deadHandlers.Count == 0 && (Handlers.Count - deadHandlers.Count)<MAX_THREADS)
                            {
                                Handler localHandler = new Handler();
                                Handlers.Add(localHandler);
                                localHandler.setIdle(false);
                                //lock(this)
                                {
                                    localHandler.client = listener.AcceptTcpClient();//listener.AcceptSocket();
                                    Thread lT = new Thread(() => localHandler.Connection(connectionID++));
                                    current++;
                                    lT.Start();
                                    
                                }
                                Thread.Sleep(1); //give the thread time to clear the pending connection.
                            }
                            else
                            {
                                Handler localHandler = deadHandlers[0]; //get handle to first reuse. Remove from reuse list.
                                localHandler.setIdle(false); //set active
                                //lock (this)
                                {
                                    localHandler.client = listener.AcceptTcpClient();//listener.AcceptSocket();
                                    Thread lT = new Thread(() => localHandler.Connection(connectionID++)); //new thread.
                                    lT.Start();
                                    current++;
                                }
                                Thread.Sleep(1); //give the thread time to clear the pending connection.
                                localHandler = null;
                            }
                     }
                }
            }
            catch (Exception e)
            {

                if (log_path!="")
                {
                    MultiThreadNetCode.globalLog.log(e.ToString(), "0.0.0.0");
                }
            }
          
        }
    }

    /// <summary>
    /// Handler for each individual request, exists in it's own thread.
    /// </summary>
    class Handler
    {
        bool justSpawned=true; 
        bool idle = true; //is handler inactive.
        NetworkStream clientStream; 
        ProtocolParsing pParse;
        public TcpClient client=new TcpClient();


        public void Connection(int cID)
        {//Remember to close the connection...
            try
            {
                Console.WriteLine("+ Established : Connection <{0}>", cID); 
                clientStream = client.GetStream();

                byte[] msg = new byte[1024];
                int bytesCount;

                clientStream.ReadTimeout = 1000;
                clientStream.WriteTimeout = 1000;
                while (true)
                {
                    bytesCount = 0;

                    try
                    {
                        bytesCount = clientStream.Read(msg, 0, 1024);
                    }
                    catch
                    {
                        //Fault
                        break;
                    }

                    if (bytesCount > 0)
                    {
                        //can end loop after at least one byte read(or timed out)
                        break;
                    }
                }

                //message has successfully been received
                ASCIIEncoding encoder = new ASCIIEncoding();
                string buffermessage = encoder.GetString(msg, 0, bytesCount);
                string recieve = buffermessage;
                //recieve = recieve.TrimEnd('\0');
                recieve = recieve.TrimEnd(' ');
                Console.WriteLine("> Request (begin) : Connection <{0}>\n" + recieve + "\n> Request (ended) : Connection <{0}>", cID);
                

                //if pParse is new, make new one.
                if (pParse == null)
                    pParse = new ProtocolParsing();
                
                //Init parse with recieve
                pParse.Initialise(recieve);
                string send = pParse.formatForWrite();
                if (send != "don't")
                {
                    //Now Replying...
                    Console.WriteLine("< Reply   (begin) : Connection <{0}>\n" + @send + "\n< Reply   (ended) : Connection <{0}>", cID);
                    byte[] bSend = Encoding.GetEncoding("ASCII").GetBytes(send.ToCharArray());
                    
                    clientStream.Write(bSend,0,bSend.Length);
                    if (MultiThreadNetCode.log_path != "")
                    {
                        MultiThreadNetCode.globalLog.log(client.Client.RemoteEndPoint.ToString(), recieve, send);
                    }
                    clientStream.Flush();
                }
                Console.WriteLine("- Terminated : Connection <{0}>", cID);
            }
            catch (Exception e)
            {
                Console.WriteLine("! Failed : Connection <{0}>", cID);
                if (MultiThreadNetCode.log_path != "")
                {
                    MultiThreadNetCode.globalLog.log(e.ToString(), e.ToString());
                }
                setIdle(true);
            }
            finally
            {
                clientStream.Close();
                client.Close();
                setIdle(true);
            }
            clientStream = null;
            client = null;
            //thread dies after this line...
        
        }
        public void setIdle(bool newState)
        {
            lock(this) //now accessing handler state.
            {
                idle = newState;
                if (idle == true)
                {
                    MultiThreadNetCode.deadHandlers.Add(this);
                }
                else if (!justSpawned)
                {
                    MultiThreadNetCode.deadHandlers.Remove(this);
                }
                else
                {
                    justSpawned = false;
                }
            }
        }
    }
    /// <summary>
    /// File access and table look up + access concurreny control.
    /// </summary>
    /// 
    class ServerData
    {
        
        static Dictionary<string, string> look_up_table = new Dictionary<string, string>(); //Shared data
        public ServerData()
        {
            lock(this)
            { 
                //replace this with file access.
                look_up_table.Add("cssbct", "place2");
                look_up_table.Add("468827", "place1");

                if (MultiThreadNetCode.perm_path != "")
                {
                    try
                    {
                        StreamReader sr = new StreamReader(MultiThreadNetCode.perm_path);
                        //StreamReader sr2 = new StreamReader(@"Z:\log");
                        while (!sr.EndOfStream)
                        {
                            string item = sr.ReadLine();
                            string[] data = item.Split();
                            string _key = data[0];
                            string _rest = item.Substring(_key.Length+1);
                            if (!look_up_table.ContainsKey(_key))
                            {
                                look_up_table.Add(_key, _rest);
                            }
                        }
                        sr.Close();
                    }
                    catch
                    {
                        Console.Write("No such file to read from : " + MultiThreadNetCode.perm_path + "\n");
                    }
                }
            }
        }
        void Save()
        {
                StreamWriter sw = new StreamWriter(MultiThreadNetCode.perm_path);
                lock (this)
                {
                    foreach (string element in look_up_table.Keys)
                    {
                        string line = element + " " +  look_up_table[element];
                        sw.WriteLine(line);
                    }
                    sw.Close();
                }

        }
        
        public string getLocation(string key)
        {
            //if person exists, return there location.
            string answer;
            if (look_up_table.TryGetValue(key, out answer))
                return answer;
            else
                return null;
            
        }
        public bool setLocation(string key, string nLocation)
        {
            //if person exists, lock for to prevent other threads from currupting data
            //...then update the person's position. Returns pass or fail.
            lock (this)
            {
                string answer;
                if (look_up_table.TryGetValue(key, out answer) == true)
                {
                    look_up_table.Remove(key);
                    look_up_table.Add(key, nLocation);
                    if (MultiThreadNetCode.perm_path != "")
                    {
                        //throw (new Exception("OI"));
                        Save();
                    }
                    return true;
                }
                else
                {//add user 
                    if (!look_up_table.ContainsKey(key))
                    {
                        look_up_table.Add(key, nLocation);
                        if (MultiThreadNetCode.perm_path != "")
                        {
                            //throw(new Exception("OI"));
                            Save();
                        }
                        return true;
                    }
                }
                return false;
            }
        }
    }

    class logging
    {
        static string logPath;
        public logging(string path)
        {
            logPath = path;
        }
        public void log(string logMsg, string IP)
        {
            if (logPath != "")
            {
                logMsg = IP + " - - " + getDate() + " Error " + logMsg;
                writeFile(ref logPath, ref logMsg);
            }
        }
        public void log(string IP, string clR, string srR)
        {
            if (logPath != "")
            {
                string logMsg = IP + " - - " + getDate() + " \"" + clR + "\" " + srR;
                writeFile(ref logPath, ref logMsg);
            }
        }
        //These too...
        public void writeFile(ref string path, ref string data)
        {
            lock(this)
            {
                StreamWriter sw = new StreamWriter(path);
                sw.WriteLine(data);
                sw.Close();
            }
        }
        //public static void readFile();

        public static string getDate()
        {
            string mon = "";
            switch (DateTime.UtcNow.Month)
            {
                case 1:
                    mon = "Jan";
                    break;
                case 2:
                    mon = "Feb";
                    break;
                case 3:
                    mon = "Mar";
                    break;
                case 4:
                    mon = "Apr";
                    break;
                case 5:
                    mon = "May";
                    break;
                case 6:
                    mon = "Jun";
                    break;
                case 7:
                    mon = "Jul";
                    break;
                case 8:
                    mon = "Aug";
                    break;
                case 9:
                    mon = "Sep";
                    break;
                case 10:
                    mon = "Oct";
                    break;
                case 11:
                    mon = "Nov";
                    break;
                case 12:
                    mon = "Dec";
                    break;
            }
            string textualDate = "[" + DateTime.UtcNow.Day + "/" + mon + "/" + DateTime.UtcNow.Year + ":" + DateTime.UtcNow.ToLongTimeString() + "] +0000";
            return textualDate;
        }
    }
    /// <summary>
    /// Parses an input, .Initialise() must be called before this can be used.
    /// Read assumes good faith. Write will return don't if the request isn't valid.
    /// </summary>
    class ProtocolParsing 
    {
        public static ServerData sd = new ServerData(); //handle on shared data store
        //Structures.
        public enum pFlags
        {
            whois,
            h9,
            h0,
            h1,
        };
        public struct request
        {
            public string name;
            public string location;
            public bool nameOnly;
        };
        //Flags and request data entity.
        public pFlags Protocol = pFlags.whois; //set default protocol
        bool initialised = false; 
        public request rData;
        string analyse = "";
        bool error = false;

        public void Initialise(string an)
        {
            //Wipe Clean.
            resetCachedData();
            analyse = an;
            formatForRead();
            initialised = true; //data read and understood. 
        } 
        public string formatForWrite()
        {
            //Any formatted data?
            if (!initialised)
            {
                throw new Exception("Cannot format for write, formatForRead() hasn't been called, no input to format");
            }
            //Enact client's request. If failed, set error flag.
            if (!rData.nameOnly)
            {
                if (!sd.setLocation(rData.name, rData.location))
                {
                    error = true;
                }
            }
            else
            {
                rData.location = sd.getLocation(rData.name);
                if (rData.location == null)
                {
                    error = true;
                }
            }

            //Generate appropriate response to client.
            switch (Protocol)
            {
                case pFlags.whois:
                    //NAME REQUEST   : <location><CR><LF> [drops connection]
                    //CHANGE REQUEST : OK<CR><LF>[drops connection]
                    //ERROR REQUEST  : ERROR: no entries found<CR><LF> [drops connection]
                    if (error == true)
                    {
                        return "ERROR: no entries found\r\n";
                    }
                    return (
                         rData.nameOnly ?
                         rData.location + "\r\n"
                         :
                         "OK\r\n"
                         );
                case pFlags.h9:
                    //NAME REQUEST   : HTTP/0.9<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><CR><LF><location><CR><LF>[drops connection]
                    //CHANGE REQUEST : HTTP/0.9<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><CR><LF>[drops connection]
                    //ERROR REQUEST  : HTTP/0.9<space>404<space>Not<space>Found<CR><LF>Content-Type:<space>text/plain\r\n\r\n[drops connection]
                    if (error == true)
                    {
                        return "HTTP/0.9 404 Not Found\r\nContent-Type: text/plain\r\n\r\n";
                    }
                    return (
                        rData.nameOnly ?
                        ("HTTP/0.9 200 OK\r\nContent-Type: text/plain\r\n\r\n" + rData.location + "\r\n")
                        :
                        ("HTTP/0.9 200 OK\r\nContent-Type: text/plain\r\n\r\n")
                        );

                case pFlags.h0:
                    //NAME REQUEST   : HTTP/1.0<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><CR><LF><location><CR><LF> [drops connection]
                    //CHANGE REQUEST : HTTP/1.0<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><CR><LF> [drops connection]
                    //ERROR REQUEST  : HTTP/1.0<space>404<space>Not<space>Found<CR><LF>Content-Type:<space>text/plain<CR><LF><CR><LF>[drops connection]
                    if (error == true)
                    {
                        return "HTTP/1.0 404 Not Found\r\nContent-Type: text/plain\r\n\r\n";
                    }
                    return (
                        rData.nameOnly ?
                        ("HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\n\r\n" + rData.location + "\r\n")
                        :
                        ("HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\n\r\n")
                        );
                case pFlags.h1:
                    //NAME REQUEST   : HTTP/1.1<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><optional header lines><CR><LF><location><CR><LF>[drops connection]
                    //CHANGE REQUEST : HTTP/1.1<space>200<space>OK<CR><LF>Content-Type:<space>text/plain<CR><LF><optional header lines><CR><LF>[drops connection]
                    //ERROR REQUEST  : HTTP/1.1<space>404<space>Not<space>Found<CR><LF>Content-Type:<space>text/plain<CR><LF><optional header lines><CR><LF>[drops connection]
                    if (error == true)
                    {
                        return "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\n";
                    }
                    return (
                        rData.nameOnly ?
                        ("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n" + rData.location + "\r\n")
                        :
                        ("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n")
                        );

                default:
                    return "don't";
            }
        }
        public void formatForRead()
        {
            int lastPos;

            string conditional = analyse;
            if (conditional.Length >= 3)
            {
                conditional = analyse.Substring(0, 5);
            }
            else
            {
                conditional = "";
            }
            if (conditional == "GET /" || conditional == "PUT /")
            {   //pad to make all modes the same length.
                analyse = " " + analyse;
                conditional = analyse.Substring(0, 5); // GET /
            }
            if (analyse.Length > 6)
            {
                conditional = analyse.Substring(0, 6);

                if (conditional == " GET /")
                {//don't parse for location and ID HTTP version.
                    rData.nameOnly = true;
                    identHTTP();
                }

                if (conditional == "POST /" || conditional == " PUT /")
                {//prase for location and ID HTTP version.
                    rData.nameOnly = false;
                    identHTTP();
                    rData.location = readFromPosToChar(analyse, analyse.Length - 3, '\n', out lastPos, true); //ignore terminating \r\n. Grab the Sequence between that and the last \n.
                }
            }
            
            if(Protocol != pFlags.h0 && Protocol != pFlags.h1 && Protocol != pFlags.h9)
            {
                string what = analyse.Substring(analyse.Length - 2, 2);
                if (analyse.Substring(analyse.Length - 2, 2) == "\r\n")
                {
                    Protocol = pFlags.whois;
                    if (readFromPosToChar(analyse, 0, ' ', out lastPos) == "")
                    {
                        rData.nameOnly = true;
                    }

                    if (rData.nameOnly)
                    {
                        rData.name = analyse.Substring(0, analyse.Length - 2);
                    }
                    else
                    {
                        //set request info.
                        rData.name = readFromPosToChar(analyse, 0, ' ', out lastPos);
                        rData.location = readFromPosToChar(analyse, lastPos + 1, '\r', out lastPos);
                    }
                }
            }
        }
        void identHTTP()
        {
            int place;
            string possibleName = readFromPosToChar(analyse, 6, '\r', out place);
            if (possibleName.Contains(" HTTP/1"))
            {
                //then its 1 or 1.1
                rData.name = readFromPosToChar(analyse, 6, ' ', out place);
                string http = readFromPosToChar(analyse, place, '\r', out place);
                if (http == " HTTP/1.0")
                {
                    Protocol = pFlags.h0; //0.9?
                }
                else if (http == " HTTP/1.1")
                {
                    Protocol = pFlags.h1; //0.9?
                }
            }
            else
            {
                Protocol = pFlags.h9; //0.9?
                rData.name = possibleName;
            }
           
        }
        public void resetCachedData()
        {
            initialised = false;
            analyse = "";
            Protocol = pFlags.whois;
            rData.location = "";
            rData.name = "";
            rData.nameOnly = false;
            error = false;
        }
        #region sequenceLocators
        string readFromPosToChar(string str, int startPos, char searchChar, out int newStart)
        {
            bool charLocated = false;
            char[] cArray = str.ToCharArray();
            int cLoc = startPos;
            while (cArray[cLoc] != searchChar && cLoc < cArray.Length-1)
            {
                cLoc++;
            }
            if(cArray[cLoc] == searchChar)
            {
                    charLocated=true;
            }
            newStart = cLoc;
            return (charLocated? str.Substring(startPos, cLoc - startPos) : "");
        }
        string readFromPosToChar(string str, int startPos, char searchChar, out int newStart, bool inverted)
        {

            char[] cArray = str.ToCharArray();
            int cLoc = startPos;
            while (cArray[cLoc] != searchChar && cLoc > 0)
            {
                cLoc--;
            }
            newStart = cLoc;
            return str.Substring(cLoc+1, startPos-cLoc);
        }
        #endregion
    }
}

//~AndyWM 2015~
