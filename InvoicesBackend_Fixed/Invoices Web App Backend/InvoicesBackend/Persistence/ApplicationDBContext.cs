using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Domain.Entities;

namespace InvoicesBackend.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Business> Businesses { get; set; }
    public DbSet<Client> Clients { get; set; }

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<ExpenseMaster> ExpenseMasters { get; set; }
    public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
    public DbSet<InvoiceBranding> InvoiceBrandings { get; set; }
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<AssistantAssignment> AssistantAssignments { get; set; }
    public DbSet<CalendarEvent> CalendarEvents { get; set; }
    public DbSet<Bill> Bills { get; set; }
    public DbSet<BillItem> BillItems { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Notification> Notifications { get; set; }
}