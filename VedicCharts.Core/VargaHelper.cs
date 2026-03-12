namespace VedicCharts.Core;

internal static class VargaHelper
{
    internal sealed record DivisionalSign(string SignName, double DegreeInSign);

    private static readonly string[] Signs =
    {
        "Aries","Taurus","Gemini","Cancer","Leo","Virgo","Libra","Scorpio","Sagittarius","Capricorn","Aquarius","Pisces"
    };

    public static int SignIndex(string signName)
    {
        for (int i = 0; i < Signs.Length; i++)
            if (Signs[i].Equals(signName, StringComparison.OrdinalIgnoreCase))
                return i;
        return 0;
    }

    public static string SignNameFromIndex(int index)
    {
        index = ((index % 12) + 12) % 12;
        return Signs[index];
    }

    public static DivisionalSign LongitudeToDivisionalSign(double longitude, string chartTypeId)
    {
        double normalized = ((longitude % 360) + 360) % 360;
        int baseSignIndex = (int)(normalized / 30) % 12;
        double degInSign = normalized % 30;

        if (chartTypeId.Equals("RasiD1", StringComparison.OrdinalIgnoreCase))
            return new DivisionalSign(Signs[baseSignIndex], degInSign);

        return chartTypeId switch
        {
            "HoraD2" => HoraD2(baseSignIndex, degInSign),
            "DrekkanaD3" => DrekkanaD3(baseSignIndex, degInSign),
            "ChaturthamshaD4" => ChaturthamshaD4(baseSignIndex, degInSign),
            "SaptamshaD7" => SaptamshaD7(baseSignIndex, degInSign),
            "NavamshaD9" => NavamshaD9(baseSignIndex, degInSign),
            "DashamamshaD10" => DashamamshaD10(baseSignIndex, degInSign),
            "DwadashamshaD12" => GenericDivision(baseSignIndex, degInSign, 12),
            "ShodashamshaD16" => GenericDivision(baseSignIndex, degInSign, 16),
            "VimshamshaD20" => GenericDivision(baseSignIndex, degInSign, 20),
            "ChaturvimshamshaD24" => GenericDivision(baseSignIndex, degInSign, 24),
            "BhamshaD27" => GenericDivision(baseSignIndex, degInSign, 27),
            "TrimshamshaD30" => TrimshamshaD30(baseSignIndex, degInSign),
            "KhavedamshaD40" => GenericDivision(baseSignIndex, degInSign, 40),
            "AkshavedamshaD45" => GenericDivision(baseSignIndex, degInSign, 45),
            "ShashtyamshaD60" => GenericDivision(baseSignIndex, degInSign, 60),
            _ => new DivisionalSign(Signs[baseSignIndex], degInSign),
        };
    }

    private static DivisionalSign GenericDivision(int baseSignIndex, double degInSign, int division)
    {
        double partSize = 30.0 / division;
        int partIndex = Math.Clamp((int)(degInSign / partSize), 0, division - 1);

        // Generic “wrap across zodiac” mapping. This is a practical fallback for less-common vargas.
        int outSignIndex = (baseSignIndex * division + partIndex) % 12;
        double outDeg = (degInSign * division) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static bool IsOddSign(int signIndex) => (signIndex % 2) == 0; // Aries=0 is odd-sign group in Jyotish

    private static bool IsMovable(int signIndex) => signIndex is 0 or 3 or 6 or 9;
    private static bool IsFixed(int signIndex) => signIndex is 1 or 4 or 7 or 10;
    private static bool IsDual(int signIndex) => !IsMovable(signIndex) && !IsFixed(signIndex);

    private static DivisionalSign HoraD2(int baseSignIndex, double degInSign)
    {
        // Matches the common Hora scheme used in the VedAstro tables.
        bool odd = IsOddSign(baseSignIndex);
        bool firstHalf = degInSign < 15.0;

        int outSignIndex = odd
            ? (firstHalf ? SignIndex("Leo") : SignIndex("Cancer"))
            : (firstHalf ? SignIndex("Cancer") : SignIndex("Leo"));

        double outDeg = (degInSign * 2) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign DrekkanaD3(int baseSignIndex, double degInSign)
    {
        int partIndex = Math.Clamp((int)(degInSign / 10.0), 0, 2);
        int outSignIndex = (baseSignIndex + (partIndex * 4)) % 12;
        double outDeg = (degInSign * 3) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign ChaturthamshaD4(int baseSignIndex, double degInSign)
    {
        int partIndex = Math.Clamp((int)(degInSign / 7.5), 0, 3);

        int start = IsMovable(baseSignIndex) ? baseSignIndex
            : IsFixed(baseSignIndex) ? (baseSignIndex + 3) % 12
            : (baseSignIndex + 6) % 12;

        int outSignIndex = (start + (partIndex * 3)) % 12;
        double outDeg = (degInSign * 4) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign SaptamshaD7(int baseSignIndex, double degInSign)
    {
        double partSize = 30.0 / 7.0;
        int partIndex = Math.Clamp((int)(degInSign / partSize), 0, 6);

        int start = IsOddSign(baseSignIndex) ? baseSignIndex : (baseSignIndex + 6) % 12;
        int outSignIndex = (start + partIndex) % 12;
        double outDeg = (degInSign * 7) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign NavamshaD9(int baseSignIndex, double degInSign)
    {
        double partSize = 30.0 / 9.0;
        int partIndex = Math.Clamp((int)(degInSign / partSize), 0, 8);

        int start = IsMovable(baseSignIndex) ? baseSignIndex
            : IsFixed(baseSignIndex) ? (baseSignIndex + 8) % 12
            : (baseSignIndex + 4) % 12;

        int outSignIndex = (start + partIndex) % 12;
        double outDeg = (degInSign * 9) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign DashamamshaD10(int baseSignIndex, double degInSign)
    {
        int partIndex = Math.Clamp((int)(degInSign / 3.0), 0, 9);
        int start = IsOddSign(baseSignIndex) ? baseSignIndex : (baseSignIndex + 8) % 12;
        int outSignIndex = (start + partIndex) % 12;
        double outDeg = (degInSign * 10) % 30.0;
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }

    private static DivisionalSign TrimshamshaD30(int baseSignIndex, double degInSign)
    {
        // Common Parashara Trimshamsha scheme (as widely implemented):
        // Odd signs: 0-5 Aries, 5-10 Aquarius, 10-18 Sagittarius, 18-25 Gemini, 25-30 Libra
        // Even signs: 0-5 Taurus, 5-10 Virgo, 10-18 Pisces, 18-25 Capricorn, 25-30 Scorpio
        bool odd = IsOddSign(baseSignIndex);
        int outSignIndex = odd
            ? degInSign < 5 ? SignIndex("Aries")
            : degInSign < 10 ? SignIndex("Aquarius")
            : degInSign < 18 ? SignIndex("Sagittarius")
            : degInSign < 25 ? SignIndex("Gemini")
            : SignIndex("Libra")
            : degInSign < 5 ? SignIndex("Taurus")
            : degInSign < 10 ? SignIndex("Virgo")
            : degInSign < 18 ? SignIndex("Pisces")
            : degInSign < 25 ? SignIndex("Capricorn")
            : SignIndex("Scorpio");

        double outDeg = (degInSign * 30) % 30.0; // per existing divisional longitude convention
        return new DivisionalSign(Signs[outSignIndex], outDeg);
    }
}

