namespace WebApp.Services;

[Flags]
public enum PasswordCharsetOptions
{
    None = 0,
    Lowercase = 1,
    Digits = 2,
    Uppercase = 4,
    Symbols = 8
}
