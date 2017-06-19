using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Threading;
namespace location
{
    /// <summary>
    /// Net Code
    /// </summary>
    class NetInterface
    {
        //defaults
        public static int port = 43;
        public static string host = "localhost"; //"whois.net.dcs.hull.ac.uk"; //"localhost";

        private static IPAddress IpLookUp(string hostStr)
        {
            int throwAway;
            if(int.TryParse(hostStr.Substring(0,1), out throwAway))
            {
                IPAddress ip;
                if (IPAddress.TryParse(hostStr, out ip))
                {
                    return ip;
                }
            }
            else if (hostStr == "localhost")
            {
                IPHostEntry host;
                host = Dns.GetHostEntry(Dns.GetHostName());

                foreach (IPAddress ip_address in host.AddressList)
                {
                    if (ip_address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip_address;
                    }
                }
            }
            else
            {
                try
                {
                    IPAddress[] table_of_addresses = Dns.GetHostAddresses(hostStr);
                    return table_of_addresses[0];
                }
                catch (Exception e)
                {
                    Console.Write(e.ToString());
                }
            }

            return null;
        }

        static void Main(string[] args)
        {

            //string[] args = { "-h", "www.hull.ac.uk", "php/cssbct/08241/ACWtest.htm", "-h9", "-p", "80"}; //Test
            int c = args.Length;

            ProtocolParsing pParse = new ProtocolParsing(args);

            if (c == 0)
            {
                Console.WriteLine("No Arguments");
            }
            else
            {
                try
                {
                    //Open Connection, initialise stream writers/readers
                    TcpClient client = new TcpClient();
                    IPAddress ip = IpLookUp(host);
                    if (ip != null & port >= 0 && port <= 65535)
                    {
                        client.Connect(ip, port);
                        StreamWriter sw = new StreamWriter(client.GetStream());
                        StreamReader sr = new StreamReader(client.GetStream());
                        //Set Timeouts to avoid deadlock.
                        sr.BaseStream.ReadTimeout = 1000;
                        sw.BaseStream.WriteTimeout = 1000;

                        //Format User Input, if valid. Send it to server with appropriate protocol.
                        string send = pParse.formatForWrite(), response = "";
                        if (send != "don't" && send != "abort:help_set")
                        {
                            sw.Write(send);
                            sw.Flush();
                        }

                        //response = pParse.formatForRead(sr.ReadToEnd().Trim());
                       // response = sr.ReadToEnd(); //dooesn't work.

                        if (send != "don't" && send != "abort:help_set")
                        {
                           
                            
                            char[] buff = new char[1];
                            try
                            {
                                int readAttempts = 0;
                                //response = sr.ReadToEnd();
                                    while (readAttempts < 10)
                                    {
                                        buff[0] = '\a';
                                        sr.Read(buff, 0, 1);
                                        if (buff[0] != '\a')
                                        {
                                            response += new string(buff);
                                        }
                                        else
                                        {
                                            readAttempts++;
                                            Thread.Sleep(10);
                                            
                                        }
                                    }
                            }
                            catch { }
                            if (response != "")
                            {
                                Depad(ref response);
                                response = pParse.formatForRead(response);
                            }
       

                            Console.WriteLine(response == "" ? "Timed Out, or Invalid Request" : response);
                        }
                        if (send == "don't")
                        {
                            Console.WriteLine("Timed Out, or Invalid Request");
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Host Name/Port is not Valid or Look Up Failed.");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Connection Error, or Help Used");
                }
            }
        }
        static void Depad(ref string pad)
        {
            pad = pad.TrimEnd(' ');
        }
        
    }
    /// <summary>
    /// Protocols
    /// </summary>
    class ProtocolParsing
    {
        bool help = false;
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
        }
        public request rData;
        public pFlags Protocol = pFlags.whois; //set default protocol

