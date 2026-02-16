using System;
using System.Linq;
using JoSystem.Data;
using JoSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace JoSystem.Services
{
    public static class MultiDbService
    {
        public static DbConnectionConfig GetConnection(string name)
        {
            using var db = new AppDbContext();
            return db.DbConnectionConfigs.AsNoTracking()
                .Where(c => c.Enabled)
                .OrderBy(c => c.Order)
                .FirstOrDefault(c => c.Name == name);
        }
    }
}

