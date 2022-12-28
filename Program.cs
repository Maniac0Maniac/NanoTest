// Assemblies used:
// System.Net v1.10.38.33445 (20852 bytes)
// nanoFramework.System.Text v1.2.22.3995 (5828 bytes)
// Windows.Storage v1.5.24.1018 (5324 bytes)
// System.IO.FileSystem v1.1.15.5532 (9796 bytes)
// System.IO.Streams v1.1.27.27650 (6748 bytes)
// System.Threading v1.1.8.6695 (3884 bytes)
// nanoFramework.Runtime.Native v1.5.4.3 (1568 bytes)
// nanoFramework.M2Mqtt v5.1.61.46161 (46332 bytes)
// nanoFramework.Hardware.Esp32 v1.4.8.26232 (4160 bytes)
// System.Device.Wifi v1.5.37.9881 (7244 bytes)
// nanoFramework.System.Collections v1.4.0.3 (4096 bytes)
// mscorlib v1.12.0.4 (31832 bytes)
// nanoFramework.Runtime.Events v1.11.1.42088 (3412 bytes)
// Windows.Storage.Streams v1.14.19.64023 (6388 bytes)
// nanoFramework.Json v2.2.72.60323 (17328 bytes)

using System.Diagnostics;
using System;
using System.Threading;
using System.Net.NetworkInformation;
using nanoFramework.Networking;
using System.Text;
using System.IO;
using nanoFramework.M2Mqtt.Messages;
using nanoFramework.M2Mqtt;
using nanoFramework.Json;
using nanoFramework.Hardware.Esp32;

namespace Skeleton {

    [System.Serializable]

    // Structure for entity confguration
    public class EntityData {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public int GPIO { get; set; }
        public bool Save { get; set; }
        public bool Mqtt { get; set; } = false;
        public string MinMaxStep { get; set; }
        public string Class { get; set; }
        public string Measurement { get; set; }
        public string Icon { get; set; }
    }

    // File paths configuration
    public static class FilePath {
        public static String Config = "I:\\Config.txt";
        public static String Entities = "I:\\Entities.txt";
    }

    public partial class Skeleton {

        // Wifi & MQTT Config
        public static string wifi_username = "WiFiName";
        public static string wifi_password = "WifiPass";
        public static string mqtt_server = "192.168.1.100";
        public static int mqtt_port  = 1883;
        public static string mqtt_username = "MqttUserName";
        public static string mqtt_password = "MqttPassword";

        // Objects
        public static MqttClient Mqtt_Client;
        private static EntityData[] _EntityData;

        // Other varialbes
        public static uint TestCount;

        // Print memory useage to debug & MQTT
        public static void PrintMemory() {

            // Read memory stats
            NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.Internal, out uint totalSize, out uint totalFreeSize, out uint largestBlock);
            uint Managed = nanoFramework.Runtime.Native.GC.Run(false);
            TestCount += 1;

            // Print on debug
            Debug.WriteLine($"--> Free {totalFreeSize}, Block {largestBlock}," + " Managed: " + Managed + " CallCount: " + TestCount) ;

            // Publish to MQTT        
            if (Mqtt_Client != null && Mqtt_Client.IsConnected) {
                Mqtt_Client.Publish("Home/abc/FreeMem", Encoding.UTF8.GetBytes(totalFreeSize.ToString()));
                Mqtt_Client.Publish("Home/abc/FreeBlock", Encoding.UTF8.GetBytes(largestBlock.ToString()));
                Mqtt_Client.Publish("Home/abc/FreeManaged", Encoding.UTF8.GetBytes(Managed.ToString()));
                Mqtt_Client.Publish("Home/abc/TestCount", Encoding.UTF8.GetBytes(TestCount.ToString()));
            }
        }