        public ProtocolParsing(string[] args)
        {
            bool pF = false, hF = false; //Port Flag (changed), Host Flag (changed)
            bool skipNext = false; //skip next data item (as it belongs to the last flag)

            List<string> RemainingData = new List<string>(); //Data that's left once flags are removed.
            foreach (string arg in args)
            {
                if ((arg == "" || arg == " " || arg == "\0") && (RemainingData.Count == 1 || RemainingData.Count ==2))
                {
                    System.Environment.Exit(1);
                }
                if (skipNext != true)
                {
                    switch (arg)
                    {
                        case "-help":
                            help = true;
                            break;
                        case "-h9":
                            Protocol = pFlags.h9;
                            break;
                        case "-h0":
                            Protocol = pFlags.h0;
                            break;
                        case "-h1":
                            Protocol = pFlags.h1;
                            break;
                        case "-h":
                            skipNext = true;
                            hF = true;
                            break;
                        case "-p":
                            skipNext = true;
                            pF = true;
                            break;
                        default:
                            RemainingData.Add(arg);
                            break;
                    }
                }
                else
                {
                    if (hF == true) //Set new host.
                    {
                        NetInterface.host = arg;
                        hF = false;
                    }
                    if (pF == true) //Set new port
                    {
                        int newPort;
                        NetInterface.port = int.TryParse(arg, out newPort) ? newPort : 43;
                        pF = false;
                    }
                    skipNext = false;
                }
                
            }
            rData.nameOnly = true;
            if (RemainingData.Count != 0 && help == false)
            {
                rData.name = RemainingData[0];
                if (RemainingData.Count > 1)
                {

                    rData.location = RemainingData[1];
                    rData.nameOnly = false;
                }
            }
            else
            {
                help = true;
            }
            if (help == true && RemainingData.Count==0)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("(c)AndyWM, 2015. ACW University of Hull - 1415\n\n");
                Console.Write("--------------------------------------------\n");
                Console.Write("location is a location look_up client\n\n");
                Console.Write("USAGE: \n");
                Console.Write("  1. location <name> [location] [-h9] [-h0] [-h1] [-h host] [-p port]\n");
                Console.Write("  2. location <-help>\n\n");
                Console.Write("  <name> : look up name on server" );
                Console.Write("  [location] : updates <name>'s location\n");
                Console.Write("  [-h9] : send via HTTP0.9 Protocol\n");
                Console.Write("  [-h0] : send via HTTP1.0 Protocol\n");
                Console.Write("  [-h1] : send via HTTP1.0 Protocol\n");
                Console.Write("  [-h host] : send to host\n");
                Console.Write("  [-p port] : send to port at host\n");
                Console.Write("  <-help> : displays help\n");
                Console.Write("  If multiple https are set, the last set will be used\n");
                Console.Write("  If set, [location] MUST follow <name>\n");
                Console.ForegroundColor = ConsoleColor.Gray;
            }    
        }

        public string formatForWrite()
        {
            if (help == true)
            {
                return "abort:help_set";
            }
            switch (Protocol)
            {
                case pFlags.whois:
                    // <name><cr><lf>
                    // <name><space><cr><lf>
                    return (
                         rData.nameOnly ?
                         rData.name + "\r\n"
                         :
                         rData.name + " " + rData.location + "\r\n"
                         );
                case pFlags.h9:
                    return (
                        rData.nameOnly ?
                        "GET /" + rData.name + "\r\n"
                        :
                        "PUT /" + rData.name + "\r\n\r\n" + rData.location + "\r\n"
                        );

                case pFlags.h0:
                    //GET<space>/<name><space>HTTP/1.0<CR><LF><optional header lines><CR><LF>
                    //POST<space>/<name><space>HTTP/1.0<CR><LF>Content-Length:<space><length><CR><LF><optional header lines><CR><LF><location><CR><LF>
                    return (
                    rData.nameOnly ?
                    ("GET /" + rData.name + " HTTP/1.0\r\n" + "" + "\r\n")
                    :
                    ("POST /" + rData.name + " HTTP/1.0\r\nContent-Length: " + rData.location.Length + "\r\n" + "" + "\r\n" + rData.location + "\r\n")
                    );
                case pFlags.h1:
                    //GET<space>/<name><space>HTTP/1.1<CR><LF>Host:<space><hostname><CR><LF><optional header lines><CR><LF>
                    //POST<space>/<name><space>HTTP/1.1<CR><LF>Host:<space><hostname><CR><LF>Content-Length:<space><length><CR><LF><optional header lines><CR><LF><location><CR><LF>
                    return ( //Dns.GetHostName()
                        rData.nameOnly ?
                        ("GET /" + rData.name + " HTTP/1.1\r\nHost: " + NetInterface.host + "\r\n" + "" + "\r\n")
                        :
                        ("POST /" + rData.name + " HTTP/1.1\r\nHost: " + NetInterface.host + "\r\n" + "Content-Length: " + rData.location.Length + "\r\n" + "" + "\r\n" + rData.location + "\r\n")
                        );

                default:
                    return "don't";
            }
        }

