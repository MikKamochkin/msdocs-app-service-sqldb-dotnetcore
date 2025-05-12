public static class ObfuscateEmailHelper
{
    public static string ObfuscateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return email;

        int atIndex = email.IndexOf('@');
        // if there's no '@' or only one character before it, nothing sensible to mask
        if (atIndex <= 1)
            return email;

        string localPart  = email.Substring(0, atIndex);
        string domainPart = email.Substring(atIndex + 1);

        // keep first and last char of local part, mask the middle
        string maskedLocal = localPart.Length <= 2
            ? localPart[0] + new string('*', localPart.Length - 1)
            : localPart[0]
            + new string('*', localPart.Length - 2)
            + localPart[^1];

        return $"{maskedLocal}@{domainPart}";
    }

}