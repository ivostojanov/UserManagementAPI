using System.Collections.Generic;
using System.Threading.Tasks;
using UserManagementAPI.Models;

namespace UserManagementAPI.Stores;

public interface IUserStore
{
    Task<IEnumerable<User>> GetAllAsync(int page, int size);
    Task<User?> GetAsync(int id);
    Task<User> AddAsync(string name, string? email);
    Task<bool> UpdateAsync(int id, string name, string? email);
    Task<bool> DeleteAsync(int id);
}
