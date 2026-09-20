using Amr.Verification;
namespace Amr.Verification.Tests;
public sealed class TrxTests
{
    [Theory]
    [InlineData(1,1,1,"Passed",true)][InlineData(0,0,0,"Passed",false)]
    [InlineData(1,0,0,"NotExecuted",false)][InlineData(1,1,0,"Failed",false)]
    [InlineData(1,1,1,"Failed",false)][InlineData(2,2,2,"Passed",false)]
    public void RejectsEmptySkippedFailedAndContradictoryResults(int total, int executed, int passed, string outcome, bool valid)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><ResultSummary><Counters total='{total}' executed='{executed}' passed='{passed}'/></ResultSummary><Results><UnitTestResult outcome='{outcome}'/></Results></TestRun>");
            if (valid) Assert.Equal(total, TrxValidator.Verify(path));
            else Assert.ThrowsAny<Exception>(() => TrxValidator.Verify(path));
        }
        finally { File.Delete(path); }
    }
    [Theory][InlineData("<TestRun/>")][InlineData("not xml")]
    public void RejectsMissingCountersAndMalformedXml(string content)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, content); Assert.ThrowsAny<Exception>(() => TrxValidator.Verify(path)); }
        finally { File.Delete(path); }
    }
}
