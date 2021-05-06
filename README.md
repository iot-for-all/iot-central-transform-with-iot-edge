---
page_type: sample
description: "A sample to show how to use IoT Edge to transform data on ingress to IoT Central."
languages:
- csharp
- typescript
products:
- azure-iot-central
- azure-iot-edge
urlFragment: azure-iot-central-transform-with-iot-edge
---

# Use IoT Edge to transform data for IoT Central

Downstream IoT devices connected to an IoT Edge device send data in various formats. To use the downstream device data in your IoT Central application, you may need to transform the data to make the format compatible with your IoT Central application.This sample shows you how to use an IoT Edge module to perform a simple transformation of downstream device data format from CSV to JSON in the IoT Edge device and forward the transformed data to IoT Central. At a high level, the steps to configure this scenario are:

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

## Set up and configure the sample

For detailed instructions on how to set up and configure the sample, see [Transform data for IoT Central > Data transformation at ingress](https://docs.microsoft.com/azure/iot-central/core/howto-transform-data#data-transformation-at-ingress)
