using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Amr.Verification;

/// <summary>
/// feature_list.json(요구사항 및 진행 상태 명세서)의 무결성과
/// 기록된 증거(Evidence) 파일 링크의 유효성을 검증하는 클래스입니다.
/// </summary>
public static class TrackerValidator
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
    static bool Nonempty(JsonNode? node) => !string.IsNullOrWhiteSpace(Text(node));
    static int Integer(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
    static JsonArray Array(JsonNode? node, string message) => node as JsonArray ?? throw new InvalidDataException(message);

    /// <summary>
    /// 지정된 JSON 파일을 읽고 추적기 검증을 수행합니다.
    /// </summary>
    /// <param name="path">feature_list.json 파일 경로</param>
    /// <returns>검증된 기능(Feature) 수와 세부 검증 항목(Check) 수</returns>
    public static (int Features, int Checks) ValidateFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Validate(JsonNode.Parse(File.ReadAllText(fullPath))!, Path.GetDirectoryName(fullPath)!);
    }

    /// <summary>
    /// 파싱된 JSON 데이터의 논리적 무결성(의존성 순환, 필수 필드 존재, 유효한 상태값 등)과 
    /// 각 증거 파일 경로의 유효성을 검사합니다.
    /// </summary>
    public static (int Features, int Checks) Validate(JsonNode data, string root)
    {
        root = Path.GetFullPath(root);
        var features = Array(data["features"], "features must be a list");
        Require(features.Count > 0, "features must be nonempty");
        var byId = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            Require(feature is JsonObject && Nonempty(feature["id"]), "invalid feature ID");
            Require(byId.TryAdd(Text(feature!["id"]), feature), "duplicate feature ID");
        }
        var included = Array(data["scope"]?["included"], "scope must include feature IDs").Select(Text).ToHashSet();
        Require(included.SetEquals(byId.Keys), "scope differs from feature IDs");
        Require(features.Count(f => Text(f!["status"]) == "in_progress") <= 1, "multiple active features");
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        void Visit(string id)
        {
            Require(byId.ContainsKey(id), $"unknown dependency {id}");
            Require(!visiting.Contains(id), $"dependency cycle at {id}");
            if (visited.Contains(id)) return;
            visiting.Add(id);
            foreach (var dep in Array(byId[id]["depends_on"], "dependencies must be a list")) Visit(Text(dep));
            visiting.Remove(id);
            visited.Add(id);
        }
        foreach (var id in byId.Keys) Visit(id);

        var checkCount = 0;
        foreach (var (id, feature) in byId)
        {
            var status = Text(feature["status"]);
            Require(new[] { "not_started", "in_progress", "blocked", "passing" }.Contains(status), $"{id}: invalid status");
            Require(Integer(feature["priority"]) > 0, $"{id}: invalid priority");
            var revision = Integer(feature["spec_revision"]);
            Require(revision > 0, $"{id}: invalid revision");
            var checks = Array(feature["verification"], $"{id}: verification must be a list");
            Require(checks.Count > 0 && checks.All(Nonempty), $"{id}: empty verification");
            checkCount += checks.Count;
            if (status == "blocked")
                Require(feature["blocker"] is JsonObject blocker && new[] { "reason", "impact", "resolution_condition" }.All(k => Nonempty(blocker[k])), $"{id}: incomplete blocker");
            else Require(feature["blocker"] is null, $"{id}: stale blocker");
            if (status is "in_progress" or "passing")
                Require(Array(feature["depends_on"], "dependencies required").All(d => Text(byId[Text(d)]["status"]) == "passing"), $"{id}: dependency not passing");
            var latest = new Dictionary<int, string>();
            DateTimeOffset? previous = null;
            foreach (var entry in Array(feature["evidence"], $"{id}: evidence must be a list"))
            {
                Require(entry is JsonObject, $"{id}: invalid evidence");
                var rev = Integer(entry!["spec_revision"]);
                var index = Integer(entry["verification_index"]);
                Require(rev >= 1 && rev <= revision, $"{id}: invalid evidence revision");
                Require(index >= 1 && (rev != revision || index <= checks.Count), $"{id}: invalid verification index");
                var time = Text(entry["executed_at"]);
                Require(Regex.IsMatch(time, @"T.*(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant), $"{id}: timestamp must include timezone");
                Require(DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when), $"{id}: invalid timestamp");
                Require(previous is null || when >= previous, $"{id}: evidence not chronological");
                previous = when;
                var result = Text(entry["result"]);
                Require(new[] { "passed", "failed", "not_run" }.Contains(result), $"{id}: invalid evidence result");
                foreach (var key in new[] { "command_or_procedure", "expected", "actual" }) Require(Nonempty(entry[key]), $"{id}: missing {key}");
                Require(Nonempty(entry["environment"]) || entry["environment"] is JsonObject { Count: > 0 }, $"{id}: missing environment");
                var paths = Array(entry["artifact_paths"], $"{id}: missing artifacts");
                Require(paths.Count > 0, $"{id}: missing artifacts");
                foreach (var node in paths) ValidateArtifact(root, Text(node), id);
                if (rev == revision) latest[index] = result;
            }
            if (status == "passing")
                Require(Enumerable.Range(1, checks.Count).All(i => latest.GetValueOrDefault(i) == "passed"), $"{id}: missing latest passing evidence");
        }
        var noteIds = new HashSet<string>();
        foreach (var note in Array(data["design_notes"], "design notes required"))
        {
            Require(note is JsonObject && Nonempty(note["id"]) && noteIds.Add(Text(note["id"])), "duplicate/invalid design note ID");
            var status = Text(note!["status"]);
            Require(new[] { "unresolved", "proposed", "adopted", "superseded" }.Contains(status), "invalid design note status");
            Require(Array(note["affects"], "affected features required").All(f => byId.ContainsKey(Text(f))), "unknown affected feature");
            if (status == "adopted")
            {
                Require(new[] { "provisional", "confirmed" }.Contains(Text(note["adoption"])), "missing adoption type");
                foreach (var key in new[] { "decision", "reason", "decided_at", "review_trigger" }) Require(Nonempty(note[key]), $"missing decision field {key}");
            }
        }
        _ = Array(data["change_log"], "missing change log");
        return (features.Count, checkCount);
    }

    /// <summary>
    /// 증거물(Artifact) 파일이 프로젝트 경로(root) 내에 존재하는 유효한 파일인지 검사합니다.
    /// 디렉터리 이탈(Directory Traversal)이나 심볼릭 링크(Junction 등)를 통한 우회도 차단합니다.
    /// </summary>
    static void ValidateArtifact(string root, string name, string id)
    {
        Require(!string.IsNullOrWhiteSpace(name) && !Path.IsPathRooted(name) && !name.Contains(':') && !name.StartsWith('\\'), $"{id}: artifact must be relative");
        var path = Path.GetFullPath(Path.Combine(root, name.Replace('\\', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, path);
        Require(relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative) && File.Exists(path), $"{id}: missing/escaping artifact {name}");
        // Reject reparse points inside artifact paths so a junction cannot escape the checkout.
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, $"{id}: linked artifact path {name}");
        }
    }
}
