using System;
using System.Threading.Tasks;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// A lightweight helper that wraps EF Core transactions so services can coordinate multiple repository calls.
/// </summary>
public interface ITransactionRunner
{
    /// <summary>
    /// Executes the provided delegate inside a database transaction and returns the delegate result.
    /// </summary>
    Task<TResult> RunAsync<TResult>(Func<Task<TResult>> action);

    /// <summary>
    /// Executes the provided delegate inside a database transaction.
    /// </summary>
    Task RunAsync(Func<Task> action);
}
