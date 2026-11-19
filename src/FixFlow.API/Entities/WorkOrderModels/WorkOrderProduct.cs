using FixFlow.Shared.Interfaces;

namespace FixFlow.API.Entities.WorkOrderModels
{
    public class WorkOrderProduct : IMustHaveTenant
    {
        public Guid Id { get; set; }

        public Guid WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPriceAtTimeOfUse { get; set; }

        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

    }
}
