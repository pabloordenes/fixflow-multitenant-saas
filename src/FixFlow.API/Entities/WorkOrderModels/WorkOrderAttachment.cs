using FixFlow.Shared.Enums;
using FixFlow.Shared.Interfaces;

namespace FixFlow.API.Entities.WorkOrderModels
{
    public class WorkOrderAttachment : IMustHaveTenant
    {
        public Guid Id { get; set; }
        public required string FileUri { get; set; }
        public AttachmentType Type { get; set; }
        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

        public Guid WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
