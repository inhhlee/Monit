using System.Xml;
using System.Xml.Linq;

namespace Amr.Verification;

/// <summary>
/// TRX(Test Results XML) 파일을 분석하여 모든 자동화 테스트가 누락 없이 실행되고 100% 통과했는지 검증하는 클래스입니다.
/// </summary>
public static class TrxValidator
{
    /// <summary>
    /// 지정된 경로의 TRX 파일을 파싱하여 테스트 실행 결과를 검증합니다.
    /// 하나라도 실패하거나, 실행되지 않은 테스트가 있거나, 집계 카운트와 개별 결과가 불일치하면 예외를 발생시킵니다.
    /// </summary>
    /// <param name="path">검증할 TRX 파일 경로</param>
    /// <returns>전체 실행 및 통과된 테스트 개수</returns>
    public static int Verify(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var document = XDocument.Load(reader);
        var counters = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Counters")
            ?? throw new InvalidDataException("Missing TRX counters");
        int Count(string key) => int.Parse(counters.Attribute(key)?.Value ?? "0");
        var total = Count("total");
        var results = document.Descendants().Where(e => e.Name.LocalName == "UnitTestResult").ToArray();
        if (total < 1 || Count("executed") != total || Count("passed") != total)
            throw new InvalidDataException("Tests must be nonempty, all executed and all passed");
        if (results.Length != total || results.Any(e => e.Attribute("outcome")?.Value != "Passed"))
            throw new InvalidDataException("TRX individual results disagree with passing counters");
        return total;
    }
}
