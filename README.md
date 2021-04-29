# AssetHub.Client example

This repository contain example code in C# and Python for injecting metervalues from devices connected to AssetHUB.

The code uses the SDK for Aure IoTHub and Azure Device Provisiong Service.  

Azure IoT normally use AMQP as protocol. Please see the Azure documentation for needed firewall openings. It is possible to transfer massages in batches.

Before you can inject messages from a device, you need to provision the device. This a one-time operation. The provvisoning process give the url for the assigned iot hub to use for sending metervalues.   

Before you can run the example you need to create the device (rest service) to get key and scope to use in the provisiong process.  

The code is provides as-is. 