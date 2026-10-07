using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Api;

// Dữ liệu ban đầu ghi vào file dữ liệu mới, theo đúng cấu trúc KB trong architecture.md:
//   /templates/email  template-001, template-002, README
//   /team/devops      members, schedule, README
//   /docs/guides      guide-001, guide-002
public static class KbSeedData
{
    public static IReadOnlyList<KbDocument> Documents { get; } =
    [
        new("doc-001", "Customer Response Template",
            "# Customer Response Template\n\nDear {customer},\n\nThank you for contacting MindX support. " +
            "We have received your request ({ticket_id}) and will reply as soon as possible.\n\n" +
            "Best regards,\nMindX Support Team\n",
            "/templates/email", ["template", "email"]),
        new("doc-002", "Incident Acknowledgement Template",
            "# Incident Acknowledgement Template\n\nDear {customer},\n\nWe are aware of the issue affecting " +
            "{service} and our engineers are investigating it now. We will send the next update within " +
            "{update_interval}.\n\nBest regards,\nMindX Support Team\n",
            "/templates/email", ["template", "email", "incident"]),
        new("doc-003", "Email Templates README",
            "# Email Templates\n\nReusable email templates for customer communication. " +
            "Replace every {placeholder} before sending.\n",
            "/templates/email", ["readme"]),
        new("doc-004", "DevOps Team Members",
            "# DevOps Team Members\n\n| Name  | Role                          |\n|-------|-------------------------------|\n" +
            "| Alice | Team Lead                     |\n| Bob   | Site Reliability Engineer     |\n" +
            "| Carol | Cloud Engineer                |\n",
            "/team/devops", ["team", "devops"]),
        new("doc-005", "DevOps On-call Schedule",
            "# DevOps On-call Schedule\n\n- Week 1: Alice\n- Week 2: Bob\n- Week 3: Carol\n\n" +
            "The rotation changes every Monday at 09:00.\n",
            "/team/devops", ["team", "schedule"]),
        new("doc-006", "DevOps Team README",
            "# DevOps Team\n\nThe DevOps team runs the cloud infrastructure, CI/CD pipelines and monitoring.\n",
            "/team/devops", ["readme"]),
        new("doc-007", "Getting Started Guide",
            "# Getting Started Guide\n\nHow to set up your development environment and handle your first " +
            "support ticket.\n",
            "/docs/guides", ["guide", "onboarding"]),
        new("doc-008", "Ticket Handling Guide",
            "# Ticket Handling Guide\n\nFollow the 7 steps: reception, response, diagnosis, resolution, " +
            "updates, follow-up and analysis.\n",
            "/docs/guides", ["guide", "support"]),
    ];
}
