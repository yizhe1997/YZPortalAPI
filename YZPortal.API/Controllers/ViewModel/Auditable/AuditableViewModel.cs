namespace YZPortal.API.Controllers.ViewModel.Auditable
{
    public class AuditableViewModel : BaseViewModel
    {
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
