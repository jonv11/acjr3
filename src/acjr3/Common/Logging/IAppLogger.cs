namespace Acjr3.Common;

public interface IAppLogger
{
    bool IsVerbose { get; }

    void Verbose(string message);
}
