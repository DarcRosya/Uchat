using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Database.Context;
using Database.Repositories.Interfaces;

namespace Database.Repositories;

public sealed class TransactionRunner : ITransactionRunner
{
    private readonly UchatDbContext _context;

    public TransactionRunner(UchatDbContext context)
    {
        _context = context;
    }

    public async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RunAsync(Func<Task> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await action();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
