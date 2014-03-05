using Agent.Core;

namespace Agent.Console
{
    static class ConsoleProgram
    {
        static void Main()
        {
            var agent = new AgentMain("console");
            agent.Run();

            System.Console.ReadLine();
        }
    }
}
