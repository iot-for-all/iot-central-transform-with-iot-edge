# iot-central-transform-with-iot-edge
IoT devices connected to an Edge device send data in various formats. To use the downstream device data, sent through Edge device, with your IoT Central application, you may need to use a transformation to make the format of the data compatible with your IoT Central application. In this example, we will use an IoT Edge module to perform a simple transformation of downstream device data format from CSV to JSON in the Edge device and forward the transformed data to IoT Central. At a high level, the steps to configure this scenario are:


## Steps

1. Set up an IoT Edge device: Install and provision an IoT Edge device as a gateway and connect the gateway to your IoT Central application.

2. Connect downstream device to the IoT Edge device: Connect downstream devices to the IoT Edge device and provision them to your IoT Central application.

3. Transform device data in IoT Edge: Create an IoT Edge module to transform the data. Deploy the module to the IoT Edge gateway device that forwards the transformed device data to your IoT Central application.

4. Verify: Send data from a downstream device to the gateway and verify the transformed device data reaches your IoT Central application.

In the example described in the following sections, the downstream device sends CSV data in the following format to the IoT Edge gateway device:
```
"<temperature >, <pressure>, <humidity>"
```
You want to use an IoT Edge module to transform the data to the following JSON format before it's sent to IoT Central:
```
{
  "device": {
      "deviceId": "<downstream-deviceid>"
  },
  "measurements": {
    "temp": <temperature>,
    "pressure": <pressure>,
    "humidity": <humidity>,
  }
}
```
The following steps show you how to set up and configure this scenario:


In this example, the IoT Edge device runs a custom module that transforms the data from the downstream device. Before you deploy and configure the IoT Edge device, you need to:

## Build the custom module.
Add the custom module to a container registry.

The IoT Edge runtime downloads custom modules from a container registry such as an Azure container registry or Docker Hub. The Azure Cloud Shell has all the tools you need to create a container registry, build the module, and upload the module to the registry:

To create a container registry:

1. Open the <a href="https://shell.azure.com/" target="_blank">Azure Cloud Shell</a> and sign in to your Azure subscription.
2. Run the following commands to create an Azure container registry:
```
REGISTRY_NAME="{your unique container registry name}"
az group create --name ingress-scenario --location eastus
az acr create -n $REGISTRY_NAME -g ingress-scenario --sku Standard --admin-enabled true
az acr credential show -n $REGISTRY_NAME
```
Make a note of the username and password values, you use them later.

To build the custom module in the Azure Cloud Shell:
1.In the Azure Cloud Shell, navigate to a suitable folder.
2.To clone the GitHub repository that contains the module source code, run the following command:
```
cd iot-central-transform-with-iot-edge/custommodule/transformmodule
az acr build --registry $REGISTRY_NAME --image transformmodule:0.0.1-amd64 -f Dockerfile.amd64 .
```
3. To build the custom module, run the following commands in the Azure Cloud Shell:
```
cd iot-central-transform-with-iot-edge/custommodule/transformmodule
az acr build --registry $REGISTRY_NAME --image transformmodule:0.0.1-amd64 -f Dockerfile.amd64 .
```
The previous commands may take several minutes to run.

## Set up an IoT Edge device
This scenario uses an IoT Edge gateway device to transform the data from any downstream devices. This section describes how to create IoT Central device templates for the gateway and downstream devices in your IoT Central application. IoT Edge devices use a deployment manifest to configure their modules.

To create a device template for the downstream device. This scenario uses a simple thermostat device model:
1.Download the device model for the <a href="https://raw.githubusercontent.com/Azure/iot-plugandplay-models/main/dtmi/com/example/thermostat-2.json" target="_blank">thermostat</a> device to your local machine.
2.Sign in to your IoT Central application and navigate to the Device templates page.
3.Select + New, select IoT Device, and select Next: Customize.
4.Enter Thermostat as the template name and select Next: Review. Then select Create.
5.Select Import a model and import the thermostat-2.json file you downloaded previously.
6.Select Publish to publish the new device template.

To create a device template for the IoT Edge gateway device:

