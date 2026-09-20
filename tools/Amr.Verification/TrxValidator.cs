using System.Xml;
using System.Xml.Linq;

namespace Amr.Verification;

public static class TrxValidator
{
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
