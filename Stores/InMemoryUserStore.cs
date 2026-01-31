using System.Collections.Generic;
using System.Linq;
using UserManagementAPI.Models;

namespace UserManagementAPI.Stores;

public static class InMemoryUserStore
{
    private static readonly List<User> _users = new();
    private static int _nextId = 1;
    private static readonly object _lock = new();

    public static IEnumerable<User> GetAll()
    {
        lock (_lock)
        {
            return _users.ToList();
        }
    }

    public static User? Get(int id)
    {
        lock (_lock)
        {
            return _users.FirstOrDefault(u => u.Id == id);
        }
    }

    public static User Add(string name, string? email)
    {
        lock (_lock)
        {
            var user = new User(_nextId++, name, email);
            _users.Add(user);
            return user;
        }
    }

    public static bool Update(int id, string name, string? email)
    {
        lock (_lock)
        {
            var idx = _users.FindIndex(u => u.Id == id);
            if (idx == -1) return false;
            _users[idx] = new User(id, name, email);
            return true;
        }
    }

    public static bool Delete(int id)
    {
        lock (_lock)
        {
            var idx = _users.FindIndex(u => u.Id == id);
            if (idx == -1) return false;
            _users.RemoveAt(idx);
            return true;
        }
    }
}