        public static void Main() {

            // Wipe config file
            bool Cleanup = true;
            if (Cleanup && File.Exists("I:\\Entities.txt")) {File.Delete("I:\\Entities.txt");}

            // Load config (from defaults) and save it
            Load_Entities();
            if (!File.Exists("I:\\Entities.txt")) {Save_Entities();}

            // Load config (from saved file) to make sure it works
            Load_Entities();

            // Start Wifi and MQTT
            Wifi_Start();
            MQTT_Start();

            // Keep reloading the config file -- It errors out!?!?!?
            // Seems same is happening for load and save
            while (true) {
                Thread.Sleep(50);
                PrintMemory();
                Thread.Sleep(50);
                Load_Entities();
                Thread.Sleep(50);
                PrintMemory();
                Thread.Sleep(50);
                Save_Entities();

            }
        }


        // Start the Wifi connection
        private static bool Wifi_Start() {

            try {
                // Prep
                Debug.WriteLine("[WIFI] Waiting for Network Up");
                bool success;
                CancellationTokenSource cs = new(10000);

                // Connect using creds in variables
                success = WifiNetworkHelper.ConnectDhcp(wifi_username, wifi_password, requiresDateTime: true, token: cs.Token);
                if (!success) {
                    Debug.WriteLine($"[WIFI] Can't get a proper IP address and DateTime, error: {NetworkHelper.Status}.");
                    if (NetworkHelper.HelperException != null) {
                        Debug.WriteLine($"[WIFI] Exception: {NetworkHelper.HelperException}");
                    }
                    return false;
                } else {
                    Debug.WriteLine($"[WIFI] Connected - IP: {NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address}");
                    return true;
                }
            } catch (System.Exception Ex) {
                Debug.WriteLine("[WIFI] ERROR in Wifi_Start: " + Ex.Message);
                return false;
            }

        }
        
        // Start MQTT and connect to the server
        private static bool MQTT_Start() {

            try {
                // Connect to the MQTT server
                Debug.WriteLine("[MQTT] Starting MQTT");
                Mqtt_Client = new MqttClient(mqtt_server);
                var ret = Mqtt_Client.Connect("abc", mqtt_username, mqtt_password, true, MqttQoSLevel.AtMostOnce, true, "Home/abc/status", "offline", true, 10);
                if (ret != MqttReasonCode.Success) {
                    Debug.WriteLine($"[MQTT] ERROR connecting: {ret}");
                    Mqtt_Client.Disconnect();
                    return false;
                }
                Debug.WriteLine("[MQTT] Connected");

                // All done!
                return true;
            } catch (Exception Ex) {
                Debug.WriteLine("[MQTT] ERROR in MQTT_Start: " + Ex.Message);
                return false;
            }
        }

        // Save the enity config to internal storage
        private static bool Save_Entities() {

            try {
                //Save the config in the namespace to local storage
                System.Diagnostics.Debug.WriteLine("[CONF] Saving Entity config file: " + FilePath.Entities);
                var serializeData = JsonConvert.SerializeObject(_EntityData);
                System.Diagnostics.Debug.WriteLine("[CONF] " + serializeData);
                var fileStream = new FileStream(FilePath.Entities, FileMode.Create);
                byte[] buffer = Encoding.UTF8.GetBytes(serializeData);
                fileStream.Write(buffer, 0, buffer.Length);
                fileStream.Dispose();
                // Success
                System.Diagnostics.Debug.WriteLine("[CONF] Entity config file saved");
                return true;
            } catch {
                // Failure
                System.Diagnostics.Debug.WriteLine("[CONF] WARNING - Could not save Entity confing file!");
                return false;
            }
        }

        // Load the saved entity config from internal storage (or load defaults is it fails)
        private static void Load_Entities() {

            // Load the Entity Config
            try {
                // Try to load the saved config into the namespace
                System.Diagnostics.Debug.WriteLine("[CONF] Loading Entiry Config file: " + FilePath.Entities);
                if (File.Exists(FilePath.Entities)) {
                    var fs = new FileStream(FilePath.Entities, FileMode.Open);
                    _EntityData = _EntityData = (EntityData[])JsonConvert.DeserializeObject(fs, typeof(EntityData[])); // exception here
                    fs.Dispose();
                    System.Diagnostics.Debug.WriteLine("[CONF] Entity config file loaded"); 
                } else {
                    // If no saved config, load the defaults
                    System.Diagnostics.Debug.WriteLine("[CONF] Entity config file not found. Loading defaults");
                    _EntityData = GetDefaultConfig();
                }
            } catch (Exception Ex) {
                System.Diagnostics.Debug.WriteLine("[WEBS] ERROR in Load_Entities!! - " + Ex.Message);
            }
        }

