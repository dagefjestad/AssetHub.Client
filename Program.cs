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

namespace AssetHub.DeviceClient
{
    internal class Program
    {
        const string GlobalProvisionEndPoint = "global.azure-devices-provisioning.net";

        private static void Main(string[] args)
        {
            Console.WriteLine("AssetHub Client example");

            
            var myAsset = ReadDeviceDetails();
            var assetReadings = ReadAssetReadingsFromFile();
            var aggregatedValuesFromMeter = AggregateReadingsIntoMinutesTimePeriod(assetReadings);
            var convertedList = ConvertToMeterValues(aggregatedValuesFromMeter);
            var messages = ConvertToIoTMessages(convertedList);

            SendMessagesToIotHub(myAsset, messages);
        }

        private static FlexAsset ReadDeviceDetails()
        {
            Console.Write("Reading device information...");

            var deviceFile = Path.Combine(Directory.GetCurrentDirectory(), "device.json");
            var myAsset = JsonConvert.DeserializeObject<FlexAsset>(File.ReadAllText(deviceFile));
            Console.WriteLine($" found {myAsset.deviceId}");

            ProvisionDevice(myAsset, deviceFile);

            return myAsset;
        }

        private static void ProvisionDevice(FlexAsset myAsset, string deviceFile)
        {
            // The device need to be provisioned before first time usage.
            if (myAsset.provisioned) return;

            myAsset.asignedHub = ProvisionDevice(myAsset.scopeId, myAsset.deviceId, myAsset.primaryKey);            
            myAsset.provisioned = true;
            File.WriteAllText(deviceFile, JsonConvert.SerializeObject(myAsset));
        }

        private static List<AssetReading> ReadAssetReadingsFromFile()
        {
            Console.Write("Reading file with readings...");

            // File with reading from Asset with more granular resolution than 1 minute. Negative load is consumption.
            var assetReadingsFile = Path.Combine(Directory.GetCurrentDirectory(), "assetreadings.csv");
            var lines = File.ReadAllLines(assetReadingsFile);
            var assetReadings = lines
                .Select(line => line.Split(';'))
                .Select(splits => new AssetReading { timestamp = DateTime.ParseExact(splits[0], "yyyyMMddHHmmss", null), load = Convert.ToDouble(splits[1]) })
                .ToList();
            Console.WriteLine($" {assetReadings.Count} successfully read");
            return assetReadings;
        }

        private static void SendMessagesToIotHub(FlexAsset myAsset, List<Message> messages)
        {
            Console.Write("Send messages to IoT hub...");
            
            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(myAsset.deviceId, myAsset.primaryKey);
            using var client = Microsoft.Azure.Devices.Client.DeviceClient.Create(myAsset.asignedHub, auth, TransportType.Mqtt);
            client.SendEventBatchAsync(messages).GetAwaiter().GetResult();

            Console.WriteLine(" successfully sent!");
        }

        private static List<Message> ConvertToIoTMessages(List<MeteredValue> meteredValues)
        {
            Console.WriteLine("Convert to IoT message format..."); 
            
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

        private static List<MeteredValue> ConvertToMeterValues(List<AssetReading> readings)
        {
            Console.WriteLine("Convert readings to IoT interface...");

            return readings.Select(value => value.load > 0
                    ? new MeteredValue {PeriodFrom = value.timestamp, PeriodTo = value.timestamp.AddMinutes(1), AveragePowerGenerationInKW = value.load, AveragePowerConsumptionInKW = 0}
                    : new MeteredValue {PeriodFrom = value.timestamp, PeriodTo = value.timestamp.AddMinutes(1), AveragePowerConsumptionInKW = value.load * (-1), AveragePowerGenerationInKW = 0})
                .ToList();
        }

        private static List<AssetReading> AggregateReadingsIntoMinutesTimePeriod(List<AssetReading> readings)
        {
            Console.Write("Aggregate readings to 1 minute resolution with average values...");

            var avgReadings = readings.GroupBy(s => s.timestamp.Ticks / TimeSpan.FromMinutes(1).Ticks)
            .Select(s => new
            {
                timespanKey = s,
                timestamp = s.First().timestamp,
                average = s.Average(x => x.load)
            }).ToList();

            var result = avgReadings.Select(rec => new AssetReading
            {
                load = rec.average,
                timestamp = new DateTime(rec.timestamp.Year, rec.timestamp.Month, rec.timestamp.Day, rec.timestamp.Hour, rec.timestamp.Minute, 0, DateTimeKind.Utc)
            }).ToList();

            Console.WriteLine($" {result.Count} aggregated asset readings");

            return result;
        }

        private static string ProvisionDevice(string scope, string deviceId, string primaryKey)
        {
            Console.Write("Provisioning device...");

            using var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly);
            var security = new SecurityProviderSymmetricKey(deviceId, primaryKey, null);
            var provClient = ProvisioningDeviceClient.Create(GlobalProvisionEndPoint, scope, security, transport);
            var result = provClient.RegisterAsync().GetAwaiter().GetResult();

            Console.WriteLine(" successfully provisioned");
            return result.AssignedHub;
        }

    }
}
