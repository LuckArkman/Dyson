using Application.Models;
using Dtos;
using Models;

namespace Interfaces;

public interface IUserService
{
    Task<User> Register(RegisterViewModel model);
    Task<User> Login(LoginViewModel model);
    Task<User> GetUserByEmail(string email);
}