1.Save a copy of the deployment manifest to your local development machine: <a href="https://raw.githubusercontent.com/iot-for-all/iot-central-transform-with-iot-edge/main/edgemodule/moduledeployment.json" target="_blank">moduledeployment.json</a>
2.Open your local copy of the moduledeployment.json manifest file in a text editor.
3.Find the registryCredentials section and replace the placeholders with the values you made a note of when you created the Azure container registry. The address value looks like <username>.azurecr.io.
4.Find the settings section for the transformmodule. Replace <acr or docker repo> with the same address value you used in the previous step. Save the changes.
5.In your IoT Central application, navigate to the Device templates page.
6.Select + New, select Azure IoT Edge, and then select Next: Customize.
7.Enter IoT Edge gateway device as the device template name. Select This is a gateway device. Select Browse to upload the moduledeployment.json deployment manifest file you edited previously.
8.When the deployment manifest is validated, select Next: Review, then select Create.
9.Under Model, select Relationships. Select + Add relationship. Enter Downstream device as the display name, and select Thermostat as the target. Select Save.
10.Select Publish to publish the device template.

You now have two device templates. The IoT Edge gateway device template, and the Thermostat template as the downstream device.

To register a gateway device in IoT Central:
1. In your IoT Central application, navigate to the Devices page.
2. Select IoT Edge gateway device and select Create a device. Enter IoT Edge gateway device as the device name, enter gateway-01 as the device ID, make sure IoT Edge gateway device is selected as the device template. Select Create.
3. In the list of devices, click on the IoT Edge gateway device, and then select Connect.
4. Make a note of the ID scope, Device ID, and Primary key values for the IoT Edge gateway device. You use them later.

To register a downstream device in IoT Central:
1. In your IoT Central application, navigate to the Devices page.
2. Select Thermostat and select Create a device. Enter Thermostat as the device name, enter downstream-01 as the device ID, make sure Thermostat is selected as the device template. Select Create.
3. In the list of devices, select the Thermostat and then select Attach to Gateway. Select the IoT Edge gateway device template and the IoT Edge gateway device instance. Select Attach.
4. In the list of devices, click on the Thermostat, and then select Connect.
5. Make a note of the ID scope, Device ID, and Primary key values for the Thermostat device. You use them later.

## Deploy the gateway and downstream devices

For convenience, this example uses Azure virtual machines to run the gateway and downstream devices. To create the two Azure virtual machines, click <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fiot-central-docs-samples%2Fmaster%2Ftransparent-gateway%2FDeployGatewayVMs.json" target="_blank">Deploy to Azure</a> and use the following information to complete the Custom deployment form:

Resource group	ingress-scenario
DNS Label Prefix Gateway	A unique DNS name for this machine such as <your name>edgegateway
DNS Label Prefix Downstream	A unique DNS name for this machine such as <your name>downstream
Scope ID	The ID scope you made a note of previously
Device ID IoT Edge Gateway	gateway-01
Device Key IoT Edge Gateway	The primary key value you made a note of previously
Authentication Type	Password
Admin Password Or Key	Your choice of password for the AzureUser account on both virtual machines.

Select Review + Create, and then Create. It takes a couple of minutes to create the virtual machines in the ingress-scenario resource group.

To check that the IoT Edge device is running correctly:
1.Open your IoT Central application. Then navigate to the IoT Edge Gateway device on the list of devices on the Devices page.
2.Select the Modules tab and check the status of the three modules. It takes a few minutes for the IoT Edge runtime to start up in the virtual machine. When it's started, the status of the three modules is Running. If the IoT Edge runtime doesn't start, see Troubleshoot your IoT Edge device.

For your IoT Edge device to function as a gateway, it needs some certificates to prove its identity to any downstream devices. This article uses demo certificates. In a production environment, use certificates from your certificate authority.

To generate the demo certificates and install them on your gateway device:
1.Use SSH to connect to and sign in on your gateway device virtual machine. You can find the DNS name for this virtual machine in the Azure portal. Navigate to the edgegateway virtual machine in the ingress-scenario resource group.
_Tip:You may need to open the port 22 for SSH access on both your virtual machines before you can use SSH to connect from your local machine or the Azure Cloud Shell._
2.Run the following commands to clone the IoT Edge repository and generate your demo certificates:
```
# Clone the repo
cd ~
git clone https://github.com/Azure/iotedge.git

# Generate the demo certificates
mkdir certs
cd certs
cp ~/iotedge/tools/CACertificates/*.cnf .
cp ~/iotedge/tools/CACertificates/certGen.sh .
./certGen.sh create_root_and_intermediate
./certGen.sh create_edge_device_ca_certificate "mycacert"
```
After you run the previous commands, the following files are ready to use in the next steps:

