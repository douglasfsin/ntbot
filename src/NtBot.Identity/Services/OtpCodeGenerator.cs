namespace NtBot.Identity.Services;

public static class OtpCodeGenerator
{
    public static string Generate(int length = 6)
    {
        var random = Random.Shared;
        var code = new char[length];
        for (var i = 0; i < length; i++)
        {
            code[i] = (char)('0' + random.Next(0, 10));
        }
        return new string(code);
    }

    public static DateTime GetExpiration(int minutes = 10) =>
        DateTime.UtcNow.AddMinutes(minutes);
}
