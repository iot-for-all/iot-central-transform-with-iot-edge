namespace transformmodule
{
    using System;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;
    using System.Security.Cryptography;

    class Telemetry
    {
        public string deviceID {get; set;}
        public float temperature {get; set;}
        public float pressure {get; set;}
        public float humidity {get; set;}
        public string scale {get; set;}
    }

    class Transformation {
        public Device device;
        public Measurements measurements;
    }

    class Device {
        public string deviceId;
    }

    class Measurements {
        public float temperature;
        public float pressure;
        public float humidity;
        public string scale; 
    }

    class Program
    {
        static int counter;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
        }

        static string ComputeDerivedSymmetricKey(string enrollmentKey, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(enrollmentKey))
            {
                return enrollmentKey;
            }

            using var hmac = new HMACSHA256(Convert.FromBase64String(enrollmentKey));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
        }


        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var messageBody = JsonConvert.DeserializeObject<Telemetry>(messageString);

                string devId = "DeviceIdMissing";

                // retrieve the device ID of the sending leaf device and insert into message
                if(!string.IsNullOrEmpty(message.ConnectionDeviceId))
                        devId = message.ConnectionDeviceId;

                // create and populate our new message content
                Device device = new Device();
                device.deviceId = message.ConnectionDeviceId;

                Measurements measure = new Measurements();
                measure.pressure = messageBody.pressure;
                measure.humidity = messageBody.humidity;
                measure.temperature = messageBody.scale.ToLower() == "celsius" ? (messageBody.temperature * 9/5) + 32 : messageBody.temperature;
                measure.scale = messageBody.scale.ToLower() == "celsius" ? "farenheit" : messageBody.scale.ToLower();

                Transformation transformation = new Transformation();
                transformation.device = device;
                transformation.measurements = measure;

                string gatewayMessage = JsonConvert.SerializeObject(transformation);

                // create a new IoT Message object and copy
                // any properties from the original message
                var gatewayPipeMessage = new Message(Encoding.ASCII.GetBytes(gatewayMessage));
                foreach (var prop in message.Properties)
                {
                    Console.WriteLine($"property key {prop.Key} and value {prop.Value}");
                    gatewayPipeMessage.Properties.Add(prop.Key, prop.Value);
                }

                // send the data to the edge Hub on a named output (for routing)
                await moduleClient.SendEventAsync("output1", gatewayPipeMessage);
                Console.WriteLine($"Converted message sent({counter}): {gatewayPipeMessage}");
                
            }
            return MessageResponse.Completed;
        }
    }
}
