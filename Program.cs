using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace AssetHub.DeviceClient
{
    class Program
    {        
        const string _globalProvisionEndPoint = "global.azure-devices-provisioning.net";
        
        static void Main(string[] args)
        {
            Console.WriteLine("AssetHub Client example");
            var deviceFile = @"c:\temp\device.json";
            var assetreadingsFile = @"c:\temp\assetreadings.csv";

            Console.WriteLine("Reading device information.....");
            var myasset = JsonConvert.DeserializeObject<FlexAsset>(File.ReadAllText(deviceFile));

            // The device need to be provisioned before first time usage. 
            if (!myasset.provisioned)
            {
                Console.WriteLine("Provisiong device.....");
                provisionDevice(myasset.scopeId, myasset.deviceId, myasset.primaryKey).GetAwaiter().GetResult();
                myasset.provisioned = true;
                File.WriteAllText(deviceFile, JsonConvert.SerializeObject(myasset));
            }

            Console.WriteLine("Reading file with readings...");
            // Read load values from asset meter
            var valuesFromMeter = new List<AssetReading>();

            //File with reading from Asset with more granular resolution than 1 minute. Negative load is consumption.

            using (var rd = new StreamReader(assetreadingsFile))
            {
                while (!rd.EndOfStream)
                {
                    var splits = rd.ReadLine().Split(';');
                    var reading = new AssetReading();
                    reading.timestamp = DateTime.ParseExact(splits[0], "yyyyMMddHHmmss", null);
                    reading.load = Convert.ToDouble(splits[1]);
                    valuesFromMeter.Add(reading);
                }
            }

            Console.WriteLine("Aggregate readings to 1 minute resolution with averange values...");
            var aggregatedValuesFromMeter = aggregateReadingsIntoMinutesTimePeriod(valuesFromMeter);

            Console.WriteLine("Convert readings to IoT interface...");
            var convertedList = convertTOMeterValues(aggregatedValuesFromMeter);

            Console.WriteLine("Convert to IoT message format...");
            var messages = convertTOIoTMessages(convertedList);

            Console.WriteLine("Send messages to IoT hub..");
            sendMessagesTOIot(myasset,messages);
        }

        private static void sendMessagesTOIot(FlexAsset myasset, List<Message> messages)
        {
            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(myasset.deviceId, myasset.primaryKey);
            using (var client = Microsoft.Azure.Devices.Client.DeviceClient.Create(myasset.asignedHub, auth, TransportType.Mqtt))
            {
                client.SendEventBatchAsync(messages).GetAwaiter().GetResult();
            }
        }

        private static List<Message> convertTOIoTMessages(List<MeteredValue> meteredValues)
        {
            var listOfMessages = new List<Message>();
            var tempList = meteredValues.Select(JsonConvert.SerializeObject)
            .Select(messageString => new Message(Encoding.UTF8.GetBytes(messageString)))
            .ToList();
            foreach (var singleMessage in tempList)
            {
                singleMessage.ContentEncoding = "UTF-8";
                singleMessage.ContentType = "application/json";
                listOfMessages.Add(singleMessage);
            }
            return listOfMessages;
        }

            private static List<MeteredValue> convertTOMeterValues(List<AssetReading> readings)
        {
            var listOfMeteredValues = new List<MeteredValue>();
            foreach (var value in readings)
            {
                if (value.load > 0)
                {
                    listOfMeteredValues.Add(new MeteredValue { PeriodFrom = value.timestamp, PeriodTo = value.timestamp.AddMinutes(1), AveragePowerGenerationInKW = value.load, AveragePowerConsumptionInKW = 0 });
                }
                else
                {
                    listOfMeteredValues.Add(new MeteredValue { PeriodFrom = value.timestamp, PeriodTo = value.timestamp.AddMinutes(1), AveragePowerConsumptionInKW = value.load * (-1), AveragePowerGenerationInKW = 0 });
                }
            }
            return listOfMeteredValues;
        }

        private static List<AssetReading> aggregateReadingsIntoMinutesTimePeriod(List<AssetReading> readings)
        {
            var aggregatedReading = new List<AssetReading>();

            var avgReadings = readings.GroupBy(s => s.timestamp.Ticks / TimeSpan.FromMinutes(1).Ticks)
            .Select(s => new
            {
                timespanKey = s,
                timestamp = s.First().timestamp,
                average = s.Average(x => x.load)
            }).ToList();

            foreach (var rec in avgReadings)
            {
                aggregatedReading.Add(new AssetReading { load = rec.average, timestamp = new DateTime(rec.timestamp.Year, rec.timestamp.Month, rec.timestamp.Day, rec.timestamp.Hour, rec.timestamp.Minute, 0,DateTimeKind.Utc) });
            }

            return aggregatedReading;
        }
        public static Task<DeviceRegistrationResult> provisionDevice(string scope, string deviceId, string primaryKey)
        {
            using var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly);
            var security = new SecurityProviderSymmetricKey(deviceId, primaryKey, null);
            var provClient = ProvisioningDeviceClient.Create(_globalProvisionEndPoint, scope, security, transport);
            return provClient.RegisterAsync();
        }

    }
}
