using System.Security.Cryptography;
using System.Text;
using Application.Models;
using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using LoginViewModel = Models.LoginViewModel;

namespace Services;

public class UserService : IUserService
    {
        readonly IRepositorio<User> _userRepository;
        readonly IConfiguration _configuration;
        public UserService(IConfiguration configuration, IRepositorio<User> userRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
            _userRepository.InitializeCollection(_configuration["MongoDbSettings:ConnectionString"],
                _configuration["MongoDbSettings:DataBaseName"],
                _configuration["MongoDbSettings:DbUserCollection"]);
        }
        private static List<User> _users = new List<User>();

        public async Task<User> GetUserByEmail(string email)
        {
            var _user = await _userRepository.GetByMailAsync(email, CancellationToken.None);
            return await Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task<User?> Register(RegisterViewModel model)
        {
            var _user = await _userRepository.GetByMailAsync(model.Email, CancellationToken.None);
            if (_user is not null)
            {
                throw new InvalidOperationException("Este e-mail já está cadastrado.");
            }

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = model.FullName,
                Email = model.Email,
                PhoneNumber = "",
                PasswordHash = HashPassword(model.Password),
                PersonalName = model.FullName,
                Nickname = model.FullName,
                BackupDate = DateTime.UtcNow
                
            };
            var user = await _userRepository.InsertOneAsync(newUser) as User;
            return user;
        }

        public async Task<User> Login(LoginViewModel model)
        {
            var user = await _userRepository.GetByMailAsync(model.Email, CancellationToken.None) as User;
            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))return null;
            return user;
        }
        
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            return HashPassword(enteredPassword).Equals(storedHash);
        }
    }