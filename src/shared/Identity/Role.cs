namespace EnterpriseKnowledgeAgent.Shared.Identity;

/// <summary>
/// The four platform roles (FRD §3). Super Admin and Super Viewer are
/// platform-level; Application Admin and Application Viewer are scoped to
/// specific application memberships.
/// </summary>
public enum Role
{
    SuperAdmin,
    SuperViewer,
    ApplicationAdmin,
    ApplicationViewer,
}
