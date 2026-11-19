using FixFlow.API.Entities.WorkOrderModels;
using FixFlow.Shared.Interfaces;

namespace FixFlow.API.Entities
{
    public class Customer : IMustHaveTenant
    {
        public Guid Id { get; set; }    
        public required string Rut { get; set; }
        public required string BusinessName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public required string MainAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public ICollection<WorkOrder> WorkOrders { get; set; } = [];
    }
}