        public string formatForRead(string analyse)
        {
            string response = "";
                //IS HTML OR WHOIS?S
                //DO HTML
                // CHECK OKAY
                     //IF OKAY, DID WE REQUEST NAME?
                //DO WHOIS
                // CHECK OKAY
                     //IF OKAY, DID WE REQUEST NAME?
            if (analyse.Substring(0, 4) == "HTTP")
            {
                //HTTP CHECKS
                if (CheckOkay(analyse))
                {
                    if (rData.nameOnly) //we sent name only, therefore expect a location back.
                    {
                        response = rData.name + " is " + harvastLocation(analyse) + "\r\n";
                    }
                    else
                    {
                        response = rData.name + " location changed to be " + rData.location + "\r\n";
                    }
                }
                else if (Failed(analyse))
                {
                    response = "404 Not Found";
                }
            }
            else
            {         
                //WHOIS CHECKS
                if (analyse == "ERROR: no entries found\r\n")
                {
                    response = "ERROR: no entries found\r\n";
                }
                else if ((analyse == "OK\r\n"))
                {
                    response = rData.name + " location changed to be " + rData.location + "\r\n";
                }
                else
                {
                    response = (rData.name + " is " + analyse).TrimEnd(' '); ;
                }          
            }
            if (rData.nameOnly)
            {
                if (rData.name == "")
                {
                    return "";
                }
            }
            else
            {
                if (rData.name == "" || rData.location == "")
                {
                    return "";
                }
            }
            return response;
        }
        public string harvastLocation(string analyse)
        {
            int loc;
            string answer = readFromPosToChar(analyse, analyse.Length - 3, '\n', out loc, true);
            return (answer == "" ? "" : answer);
        }
        #region checks
        bool CheckOkay(string analyse)
        {
            int start = 8;
            string check = readFromPosToChar(analyse, start, '\r', out start);
            if (check == " 200 OK") //is return of request
            {
                return true;
            }
            return false;
        }
        bool Failed(string analyse)
        {
            int start = 8;
            string check = readFromPosToChar(analyse, start, '\r', out start);
            if (check == " 404 Not Found") //is return of request
            {
                return true;
            }
            return false;
        }
        #endregion
        #region sequenceLocators
        string readFromPosToChar(string str, int startPos, char searchChar, out int newStart)
        {
            bool charLocated = false;
            char[] cArray = str.ToCharArray();
            int cLoc = startPos;
            while (cArray[cLoc] != searchChar && cLoc < cArray.Length - 1)
            {
                cLoc++;
            }
            if (cArray[cLoc] == searchChar)
            {
                charLocated = true;
            }
            newStart = cLoc;
            return (charLocated ? str.Substring(startPos, cLoc - startPos) : "");
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
            return str.Substring(cLoc + 1, startPos - cLoc);
        }
        #endregion
    }
}

//~AndyWM 2015~
