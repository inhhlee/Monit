using System;
using System.Collections.Generic;
using System.Text;

namespace Amr.Communication.Mqtt
{
    public class MqttClientService
    {
        // 싱글톤 패턴을 이용한 인스턴스 객체 한개 생성
        private static readonly MqttClientService _instance = new MqttClientService();

        // 기본 생성자 만들기 
        private MqttClientService()
        {
        }


        // 밖에서 참조 할 수 있는 생성자 통로 
        public static MqttClientService instance
        {
            get {  return _instance; }
        }

        // 비동기 메서드
        public async Task ConnectAsync()
        {
            // 실제 연결 로직 
        }

        // get,set 
        public bool IsConnected {get; private set; }
    }
}
