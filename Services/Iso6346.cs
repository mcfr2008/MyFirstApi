namespace MyFirstApi.Services;

// ISO 6346 shipping container numbers: 3-letter owner code + category letter
// (U/J/Z) + 6-digit serial + check digit, e.g. CSQU3054383.
public static class Iso6346
{
    public static bool IsValid(string code)
    {
        if (code.Length != 11) return false;
        if (!code[..3].All(char.IsAsciiLetterUpper) || "UJZ".IndexOf(code[3]) < 0) return false;
        if (!code[4..].All(char.IsAsciiDigit)) return false;

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += CharValue(code[i]) * (1 << i);
        }
        return sum % 11 % 10 == code[10] - '0';
    }

    // Letters count up from A=10, skipping multiples of 11 (so B=12, L=23, V=34).
    private static int CharValue(char c)
    {
        if (char.IsAsciiDigit(c)) return c - '0';

        var value = 10;
        for (var letter = 'A'; letter < c; letter++)
        {
            value++;
            if (value % 11 == 0) value++;
        }
        return value;
    }
}
