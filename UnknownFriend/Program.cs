using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Corale.Colore;
using Corale.Colore.Razer.Effects;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace UnknownFriend
{

    class Program
    {

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private static System.Timers.Timer cameraTimer;
        private static System.Timers.Timer colorTimer;

        public static ColorCore currentColor;
        public static ColorCore targetColor;

        private static FilterInfoCollection videoDevices; // list of webcam
        private static VideoCaptureDevice videoSource;

        public struct ColorCore
        {
            public ColorCore(uint _r, uint _g, uint _b) { r = _r; g = _g; b =_b;  }
            public uint r;
            public uint g;
            public uint b;
        }

        public struct ColorEmotion
        {
            public static ColorCore Anger{
                get { return new ColorCore {
                        r = 0x0000FF,
                        g = 0x000000,
                        b = 0x000000
                    };
                }
            }

            public static ColorCore Contempt{
                get { return new ColorCore{
                        r = 0x000000,
                        g = 0x0000FF,
                        b = 0x0000FF
                    };
                }
            }

            public static ColorCore Disgust{
                get { return new ColorCore{
                        r = 0x000000,
                        g = 0x0000FF,
                        b = 0x000000
                    };
                }
            }

            public static ColorCore Fear{
                get{ return new ColorCore{
                        r = 0x0000FF,
                        g = 0x000000,
                        b = 0x0000FF
                    };
                }
            }

            public static ColorCore Happiness{
                get{ return new ColorCore{
                        r = 0x0000FF,
                        g = 0x0000FF,
                        b = 0x000000
                    };
                }
            }

            public static ColorCore Neutral{
                get{ return new ColorCore{
                        r = 0x0000FF,
                        g = 0x0000FF,
                        b = 0x0000FF
                    };
                }
            }

            public static ColorCore Sadness{
                get{ return new ColorCore{
                        r = 0x000000,
                        g = 0x000000,
                        b = 0x0000FF
                    };
                }
            }

            public static ColorCore Surprise{
                get{ return new ColorCore{
                        r = 0x0000FF,
                        g = 0x000077,
                        b = 0x000000
                    };
                }
            }
        }

        public static void Main()
        {
            var handle = GetConsoleWindow();
            var defaultStyle = Corale.Colore.Core.Headset.Instance.CurrentEffectId;

            // Uncomment to hide consile window
            //ShowWindow(handle, SW_HIDE);

            currentColor = ColorEmotion.Neutral;
            targetColor = currentColor;

            colorTimer = new System.Timers.Timer();
            colorTimer.Interval = 100;
            colorTimer.Elapsed += OnTimedEventLerp;
            colorTimer.AutoReset = true;
            colorTimer.Enabled = true;

            
            // Create a timer and set a two second interval.
            cameraTimer = new System.Timers.Timer();
            cameraTimer.Interval = 3500;

            // Hook up the Elapsed event for the timer. 
            cameraTimer.Elapsed += OnTimedEvent;

            // Have the timer fire repeated events (true is the default)
            cameraTimer.AutoReset = true;

            // Start the timer
            cameraTimer.Enabled = true;


            // enumerate video devices
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            // create video source
            videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            // set NewFrame event handler
            videoSource.NewFrame += new NewFrameEventHandler(Video_NewFrame);
            videoSource.Start();
            
            

            Console.WriteLine("Press the Enter key to exit the program at any time... ");
            Console.ReadLine();

            cameraTimer.Dispose();
            colorTimer.Dispose();

            videoSource.Stop();

        }

        public static uint colorToUint(ColorCore _color)
        {
            uint result = (_color.r * 0x010000) + (_color.g * 0x000100) + (_color.b * 0x000001);
            return result;
        }

        static bool takePhoto = false;
        static int x = 0;
        static int y = 0;

        private static async void Video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (takePhoto)
            {
                Bitmap bitmap = eventArgs.Frame;
                Bitmap current = (Bitmap)bitmap.Clone();

                string filepath = Environment.CurrentDirectory;
                string fileName = System.IO.Path.Combine(filepath, $"Image{y++}.bmp");
                current.Save(fileName);
                bitmap.Dispose();
                current.Dispose();

                MakeRequest(fileName);
                File.Delete(fileName);

                takePhoto = false;
            }
        }

        private static void OnTimedEventLerp(Object source, System.Timers.ElapsedEventArgs e)
        {

            currentColor.r = ((currentColor.r * 10) + targetColor.r) / 11;
            currentColor.g = ((currentColor.g * 10) + targetColor.g) / 11;
            currentColor.b = ((currentColor.b * 10) + targetColor.b) / 11;

            Guid g = Corale.Colore.Core.Headset.Instance.CurrentEffectId;
            Corale.Colore.Core.Headset.Instance.SetStatic(Corale.Colore.Core.Color.FromRgb(colorToUint(currentColor)));
        }

        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            takePhoto = true;           
        }

        private static void VideoSource_SnapshotFrame(object sender, NewFrameEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);

            byte[] result = binaryReader.ReadBytes((int)fileStream.Length);
            binaryReader.Close();
            fileStream.Close();
            return result;
        }

        static async void MakeRequest(string imageFilePath)
        {
            var client = new HttpClient();
            
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "<inset your own key here>"); // 
            string uri = "https://westus.api.cognitive.microsoft.com/emotion/v1.0/recognize?";
            HttpResponseMessage response;
            string responseContent;

            byte[] byteData = GetImageAsByteArray(imageFilePath);

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(uri, content);
                responseContent = response.Content.ReadAsStringAsync().Result;
            }

            Console.WriteLine(responseContent);
            try
            {
                JToken rootToken = JArray.Parse(responseContent).First;
                JToken faceRectangleToken = rootToken.First;

                JToken scoresToken = rootToken.Last;

                JEnumerable<JToken> scoreList = scoresToken.First.Children();

                string angerString = scoreList.ToArray()[0].ToString().Split(':')[1];
                string contemptString = scoreList.ToArray()[1].ToString().Split(':')[1];
                string disgustString = scoreList.ToArray()[2].ToString().Split(':')[1];
                string fearString = scoreList.ToArray()[3].ToString().Split(':')[1];
                string happinessString = scoreList.ToArray()[4].ToString().Split(':')[1];
                string neutralString = scoreList.ToArray()[5].ToString().Split(':')[1];
                string sadnessString = scoreList.ToArray()[6].ToString().Split(':')[1];
                string surpriseString = scoreList.ToArray()[7].ToString().Split(':')[1];

                double anger = Double.Parse(angerString);
                double contempt = Double.Parse(contemptString);
                double disgust = Double.Parse(disgustString);
                double fear = Double.Parse(fearString);
                double happiness = Double.Parse(happinessString);
                double neutral = Double.Parse(neutralString);
                double sadness = Double.Parse(sadnessString);
                double surprise = Double.Parse(surpriseString);

                sadness = sadness * 0.8;

                Console.WriteLine($"anger : {anger}");
                Console.WriteLine($"contempt : {contempt}");
                Console.WriteLine($"disgust: {disgust}");
                Console.WriteLine($"fear : {fear}");
                Console.WriteLine($"happiness : {happiness}");
                Console.WriteLine($"neutral : {neutral}");
                Console.WriteLine($"sadness : {sadness}");
                Console.WriteLine($"surprise : {surprise}");

                if (anger > Math.Max(contempt,disgust) && anger > Math.Max(fear, happiness) && anger > Math.Max(sadness, surprise))
                {
                    targetColor = ColorEmotion.Anger;
                }

                if (contempt > Math.Max(anger, disgust) && contempt > Math.Max(fear, happiness) && contempt > Math.Max(sadness, surprise))
                {
                    targetColor = ColorEmotion.Contempt;
                }

                if (disgust > Math.Max(contempt, anger) && disgust > Math.Max(fear, happiness) && disgust > Math.Max(sadness, surprise))
                {
                    targetColor = ColorEmotion.Disgust;
                }

                if (fear > Math.Max(contempt, disgust) && fear > Math.Max(anger, happiness) && fear > Math.Max(sadness, surprise))
                {
                    targetColor = ColorEmotion.Fear;
                }

                if (happiness > Math.Max(contempt, disgust) && happiness > Math.Max(fear, anger) && happiness > Math.Max(sadness, surprise))
                {
                    targetColor = ColorEmotion.Happiness;
                }

                if (sadness > Math.Max(contempt, disgust) && sadness > Math.Max(fear, happiness) && sadness > Math.Max(anger, surprise))
                {
                    targetColor = ColorEmotion.Sadness;
                }

                if (surprise > Math.Max(contempt, disgust) && surprise > Math.Max(fear, happiness) && surprise > Math.Max(sadness, anger))
                {
                    targetColor = ColorEmotion.Surprise;
                }
            }
            catch { }
        }
    }
}
