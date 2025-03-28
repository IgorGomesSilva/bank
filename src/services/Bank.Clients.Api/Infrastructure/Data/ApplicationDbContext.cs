using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Bank.Clients.Api.Domain;
using Bank.Clients.Api.Infrastructure.Data.DbMappings;
using Bank.Core.Data;
using Bank.Core.DomainObjects;
using Bank.Core.Mediator;
using Bank.Core.Messages;
using Microsoft.EntityFrameworkCore;

namespace Bank.Clients.Api.Infrastructure.Data;

[ExcludeFromCodeCoverage]
public class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly IMediatorHandler _mediatorHandler;
    public DbSet<Client> Clients { get; set; }

    public ApplicationDbContext()
    {
        
    }
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options, 
        IMediatorHandler mediatorHandler) : base(options)
    {
        _mediatorHandler = mediatorHandler;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = "Host=localhost;Port=5432;Database=bank;Username=postgres;Password=12345;";
            optionsBuilder.UseNpgsql(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<Event>();
        modelBuilder.Ignore<ValidationResult>();

        modelBuilder.ApplyConfiguration(new ClientMapping());
    }

    public async Task<bool> CommitAsync()
    {
        UpdadteCreateAndUpdatedDate();

        var sucess = await base.SaveChangesAsync() > 0;
        if (sucess) await _mediatorHandler.PublishEvents(this);

        return sucess;
    } 

    private void UpdadteCreateAndUpdatedDate()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is Entity && (
                    e.State == EntityState.Added
                    || e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            ((Entity)entityEntry.Entity).UpdatedDate = DateTime.UtcNow;

            if (entityEntry.State == EntityState.Added)
            {
                ((Entity)entityEntry.Entity).CreatedDate = DateTime.UtcNow;
            }
        }
    }   

}

[ExcludeFromCodeCoverage]
public static class MediatorExtension
{
    public static async Task PublishEvents<T>(this IMediatorHandler mediator, T ctx) where T : DbContext
    {
        var entities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.Notifications != null && x.Entity.Notifications.Any());

        var domainEvents = entities
            .SelectMany(x => x.Entity!.Notifications!)
            .ToList();

        entities.ToList()
            .ForEach(entity => entity.Entity.ClearEvents());

        var tasks = domainEvents
        .Select(async (domainEvent) =>
        {
            await mediator.PublishEvent(domainEvent);
        });

        await Task.WhenAll(tasks);
    }
}