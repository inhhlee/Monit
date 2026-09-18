using Amr.Communication.Mqtt;
using System;

public class MqttSubscriber
{

    // Mqtt ClientService 읽기 전용 캡슐화 
    private readonly MqttClientService _mqttClient;

    // 기본 생성자를 통해 캡슐화 관리
    public MqttSubscriber(MqttClientService mqttClient)
    {
        _mqttClient = mqttClient;
    }


    //  네트워크 통신 시간이 걸리는 비동기 작업을 핸들러 내부에서 정리 
    // 수신한 메시지를 어떻게 위로 넘길 것인지에 대해서 생각하기 
    public async Task SubscribeAsync()
    {
        // Telemetry
        // State
        // Alarm
        // Heartbeat
        // ACK
    }
}