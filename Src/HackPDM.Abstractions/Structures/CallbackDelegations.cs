using System.Threading.Tasks;

namespace HackPDM.Abstractions;

public static class CallbackDelegations
{
    /// <summary>Tries to log in to the server.</summary>
    /// <returns><see langword="true"/> when the login succeeded.</returns>
    public delegate Task<bool> AbleToLogin();
}