~/certs/certs/azure-iot-test-only.root.ca.cert.pem - The root CA certificate used to make all the other demo certificates for testing an IoT Edge scenario.
~/certs/certs/iot-edge-device-mycacert-full-chain.cert.pem - A device CA certificate that's referenced from the config.yaml file. In a gateway scenario, this CA certificate is how the IoT Edge device verifies its identity to downstream devices.
~/certs/private/iot-edge-device-mycacert.key.pem - The private key associated with the device CA certificate.
3.Open the config.yaml file in a text editor. For example:
```
sudo nano /etc/iotedge/config.yaml
```
4.Locate the Certificate settings settings. Uncomment and modify the certificate settings as follows:
```
certificates:
  device_ca_cert: "file:///home/AzureUser/certs/certs/iot-edge-device-ca-mycacert-full-chain.cert.pem"
  device_ca_pk: "file:///home/AzureUser/certs/private/iot-edge-device-ca-mycacert.key.pem"
  trusted_ca_certs: "file:///home/AzureUser/certs/certs/azure-iot-test-only.root.ca.cert.pem"
```
The example shown above assumes you're signed in as AzureUser and created a device CA certificated called "mycacert".
5.Save the changes and restart the IoT Edge runtime:
```
sudo systemctl restart iotedge
```
If the IoT Edge runtime starts successfully after your changes, the status of the $edgeAgent and $edgeHub modules changes to Running. You can see these status values on the Modules page for your gateway device in IoT Central.
If the runtime doesn't start, check the changes you made in config.yaml and see  <a href="https://review.docs.microsoft.com/en-us/azure/iot-edge/troubleshoot" target="_blank">Troubleshoot your IoT Edge device</a>.

## Connect downstream device to IoT Edge device
To connect a downstream device to the IoT Edge gateway device:
1.Use SSH to connect to and sign in on your downstream device virtual machine. You can find the DNS name for this virtual machine in the Azure portal. Navigate to the leafdevice virtual machine in the ingress-scenario resource group.
_Tip:You may need to open the port 22 for SSH access on both your virtual machines before you can use SSH to connect from your local machine or the Azure Cloud Shell._
2.To clone the GitHub repository with the source code for the sample downstream device, run the following command:
```
cd ~
git clone https://github.com/iot-for-all/iot-central-transform-with-iot-edge
```
3.To copy the required certificate from the gateway device, run the following scp commands. This scp command uses the hostname edgegateway to identify the gateway virtual machine. You'll be prompted for your password:
```
cd ~/iot-central-transform-with-iot-edge
scp AzureUser@edgegateway:/home/AzureUser/certs/certs/azure-iot-test-only.root.ca.cert.pem leafdevice/certs
```
4. Navigate to the leafdevice folder and install the required packages. Then run the build and start scripts to provision and connect the device to the gateway:
```
cd ~/iot-central-transform-with-iot-edge/leafdevice
sudo apt update
sudo apt install nodejs npm node-typescript
npm install
npm run-script build
npm run-script start
```
5. Enter the device ID, scope ID, and SAS key for the downstream device you created previously. For the hostname, enter edgegateway. The output from the command looks like:
```
Registering device downstream-01 with scope 0ne00284FD9
Registered device downstream-01.
Connecting device downstream-01
Connected device downstream-01
Sent telemetry for device downstream-01
Sent telemetry for device downstream-01
Sent telemetry for device downstream-01
Sent telemetry for device downstream-01
Sent telemetry for device downstream-01
Sent telemetry for device downstream-01
```

## Verify
To verify the scenario is running, navigate to your IoT Edge gateway device in IoT Central:
![image](https://user-images.githubusercontent.com/42657564/115048548-880c8a80-9e8e-11eb-8f1f-1ea98451dcc6.png)

Select Modules. Verify that the three IoT Edge modules $edgeAgent, $edgeHub and transformmodule are running.
Select the Downstream Devices and verify that the downstream device is provisioned.
Select Raw data. The telemetry data in the Unmodeled data column looks like:
```
{"device":{"deviceId":"downstream-01"},"measurements":{"temperature":85.21208,"pressure":59.97321,"humidity":77.718124,"scale":"farenheit"}}
```
Because the IoT Edge device is transforming the data from the downstream device, the telemetry is associated with the gateway device in IoT Central. We can see the transformed dated in the Raw data view. To visualize the telemetry, create a new version of the IoT Edge gateway device template with definitions for the telemetry types.

