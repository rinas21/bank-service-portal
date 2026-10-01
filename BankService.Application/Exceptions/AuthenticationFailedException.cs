namespace BankService.Application.Exceptions;

public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}
