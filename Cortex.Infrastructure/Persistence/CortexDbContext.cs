using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cortex.Infrastructure.Persistence
{
    public class CortexDbContext : DbContext
    {
        public CortexDbContext(DbContextOptions<CortexDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CortexDbContext).Assembly);
        }

    }
}
