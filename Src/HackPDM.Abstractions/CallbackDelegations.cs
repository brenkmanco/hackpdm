using System.Threading.Tasks;

namespace HackPDM.Abstractions;

public static class CallbackDelegations
{
    public delegate Task<bool> AbleToLogin();
}
