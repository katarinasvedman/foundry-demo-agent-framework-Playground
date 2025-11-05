using System.Threading;
using System.Threading.Tasks;

namespace Foundry.Agents.Agents.Shared
{
    /// <summary>
    /// User context information for On-Behalf-Of (OBO) flow
    /// </summary>
    public class UserContext
    {
        public string? AccessToken { get; set; }
        public string? UserId { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? TenantId { get; set; }
        
        public static UserContext Empty => new UserContext();
    }
    
    /// <summary>
    /// Extended interface that supports On-Behalf-Of flow
    /// </summary>
    public interface IPersistentAgentsClientAdapterWithOBO : IPersistentAgentsClientAdapter
    {
        /// <summary>
        /// Run an agent with user context for On-Behalf-Of flow
        /// </summary>
        Task<string?> RunAgentAsUserAsync(string agentId, object payload, UserContext userContext, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Create an agent with user context
        /// </summary>
        Task<string?> CreateAgentWithUserContextAsync(string modelDeploymentName, string name, string? instructions, 
            UserContext userContext, System.Collections.Generic.IEnumerable<string>? toolTypes = null);
    }
}