        // Produce a default config if none exists
        private static EntityData[] GetDefaultConfig() {

            String[][] jaggedArray3 ={
            // System Entities goes here
                            //  0           1                  2          3         4         5           6                 7               8             9
                            // Type         Name               Value      GPIO      Save      Mqtt        Min/Max/Step      Class           Measurement    Icon
               new String[] {"sys",         "Free Mem",        "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "Free Block",      "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "Free Managed",    "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "HostName",        "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "IP",              "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "MAC",             "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"sys",         "Mqtt Reconnected","",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"syssensor",   "RSSI",            "",        "",       "",       "1",        "",               "power",        "%",    "access-point"},
               new String[] {"sys",         "UpTime",          "",        "",       "",       "",         "",               "",             "",     ""},
               new String[] {"message",     "",                "",        "",       "",       "",         "",               "",             "",     ""},

            // Custom Entities goes here...
               new String[] {"sensor_1",    "Temp",            "",        "15",     "",       "1",        "15",             "temperature",  "°C",   "thermometer"},
               new String[] {"sensor_2",    "Power",           "",        "36",     "",       "1",        "",               "power",        "W",    "lightning-bolt"},
               new String[] {"switch_1",    "LEDs",            "",        "19",     "1",      "1",        "12",             "",             "",     "lightbulb"},
               new String[] {"switch_2",    "Boiler",          "",        "23",     "1",      "1",        "33/0/35/78",     "",             "",     "water-boiler"},
               new String[] {"switch_3",    "Lights",          "",        "21",     "1",      "1",        "14",             "",             "",     "lightbulb"},
               new String[] {"switch_4",    "Network",         "",        "22",     "1",      "1",        "27",             "",             "",     "router-network"},
               new String[] {"switch_5",    "Spare",           "",        "18",     "1",      "1",        "13/0/35/65",     "",             "",     "help"},
               new String[] {"separator",   "",                "",        "",       "",       "",         "",               "",             "",     ""}
            };

            try {
                // Convert the config data into EntityData[]
                var _EntityData = new EntityData[jaggedArray3.Length];
                for (int row = 0; row < jaggedArray3.Length; row++) {
                    // Loop through each item in the array and add it to the EntityData Namespace
                    var Item = new EntityData();
                    Item.Type = jaggedArray3[row][0];
                    Item.Name = jaggedArray3[row][1];
                    if (jaggedArray3[row][2] != "") { Item.Value = jaggedArray3[row][2]; }
                    if (jaggedArray3[row][3] != "") { Item.GPIO = Convert.ToInt16(jaggedArray3[row][3]); } else { Item.GPIO = -1; }
                    if (jaggedArray3[row][4] != "") { Item.Save = Convert.ToBoolean(Convert.ToByte(jaggedArray3[row][4])); }
                    if (jaggedArray3[row][5] != "") { Item.Mqtt = Convert.ToBoolean(Convert.ToByte(jaggedArray3[row][5])); }
                    if (jaggedArray3[row][6] != "") { Item.MinMaxStep = jaggedArray3[row][6]; }
                    if (jaggedArray3[row][7] != "") { Item.Class = jaggedArray3[row][7]; }
                    if (jaggedArray3[row][8] != "") { Item.Measurement = jaggedArray3[row][8]; }
                    if (jaggedArray3[row][9] != "") { Item.Icon = jaggedArray3[row][9]; }
                    _EntityData[row] = Item;
                }

                // Return the results
                return _EntityData;
            } catch (Exception Ex) {
                System.Diagnostics.Debug.WriteLine("[VARS] ERROR in GetDefaultConfig: " + Ex.Message);
                return null;
            }

        }

    }
}
