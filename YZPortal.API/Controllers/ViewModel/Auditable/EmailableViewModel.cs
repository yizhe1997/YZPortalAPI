namespace YZPortal.API.Controllers.ViewModel.Auditable
{
    public class EmailableViewModel : AuditableViewModel
	{
		public string? Email { get; set; }
		public DateTime? SentDateTime { get; set; }
		public string? FailedMessage { get; set; }
		public DateTime? FailedSentDateTime { get; set; }
		public int Attempts { get; set; }
		public DateTime? LastAttemptedSentDateTime { get; set; }
	}
}
