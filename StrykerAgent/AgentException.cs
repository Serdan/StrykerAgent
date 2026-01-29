namespace StrykerAgent;

public sealed class AgentException(int exitCode, string message) : Exception(message)
{
    public int ExitCode => exitCode;
}
