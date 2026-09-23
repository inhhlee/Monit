using Amr.Verification;

/// <summary>
/// Amr.Verification 도구의 진입점(Entry Point)입니다.
/// 명령줄 인수에 따라 tracker(추적기 검증), trx(테스트 결과 검증), broker(실제 Broker 연동 검증) 작업을 분기하여 수행합니다.
/// </summary>
try
{
    if (args.Length == 2 && args[0] == "tracker")
    {
        var (features, checks) = TrackerValidator.ValidateFile(args[1]);
        Console.WriteLine($"PASS: {features} features, {checks} verification items; structure and artifact links only");
    }
    else if (args.Length == 2 && args[0] == "trx")
        Console.WriteLine($"PASS: {TrxValidator.Verify(args[1])} tests executed and passed");
    else if (args.Length is 2 or 3 && args[0] == "broker")
        await BrokerVerification.RunAsync(args[1], args.Length == 3 ? args[2] : "docker");
    else
        throw new ArgumentException("Usage: Amr.Verification tracker <feature_list.json> | trx <results.trx> | broker <repository-root> [docker-executable]");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL: {error.Message}");
    return 1;
}
