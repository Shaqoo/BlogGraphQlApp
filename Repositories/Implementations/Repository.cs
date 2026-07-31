using BlogGraphQlApp.Core.Repositories;
using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BlogGraphQlApp.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

        public async Task AddRangeAsync(IEnumerable<T> entities) => await _dbSet.AddRangeAsync(entities);

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression) => await _dbSet.AnyAsync(expression);
       
        public async Task<int> CountAsync(Expression<Func<T, bool>> expression) => await _dbSet.Where(expression).CountAsync();
    
        public IQueryable<T> Find(Expression<Func<T, bool>> expression) => _dbSet.Where(expression);

        public IQueryable<T> GetAll() => _dbSet.AsQueryable();

        public async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);

        public void Remove(T entity) => _dbSet.Remove(entity);

        public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);

        public void Update(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}