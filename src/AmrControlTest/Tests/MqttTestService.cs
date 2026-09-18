using System;
using System.Collections.Generic;
using System.Text;
using MQTTnet;
using System.Threading.Tasks;

namespace DevEnvironmentTest.Tests
{
    public class MqttTestService
    {
        private IMqttClient? _mqttClient;

        public async Task ConnectAsync()
        {
            var mqttFactory = new MqttClientFactory();
            _mqttClient = mqttFactory.CreateMqttClient();

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", 1883) // 우리가 띄운 Docker Mosquitto
                .Build();

            await _mqttClient.ConnectAsync(mqttClientOptions);
        }
    }
}
