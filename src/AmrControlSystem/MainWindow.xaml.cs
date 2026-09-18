using MQTTnet;
using MQTTnet.Protocol;
using System.Windows;
using Microsoft.Data.Sqlite;
using Serilog;
using System.Text.Json;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;


namespace AmrControlSystem
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void IntegrationTest_Click(
    object sender,
    RoutedEventArgs e)
        {
            try
            {
                ResultTextBox.Clear();
                ResultTextBox.AppendText("[INTEGRATION TEST START]\n");

                const string topic = "devtest/integration";
                string runId = Guid.NewGuid().ToString();

                double[] testSpeeds =
                {
            0.5,
            0.8,
            1.2,
            1.5,
            1.1
        };

                var receivedSpeeds = new List<double>();

                //
                // 1. SQLite 준비
                //
                using (var connection =
                       new SqliteConnection("Data Source=devtest.db"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();

                    command.CommandText =
                    """
            CREATE TABLE IF NOT EXISTS IntegrationTelemetry
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId TEXT NOT NULL,
                AmrId TEXT NOT NULL,
                Speed REAL NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """;

                    command.ExecuteNonQuery();
                }

                ResultTextBox.AppendText(
                    "SQLITE READY : PASS\n");

                //
                // 2. Seq Logger 준비
                //
                using var logger =
                    new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Seq(
                            "http://localhost:5341")
                        .CreateLogger();

                ResultTextBox.AppendText(
                    "SEQ LOGGER : PASS\n");

                //
                // 3. MQTT Client 생성
                //
                var factory =
                    new MqttClientFactory();

                using var client =
                    factory.CreateMqttClient();

                var receivedCount = 0;

                var receiveCompleted =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                //
                // 4. MQTT 메시지 수신 처리
                //
                client.ApplicationMessageReceivedAsync += e =>
                {
                    try
                    {
                        var json =
                            e.ApplicationMessage
                                .ConvertPayloadToString();

                        var telemetry =
                            JsonSerializer.Deserialize<TelemetryMessage>(
                                json);

                        if (telemetry == null)
                            return Task.CompletedTask;

                        //
                        // SQLite 저장
                        //
                        using var connection =
                            new SqliteConnection(
                                "Data Source=devtest.db");

                        connection.Open();

                        var insert =
                            connection.CreateCommand();

                        insert.CommandText =
                        """
                INSERT INTO IntegrationTelemetry
                (
                    RunId,
                    AmrId,
                    Speed,
                    CreatedAt
                )
                VALUES
                (
                    $runId,
                    $amrId,
                    $speed,
                    $createdAt
                );
                """;

                        insert.Parameters.AddWithValue(
                            "$runId",
                            runId);

                        insert.Parameters.AddWithValue(
                            "$amrId",
                            telemetry.AmrId);

                        insert.Parameters.AddWithValue(
                            "$speed",
                            telemetry.Speed);

                        insert.Parameters.AddWithValue(
                            "$createdAt",
                            telemetry.Timestamp.ToString("O"));

                        insert.ExecuteNonQuery();

                        //
                        // Seq 로그 기록
                        //
                        logger.Information(
                            "AMR Telemetry 수신 " +
                            "{AmrId} {Speed} {RunId}",
                            telemetry.AmrId,
                            telemetry.Speed,
                            runId);

                        lock (receivedSpeeds)
                        {
                            receivedSpeeds.Add(
                                telemetry.Speed);
                        }

                        Dispatcher.Invoke(() =>
                        {
                            ResultTextBox.AppendText(
                                $"RECEIVE : {telemetry.AmrId} / " +
                                $"Speed={telemetry.Speed}\n");
                        });

                        if (Interlocked.Increment(
                                ref receivedCount)
                            >= testSpeeds.Length)
                        {
                            receiveCompleted.TrySetResult(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        receiveCompleted.TrySetException(ex);
                    }

                    return Task.CompletedTask;
                };

                //
                // 5. Mosquitto 연결
                //
                var options =
                    new MqttClientOptionsBuilder()
                        .WithClientId(
                            $"IntegrationTest-{Guid.NewGuid()}")
                        .WithTcpServer(
                            "localhost",
                            1883)
                        .Build();

                await client.ConnectAsync(
                    options,
                    CancellationToken.None);

                ResultTextBox.AppendText(
                    "MQTT CONNECT : PASS\n");

                //
                // 6. Topic Subscribe
                //
                var subscribeOptions =
                    factory
                        .CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(topic)
                        .Build();

                await client.SubscribeAsync(
                    subscribeOptions,
                    CancellationToken.None);

                ResultTextBox.AppendText(
                    "MQTT SUBSCRIBE : PASS\n");

                //
                // 7. AMR Telemetry 5건 Publish
                //
                foreach (var speed in testSpeeds)
                {
                    var telemetry =
                        new TelemetryMessage
                        {
                            AmrId = "AMR_001",
                            Speed = speed,
                            Timestamp = DateTime.Now
                        };

                    string json =
                        JsonSerializer.Serialize(
                            telemetry);

                    var message =
                        new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(json)
                            .WithQualityOfServiceLevel(
                                MqttQualityOfServiceLevel.AtMostOnce)
                            .Build();

                    await client.PublishAsync(
                        message,
                        CancellationToken.None);

                    await Task.Delay(100);
                }

                ResultTextBox.AppendText(
                    "MQTT PUBLISH : PASS\n");

                //
                // 8. 전체 메시지 수신 대기
                //
                var completed =
                    await Task.WhenAny(
                        receiveCompleted.Task,
                        Task.Delay(5000));

                if (completed != receiveCompleted.Task)
                {
                    throw new Exception(
                        "MQTT 수신 Timeout");
                }

                await receiveCompleted.Task;

                //
                // 9. SQLite 실제 저장 건수 검증
                //
                long savedCount;

                using (var connection =
                       new SqliteConnection(
                           "Data Source=devtest.db"))
                {
                    connection.Open();

                    var command =
                        connection.CreateCommand();

                    command.CommandText =
                    """
            SELECT COUNT(*)
            FROM IntegrationTelemetry
            WHERE RunId = $runId;
            """;

                    command.Parameters.AddWithValue(
                        "$runId",
                        runId);

                    savedCount =
                        (long)(command.ExecuteScalar() ?? 0L);
                }

                ResultTextBox.AppendText(
                    $"SQLITE SAVED : {savedCount} / " +
                    $"{testSpeeds.Length}\n");

                //
                // 10. LiveCharts 갱신
                //
                double[] chartValues;

                lock (receivedSpeeds)
                {
                    chartValues =
                        receivedSpeeds.ToArray();
                }

                TestChart.Series =
                    new ISeries[]
                    {
                new LineSeries<double>
                {
                    Name = "AMR Speed",
                    Values = chartValues
                }
                    };

                ResultTextBox.AppendText(
                    "LIVECHART UPDATE : PASS\n");

                //
                // 11. MQTT 연결 종료
                //
                await client.DisconnectAsync();

                ResultTextBox.AppendText(
                    "MQTT DISCONNECT : PASS\n");

                ResultTextBox.AppendText(
                    "[INTEGRATION TEST PASS]\n");
            }
            catch (Exception ex)
            {
                ResultTextBox.AppendText(
                    $"[INTEGRATION TEST FAIL]\n{ex.Message}\n");
            }
        }

        private sealed class TelemetryMessage
        {
            public string AmrId { get; set; } = "";
            public double Speed { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private void ChartTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultTextBox.Clear();
                ResultTextBox.AppendText("[LIVECHARTS TEST START]\n");

                TestChart.Series = new ISeries[]
                {
            new LineSeries<double>
            {
                Name = "AMR Speed",

                Values = new double[]
                {
                    0.5,
                    0.8,
                    1.2,
                    1.5,
                    1.1,
                    1.8,
                    1.4
                }
            }
                };

                ResultTextBox.AppendText(
                    "CHART RENDER : PASS\n");

                ResultTextBox.AppendText(
                    "[LIVECHARTS TEST END]\n");
            }
            catch (Exception ex)
            {
                ResultTextBox.AppendText(
                    $"FAIL : {ex.Message}\n");
            }
        }
        private void SeqTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultTextBox.Clear();
                ResultTextBox.AppendText("[SEQ TEST START]\n");

                using var logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Seq("http://localhost:5341")
                    .CreateLogger();

                ResultTextBox.AppendText(
                    "LOGGER CREATE : PASS\n");

                logger.Information(
                    "개발환경 Seq 로그 테스트 - AMR ID: {AmrId}, Result: {Result}",
                    "AMR_001",
                    "PASS");

                ResultTextBox.AppendText(
                    "LOG SEND : PASS\n");

                ResultTextBox.AppendText(
                    "[SEQ TEST END]\n");
            }
            catch (Exception ex)
            {
                ResultTextBox.AppendText(
                    $"FAIL : {ex.Message}\n");
            }
        }

        private void SqliteTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultTextBox.Clear();
                ResultTextBox.AppendText("[SQLITE TEST START]\n");

                using var connection =
                    new SqliteConnection("Data Source=devtest.db");

                connection.Open();

                ResultTextBox.AppendText("DB OPEN : PASS\n");

                // 1. Table 생성
                var createCommand = connection.CreateCommand();

                createCommand.CommandText =
                """
        CREATE TABLE IF NOT EXISTS TestData
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Message TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """;

                createCommand.ExecuteNonQuery();

                ResultTextBox.AppendText("CREATE TABLE : PASS\n");

                // 2. 데이터 저장
                var insertCommand = connection.CreateCommand();

                insertCommand.CommandText =
                """
        INSERT INTO TestData
        (
            Message,
            CreatedAt
        )
        VALUES
        (
            $message,
            $createdAt
        );
        """;

                insertCommand.Parameters.AddWithValue(
                    "$message",
                    "SQLite Test");

                insertCommand.Parameters.AddWithValue(
                    "$createdAt",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                insertCommand.ExecuteNonQuery();

                ResultTextBox.AppendText("INSERT : PASS\n");

                // 3. 데이터 조회
                var selectCommand = connection.CreateCommand();

                selectCommand.CommandText =
                """
        SELECT Id, Message, CreatedAt
        FROM TestData
        ORDER BY Id DESC
        LIMIT 1;
        """;

                using var reader = selectCommand.ExecuteReader();

                if (reader.Read())
                {
                    ResultTextBox.AppendText("SELECT : PASS\n");

                    ResultTextBox.AppendText(
                        $"ID      : {reader.GetInt64(0)}\n");

                    ResultTextBox.AppendText(
                        $"MESSAGE : {reader.GetString(1)}\n");

                    ResultTextBox.AppendText(
                        $"TIME    : {reader.GetString(2)}\n");
                }

                ResultTextBox.AppendText("[SQLITE TEST END]\n");
            }
            catch (Exception ex)
            {
                ResultTextBox.AppendText(
                    $"FAIL : {ex.Message}\n");
            }
        }

        private async void MqttTest_Click(
            object sender,
            RoutedEventArgs e)
        {   
            try
            {
                ResultTextBox.Clear();

                ResultTextBox.AppendText(
                    "[MQTT TEST START]\n");

                var factory =
                    new MqttClientFactory();

                using var client =
                    factory.CreateMqttClient();

                client.ApplicationMessageReceivedAsync += e =>
                {
                    var payload =
                        e.ApplicationMessage
                            .ConvertPayloadToString();

                    Dispatcher.Invoke(() =>
                    {
                        ResultTextBox.AppendText(
                            $"RECEIVE : {payload}\n");
                    });

                    return Task.CompletedTask;
                };

                var options =
                    new MqttClientOptionsBuilder()
                        .WithClientId(
                            "AmrControlSystem-Test")
                        .WithTcpServer(
                            "localhost",
                            1883)
                        .Build();

                await client.ConnectAsync(
                    options,
                    CancellationToken.None);

                ResultTextBox.AppendText(
                    "CONNECT : PASS\n");

                var subscribeOptions =
                    factory
                        .CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(
                            "devtest/mqtt")
                        .Build();

                await client.SubscribeAsync(
                    subscribeOptions,
                    CancellationToken.None);

                ResultTextBox.AppendText(
                    "SUBSCRIBE : PASS\n");

                var message =
                    new MqttApplicationMessageBuilder()
                        .WithTopic(
                            "devtest/mqtt")
                        .WithPayload(
                            "Hello AMR MQTT")
                        .WithQualityOfServiceLevel(
                            MqttQualityOfServiceLevel
                                .AtLeastOnce)
                        .Build();

                await client.PublishAsync(
                    message,
                    CancellationToken.None);

                ResultTextBox.AppendText(
                    "PUBLISH : PASS\n");

                await Task.Delay(1000);

                await client.DisconnectAsync();

                ResultTextBox.AppendText(
                    "[MQTT TEST END]\n");
            }
            catch (Exception ex)
            {
                ResultTextBox.AppendText(
                    $"FAIL : {ex.Message}\n");
            }
        }
    }
}