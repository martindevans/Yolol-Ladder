using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace YololCompetition.Services.Database
{
    public interface IDatabase
    {
        DbCommand CreateCommand();
    }

    public class SqlAsyncResult<TItem>
        : IAsyncEnumerable<TItem>
    {
        private readonly IDatabase _database;
        private readonly Func<IDatabase, DbCommand> _prepare;
        private readonly Func<DbDataReader, TItem> _read;

        protected internal SqlAsyncResult(IDatabase database, Func<IDatabase, DbCommand> prepare, Func<DbDataReader, TItem> read)
        {
            _database = database;
            _prepare = prepare;
            _read = read;
        }

        IAsyncEnumerator<TItem> IAsyncEnumerable<TItem>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return new AsyncEnumerator(_prepare(_database), _read);
        }

        private class AsyncEnumerator
            : IAsyncEnumerator<TItem>, IAsyncDisposable
        {
            private readonly DbCommand _query;
            private readonly Func<DbDataReader, TItem> _read;

            private DbDataReader? _reader;

            public AsyncEnumerator(DbCommand query, Func<DbDataReader, TItem> read)
            {
                _query = query;
                _read = read;
            }

            public async ValueTask DisposeAsync()
            {
                await _query.DisposeAsync();
                if (_reader != null)
                    await _reader.DisposeAsync();
                _reader = null;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_reader == null)
                    _reader = await _query.ExecuteReaderAsync();

                return await _reader.ReadAsync();
            }

            public TItem Current {
                get
                {
                    if (_reader == null)
                        throw new InvalidOperationException("Cannot get `Current` item when reader is null");
                    return _read(_reader);
                }
            }
        }
    }
}
