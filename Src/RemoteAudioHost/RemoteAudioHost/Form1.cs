﻿using NAudio.Wave;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.Threading;
using System.Runtime.Remoting.Channels;
using static RemoteAudioHost.Form1;
using static System.Windows.Forms.DataFormats;
using System.Text;

namespace RemoteAudioHost
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        private static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        private static uint CurrentResolution = 0;
        private static bool running = false;
        private static string audioport, localip;
        private static WasapiLoopbackCapture waveIn = null;
        public static byte[] rawdataavailable = null, raw = null;
        public static Action<bool> action = null;
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            if (System.IO.File.Exists("tempsave"))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader("tempsave"))
                {
                    textBox1.Text = file.ReadLine();
                    textBox2.Text = file.ReadLine();
                }
            }
            GetAudioByteArray();
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
            System.Threading.Thread.Sleep(300);
            using (System.IO.StreamWriter createdfile = new System.IO.StreamWriter("tempsave"))
            {
                createdfile.WriteLine(textBox1.Text);
                createdfile.WriteLine(textBox2.Text);
            }
            waveIn.Dispose();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                button1.Text = "Stop";
                running = true;
                localip = textBox1.Text;
                audioport = textBox2.Text;
                Task.Run(() => LSPAudio.Connect());
            }
            else
            {
                button1.Text = "Start";
                running = false;
                System.Threading.Thread.Sleep(100);
                Task.Run(() => LSPAudio.Disconnect());
            }
        }
        private void GetAudioByteArray()
        {
            waveIn = new WasapiLoopbackCapture();
            waveIn.DataAvailable += waveIn_DataAvailable;
            waveIn.StartRecording();
        }
        public void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            raw = e.Buffer;
            Array.Resize(ref raw, e.BytesRecorded);
            rawdataavailable = raw;
        }
        public class LSPAudio
        {
            private static string localip;
            private static string port;
            private static WebSocketServer wss;
            public static void Connect()
            {
                try
                {
                    localip = Form1.localip;
                    port = Form1.audioport;
                    String connectionString = "ws://" + localip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Audio>("/Audio");
                    wss.Start();
                }
                catch { }
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/Audio");
                wss.Stop();
            }
        }
        public class Audio : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (rawdataavailable != null)
                        SendAsync(rawdataavailable, Form1.action);
                    rawdataavailable = null;
                    Thread.Sleep(1);
                }
            }
        }
    }
}