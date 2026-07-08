using Domain.Abstractions.Persistence;
using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    internal sealed class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.Value == email.Value, ct);

        public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, ct);

        public async Task AddAsync(User user, CancellationToken ct = default) =>
            await _context.Users.AddAsync(user, ct);

        public void Update(User user) => _context.Users.Update(user);
    }

}
