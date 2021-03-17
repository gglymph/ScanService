using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using NTwain;
using Newtonsoft.Json;
using System.Net;
using System.Threading;


namespace ScanService
{
    class Program
    {
        private static IWebSocketConnection _socket;
        public static void Main()
        {
            var server = new WebSocketServer("ws://127.0.0.1:8181");
            server.Start(socket =>
            {
                socket.OnOpen = () => Console.WriteLine("Open!");
                socket.OnClose = () => Console.WriteLine("Close!");
                socket.OnMessage = message =>
                {
                    if (message == "devices")
                        socket.Send(GetDevices());
                    else
                        socket.Send(ScanDocument(message));
                };
                _socket = socket;
            });
            while(true) { }
        }

        private static string ScanDocument(string message)
        {
            var scanRequest = JsonConvert.DeserializeObject<ScanRequest>(message);
            Console.WriteLine("Scanning Document.....");
            var rc = twain.Open();
            if (rc == NTwain.Data.ReturnCode.Success)
            {
                var hit = twain.SingleOrDefault(p => p.Id == scanRequest.DeviceId);
                if (hit == null)
                {
                    Console.WriteLine("This source was not found");
                    twain.Close();
                    return ReturnStatus.DeviceNotFound.ToString();
                }
                else
                {
                    rc = hit.Open();
                    if (rc == NTwain.Data.ReturnCode.Success)
                    {
                        if (scanRequest.PixelType == "gray")
                            hit.Capabilities.ICapPixelType.SetValue(NTwain.Data.PixelType.Gray);
                        else
                            hit.Capabilities.ICapPixelType.SetValue(NTwain.Data.PixelType.RGB);
                        
                        if (hit.Capabilities.CapAutoFeed.IsSupported)
                            hit.Capabilities.CapAutoFeed.SetValue(scanRequest.AutoFeed ? NTwain.Data.BoolType.True : NTwain.Data.BoolType.False);
                        if (hit.Capabilities.CapDuplexEnabled.IsSupported)
                            hit.Capabilities.CapDuplexEnabled.SetValue(scanRequest.Duplex ? NTwain.Data.BoolType.True : NTwain.Data.BoolType.False);
                        Console.WriteLine("Starting capture from device {0}", scanRequest.DeviceId);
                        rc = hit.Enable(scanRequest.ShowUI ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI, false, IntPtr.Zero);
                        
                    }
                }
            }
            return rc.ToString();
        }

        private static string GetDevices()
        {
            Console.WriteLine("Getting TWAIN Sources.....");
            Thread.Sleep(1000);
            var rc = twain.Open();
            if (rc == NTwain.Data.ReturnCode.Success)
            {
                var devicesJson = JsonConvert.SerializeObject(twain.Select(p => new { Name = p.Name, Id = p.Id, Capabilities = p.Capabilities }));
                twain.Close();
                return devicesJson;
            }
            twain.Close();
            return ReturnStatus.CouldNotConnect.ToString();
        }

        static readonly TwainSession twain = InitTwain();

        private static TwainSession InitTwain()
        {
            var twain = new TwainSession(NTwain.Data.TWIdentity.CreateFromAssembly(NTwain.Data.DataGroups.Image, System.Reflection.Assembly.GetExecutingAssembly()));
            twain.TransferReady += (s, e) =>
                Console.WriteLine("Got xfer ready on thread {0}.", Thread.CurrentThread.ManagedThreadId);

            twain.DataTransferred += DataTransferred;

            twain.SourceDisabled += (s, e) =>
            {
                Console.WriteLine("Source disable on thread {0}.", Thread.CurrentThread.ManagedThreadId);
                var rc = twain.CurrentSource.Close();
                rc = twain.Close();
            };
            return twain;
        }

        private static void DataTransferred(object sender, DataTransferredEventArgs e)
        {
            if (e.NativeData != IntPtr.Zero)
            {
                Console.WriteLine("SUCCESS! Got twain data on thread {0}.", Thread.CurrentThread.ManagedThreadId);
                var stream = e.GetNativeImageStream();
                if (stream != null)
                {
                    byte[] bytes;
                    using (var ms = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    string base64 = Convert.ToBase64String(bytes);
                    _socket.Send($"data:image/jpeg;base64,{base64}");
                }
                
            }
            else
                Console.WriteLine("FAILURE! No twain data on thread {0}.", Thread.CurrentThread.ManagedThreadId);

            
        }
    }
}
