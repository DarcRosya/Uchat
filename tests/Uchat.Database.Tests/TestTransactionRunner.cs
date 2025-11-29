using System;
using System.Threading.Tasks;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Tests;

internal sealed class TestTransactionRunner : ITransactionRunner
{
    public Task RunAsync(Func<Task> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return action();
    }

    public Task<TResult> RunAsync<TResult>(Func<Task<TResult>> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return action();
    }
}
