using Amr.Verification;

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
