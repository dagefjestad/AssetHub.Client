import os
import asyncio
import uuid
from azure.iot.device.aio import IoTHubDeviceClient
from azure.iot.device.aio import ProvisioningDeviceClient
from azure.iot.device import Message

messages_to_send = 10

# "pip install azure-iot-hub"
# "pip install azure-iot-device"

async def main():
    
    # This should ony be done once for every Device

    # vlaues from rest call - Create device
    provisioning_host = "global.azure-devices-provisioning.net"
    id_scope = "*************"
    deviceId = "*********************"
    primarykey = "*******************"

    # Provision device
    provisioning_device_client = ProvisioningDeviceClient.create_from_symmetric_key(
        provisioning_host=provisioning_host,
        registration_id=deviceId,
        id_scope=id_scope,
        symmetric_key=primarykey,
    )

    registration_result = await provisioning_device_client.register()
    print(registration_result)

    # Creating a client for sending data 
    if registration_result.status == "assigned":
        print("Provisioned the device")
        device_client = IoTHubDeviceClient.create_from_symmetric_key(
            symmetric_key=primarykey,
            hostname=registration_result.registration_state.assigned_hub,
            device_id=registration_result.registration_state.device_id,
        
    )

    # Connect the client.
    await device_client.connect()
    msgData = '{"PeriodFrom":"2020-10-16T07:49:00Z","PeriodTo":"2020-10-16T07:50:00Z","AveragePowerConsumptionInKW":0.0,"AveragePowerGenerationInKW":27.0}' 
    msg = Message(msgData, message_id=None, content_encoding="utf-8", content_type="application/json", output_name=None)
    await device_client.send_message(msg)
    
    print("Send message to AssetHUB")

    await device_client.disconnect()


if __name__ == "__main__":
    asyncio.run(main())