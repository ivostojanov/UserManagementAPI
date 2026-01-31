using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UserManagementAPI.Models;

namespace UserManagementAPI.Stores;

public class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId = 0;

    public Task<IEnumerable<User>> GetAllAsync(int page, int size)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 100;
        const int maxSize = 1000;
        if (size > maxSize) size = maxSize;

        var skip = (page - 1) * size;
        var snapshot = _users.Values
            .OrderBy(u => u.Id)
            .Skip(skip)
            .Take(size)
            .ToArray();

        return Task.FromResult<IEnumerable<User>>(snapshot);
    }

    public Task<User?> GetAsync(int id)
    {
        return Task.FromResult(_users.TryGetValue(id, out var u) ? u : null);
    }

    public Task<User> AddAsync(string name, string? email)
    {
        var id = Interlocked.Increment(ref _nextId);
        var user = new User(id, name, email);
        _users[id] = user;
        return Task.FromResult(user);
    }

    public Task<bool> UpdateAsync(int id, string name, string? email)
    {
        if (!_users.TryGetValue(id, out var existing)) return Task.FromResult(false);
        var updated = new User(id, name, email);
        var result = _users.TryUpdate(id, updated, existing);
        return Task.FromResult(result);
    }

    public Task<bool> DeleteAsync(int id)
    {
        return Task.FromResult(_users.TryRemove(id, out _));
    }
}
