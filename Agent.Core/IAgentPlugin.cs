using System;
using Agent.Core.ServerOperations;

namespace Agent.Core
{
    public delegate bool SendResultHandler(ISofOperation operation);
    public delegate bool RegisterOperationHandler(ISofOperation operation);

    public interface IAgentPlugin
    {
        void Start();
        void Stop();

        void RunOperation(ISofOperation operation);
        ISofOperation InitialData();

        event SendResultHandler SendResults;
        event RegisterOperationHandler RegisterOperation;

        string Name { get; }
    }
}
