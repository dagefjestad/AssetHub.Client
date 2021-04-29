# AssetHub.Client example

This repository contain example code in C# and Python for injecting metervalues from devices connected to AssetHUB.

The code uses the SDK for Aure IoTHub and Azure Device Provisiong Service.  

Azure IoT normally use AMQP as protocol. Please see the Azure documentation for needed firewall openings. 

Before you can inject messages from an device you have to provision a device first. You need to to do this only once. It is possible to transfer massages in batches.  

Before you can run the example you need to create the device (provided rest service) to get key and scope to use in the provisiong process. You get the url for the assigned iot hub from the provisiong step. 

The code is provides as-is. 