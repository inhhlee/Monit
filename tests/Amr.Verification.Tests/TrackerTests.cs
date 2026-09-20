using System.Text.Json.Nodes;
using Amr.Verification;

namespace Amr.Verification.Tests;

public sealed class TrackerTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "amr-harness-" + Guid.NewGuid().ToString("N"));
    readonly JsonNode data;
    JsonNode Feature => data["features"]![0]!;
    public TrackerTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "log.txt"), "Synthetic fixture only");
        data = JsonNode.Parse("""
        {
          "scope": {"included": ["A", "B"]},
          "features": [
            {"id":"A","status":"not_started","priority":1,"spec_revision":2,"depends_on":[],"verification":["one","two"],"blocker":null,"evidence":[]},
            {"id":"B","status":"not_started","priority":2,"spec_revision":1,"depends_on":["A"],"verification":["three"],"blocker":null,"evidence":[]}
          ],
          "design_notes": [], "change_log": []
        }
        """)!;
    }
    JsonObject Entry(int index = 1) => new()
    {
        ["spec_revision"] = 2, ["verification_index"] = index,
        ["executed_at"] = "2026-09-18T18:00:00+09:00", ["command_or_procedure"] = "synthetic",
        ["environment"] = "fixture", ["expected"] = "pass", ["actual"] = "pass",
        ["result"] = "passed", ["artifact_paths"] = new JsonArray("log.txt")
    };
    void Passing() { Feature["status"] = "passing"; Feature["evidence"] = new JsonArray(Entry(1), Entry(2)); }
    void Reject() => Assert.ThrowsAny<Exception>(() => TrackerValidator.Validate(data, root));
    [Fact] public void InitialStructure() => Assert.Equal((2, 3), TrackerValidator.Validate(data, root));
    [Fact] public void CompleteEvidence() { Passing(); Assert.Equal((2, 3), TrackerValidator.Validate(data, root)); }
    [Fact] public void LatestFailureInvalidatesPass()
    {
        Passing(); var entry = Entry(); entry["result"] = "failed";
        ((JsonArray)Feature["evidence"]!).Add(entry); Reject();
    }
    [Fact] public void OldRevisionCannotCertify() { Passing(); Feature["spec_revision"] = 3; Reject(); }
    [Theory]
    [InlineData("unknown")][InlineData("passing")][InlineData("blocked")]
    public void InvalidStatuses(string status) { Feature["status"] = status; Reject(); }
    [Theory]
    [InlineData("B")][InlineData("missing")]
    public void CyclicOrUnknownDependency(string dep) { Feature["depends_on"] = new JsonArray(dep); Reject(); }
    [Fact] public void DependencyMustPass() { data["features"]![1]!["status"] = "in_progress"; Reject(); }
    [Fact] public void MultipleActiveFeaturesRejected()
    {
        Feature["status"] = "in_progress"; data["features"]![1]!["status"] = "in_progress"; Reject();
    }
    [Theory]
    [InlineData("missing")][InlineData("../log.txt")][InlineData("C:\\log.txt")][InlineData("C:log.txt")][InlineData("\\server\\log.txt")]
    public void MissingOrEscapingArtifact(string path)
    {
        var entry = Entry(); entry["artifact_paths"] = new JsonArray(path);
        Feature["evidence"] = new JsonArray(entry); Reject();
    }
    [Theory]
    [InlineData("executed_at", "2026-09-18T18:00:00")][InlineData("executed_at", "invalid+09:00")]
    [InlineData("result", "success")][InlineData("environment", "")]
    public void InvalidEvidenceFields(string key, string value)
    {
        var entry = Entry(); entry[key] = value; Feature["evidence"] = new JsonArray(entry); Reject();
    }
    [Theory][InlineData(0)][InlineData(3)]
    public void InvalidEvidenceIndex(int index) { Feature["evidence"] = new JsonArray(Entry(index)); Reject(); }
    [Fact] public void EvidenceMustBeChronological()
    {
        var older = Entry(); older["executed_at"] = "2026-09-17T18:00:00+09:00";
        Feature["evidence"] = new JsonArray(Entry(), older); Reject();
    }
    [Fact] public void StaleBlockerRejected() { Feature["blocker"] = new JsonObject { ["reason"] = "old" }; Reject(); }
    [Fact] public void ValidBlockerAccepted()
    {
        Feature["status"] = "blocked";
        Feature["blocker"] = new JsonObject { ["reason"] = "external", ["impact"] = "cannot run", ["resolution_condition"] = "restore service" };
        TrackerValidator.Validate(data, root);
    }
    [Fact] public void DuplicateIdsRejected() { data["features"]![1]!["id"] = "A"; Reject(); }
    [Fact] public void InvalidJsonFileRejected()
    {
        var path = Path.Combine(root, "bad.json"); File.WriteAllText(path, "{");
        Assert.ThrowsAny<Exception>(() => TrackerValidator.ValidateFile(path));
    }
    [Fact] public void AdoptedDecisionRequiresReason()
    {
        data["design_notes"] = new JsonArray(new JsonObject
        {
            ["id"] = "DN-1", ["status"] = "adopted", ["affects"] = new JsonArray("A"),
            ["adoption"] = "provisional", ["decision"] = "dev", ["decided_at"] = "2026-09-20", ["review_trigger"] = "integration"
        }); Reject();
    }
    public void Dispose() => Directory.Delete(root, true);
}
