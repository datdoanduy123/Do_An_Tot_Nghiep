using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Data.Repositories
{
    public class AgentRepository : IAgentRepository
    {
        private readonly ApplicationDbContext _dbContext;
        public AgentRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<AgentContext> CreateAsync(AgentContext agent)
        {
            _dbContext.AgentContexts.Add(agent);
            await _dbContext.SaveChangesAsync();
            return agent;
        }

        public async Task<AgentContext?> GetByFileIdAsync(int FileId)
        {
            return await _dbContext.AgentContexts.FirstOrDefaultAsync(u => u.FileId == FileId);                 /////// Lỗi
        }
    }
}