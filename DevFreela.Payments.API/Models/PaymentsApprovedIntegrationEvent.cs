namespace DevFreela.Payments.API.Models;

public class PaymentsApprovedIntegrationEvent
{
    public PaymentsApprovedIntegrationEvent(int idProject)
    {
        IdProject = idProject;
    }

    public int IdProject { get; set; }
}
