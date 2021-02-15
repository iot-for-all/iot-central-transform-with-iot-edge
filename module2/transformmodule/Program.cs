namespace transformmodule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using YamlDotNet.Serialization;
    using System.Security.Cryptography;
    using Microsoft.Azure.Devices.Provisioning.Client;
    using Microsoft.Azure.Devices.Provisioning.Client.Transport;
    using Microsoft.Azure.Devices.Shared;

    class Telemetry
    {
        public string deviceID;
        public float temperature;
        public float humidity;
        public string scale;
    }

    class YamlDocument
    {
        readonly Dictionary<object, object> root;

        public YamlDocument(string input)
        {
            var reader = new StringReader(input);
            var deserializer = new Deserializer();
            this.root = (Dictionary<object, object>)deserializer.Deserialize(reader);
        }

        public object GetKeyValue(string key)
        {
            if(this.root.ContainsKey(key))
            {
                return this.root[key];
            }

            foreach(var item in this.root)
            {
                var subItem = item.Value as Dictionary<object, object>;
                if(subItem != null && subItem.ContainsKey(key))
                {
                    return subItem[key];
                }
            }

            return null;            
        }
    }
    class Program
    {
        static int counter;
        static string globalDpsHostname;
        static string masterKey;
        static string scopeId;

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

            try {
                bool appfilexists = File.Exists(@"/app/copiedConfig.yaml");
                StreamReader sr = new StreamReader(@"/app/copiedConfig.yaml");
                var yamlString = sr.ReadToEnd();

                var yamlDoc = new YamlDocument(yamlString);
                scopeId = yamlDoc.GetKeyValue("scope_id").ToString();

                masterKey = yamlDoc.GetKeyValue("masterdpssaskey").ToString();
                var globalEndPoint = yamlDoc.GetKeyValue("global_endpoint").ToString();
                globalDpsHostname = new Uri(globalEndPoint).Host;
            }
            catch (Exception ex){
                Console.WriteLine($" encountered exception {ex.Message}");
                 Console.WriteLine($" encountered innerexception {ex.InnerException}");
            }
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

                Telemetry gatet = new Telemetry();
                gatet.deviceID = message.ConnectionDeviceId;
                gatet.humidity = messageBody.humidity;
                gatet.temperature = messageBody.temperature;
                gatet.scale = messageBody.scale;

                Telemetry hubt = new Telemetry();
                hubt.deviceID = message.ConnectionDeviceId;
                hubt.humidity = messageBody.humidity;
                hubt.temperature = messageBody.scale.ToLower() == "celsius" ? (messageBody.temperature * 9/5) + 32 : messageBody.temperature;
                hubt.scale = messageBody.scale.ToLower() == "celsius" ? "farenheit" : messageBody.scale.ToLower();

                // serialize to a string
                string hubMessage = JsonConvert.SerializeObject(hubt);
                string gatewayMessage = JsonConvert.SerializeObject(gatet);

                // create a new IoT Message object and copy
                // any properties from the original message
                var hubPipeMessage = new Message(Encoding.ASCII.GetBytes(hubMessage));
                var gatewayPipeMessage = new Message(Encoding.ASCII.GetBytes(gatewayMessage));
                foreach (var prop in message.Properties)
                {
                    Console.WriteLine($"property key {prop.Key} and value {prop.Value}");
                    hubPipeMessage.Properties.Add(prop.Key, prop.Value);
                    gatewayPipeMessage.Properties.Add(prop.Key, prop.Value);
                }

                // send the data to the edge Hub on a named output (for routing)
                await moduleClient.SendEventAsync("output1", gatewayPipeMessage);

                // send message to hub directly
                await SendDeviceMessage(hubPipeMessage, message.ConnectionDeviceId);
                Console.WriteLine($"Converted message sent({counter}): {hubPipeMessage}");
                
            }
            return MessageResponse.Completed;
        }

        static async Task SendDeviceMessage(Message message, string deviceId) 
        {
            try
            {

                var transformedKey = ComputeDerivedSymmetricKey(masterKey, deviceId);
                using var security = new SecurityProviderSymmetricKey(
                    deviceId,
                    transformedKey,
                    null);

                using var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly);

                ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
                    globalDpsHostname,
                    scopeId,
                    security,
                    transport);

                Console.WriteLine($"Initialized for registration Id {security.GetRegistrationID()}.");

                Console.WriteLine("Registering with the device provisioning service...");
                DeviceRegistrationResult result = await provClient.RegisterAsync();

                Console.WriteLine($"Registration status: {result.Status}.");
                if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                {
                    Console.WriteLine($"Registration status did not assign a hub, so exiting this sample.");
                    return;
                }

                IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
                    result.DeviceId,
                    security.GetPrimaryKey());
                DeviceClient iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt_Tcp_Only);
                await iotClient.OpenAsync();
                await iotClient.SendEventAsync(message);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Encountered exception. message - {ex.Message}.");
                Console.WriteLine($"Encountered exception. inner exception {ex.InnerException}.");
                return;
            }
        }
    }
}
