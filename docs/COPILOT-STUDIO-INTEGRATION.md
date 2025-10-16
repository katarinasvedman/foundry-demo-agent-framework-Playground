# Copilot Studio Integration Guide

This document explains how to integrate Microsoft Copilot Studio agents into the Foundry Demo agent orchestration system.

## Overview

The Copilot Studio integration allows you to leverage conversational AI capabilities from Microsoft Copilot Studio bots within your agent pipeline. This enables more natural language interactions and can enhance the user experience by providing conversational interfaces to your energy analysis workflow.

## Prerequisites

1. **Microsoft Copilot Studio Bot**: You need a published Copilot Studio bot with a publicly accessible endpoint
2. **Azure Tenant**: Access to an Azure tenant where the bot is deployed
3. **Authentication**: Proper Azure credentials configured (Managed Identity or Service Principal)

## Configuration

### 1. Update Configuration Files

Add Copilot Studio settings to your `appsettings.json`:

```json
{
  "CopilotStudio": {
    "Enabled": true,
    "BotUrl": "https://your-copilot-studio-bot-url.com",
    "TenantId": "your-azure-tenant-id"
  }
}
```

### 2. Environment Variables (Alternative)

You can also configure using environment variables:
```bash
COPILOT_STUDIO_BOT_URL=https://your-copilot-studio-bot-url.com
AZURE_TENANT_ID=your-azure-tenant-id
```

### 3. Enable in Orchestrator

The Copilot Studio agent is automatically included in the pipeline when:
- `CopilotStudio:Enabled` is set to `true`, OR
- `CopilotStudio:BotUrl` is configured

## Usage in Pipeline

### Automatic Integration

When enabled, the Copilot Studio agent is inserted into the pipeline after the Energy agent:

```
RemoteData Agent → Energy Agent → Copilot Studio Agent → EmailGenerator → EmailAssistant
```

### Pipeline Flow

1. **RemoteData Agent**: Fetches external energy data
2. **Energy Agent**: Performs energy analysis calculations
3. **Copilot Studio Agent**: Processes the energy analysis through conversational AI
4. **EmailGenerator**: Creates email content (if requested)
5. **EmailAssistant**: Sends the email (if requested)

## Agent Instructions

The Copilot Studio agent uses instructions from `Agents/CopilotStudio/CopilotStudioInstructions.md` which define:

- How to process energy analysis context
- Input/output JSON formats
- Error handling procedures
- Conversation context management

## Input Format

The agent expects JSON input with:

```json
{
    "message": "user request or context from previous agents",
    "context": {
        "zone": "SE3",
        "city": "Stockholm", 
        "date": "2025-10-15",
        "user_request": "original user request",
        "previous_agent_output": "output from Energy agent"
    },
    "conversation_id": "optional conversation identifier"
}
```

## Output Format

The agent responds with:

```json
{
    "response": "conversational response from Copilot Studio",
    "conversation_id": "conversation identifier",
    "status": "success",
    "metadata": {
        "response_time": "timestamp",
        "bot_version": "bot version info"
    },
    "context_passed": {
        "zone": "SE3",
        "city": "Stockholm",
        "date": "2025-10-15"
    }
}
```

## Manual Usage

### Create Agent Independently

```csharp
var copilotAgent = await CopilotStudioAgent.GetOrCreateAIAgentAsync(
    endpoint: "https://your-ai-foundry-endpoint",
    configuration: configuration,
    logger: logger);
```

### Create Custom Agent

```csharp
var copilotStudioService = new CopilotStudioAgent(logger, configuration);

var customAgent = await copilotStudioService.CreateCustomCopilotStudioAgentAsync(
    copilotUrl: "https://your-bot-url.com",
    tenantId: "your-tenant-id",
    agentName: "CustomEnergyBot",
    instructions: "Custom instructions for the bot...");
```

## Authentication

The integration uses `DefaultAzureCredential` which supports:

- **Managed Identity** (recommended for Azure deployments)
- **Azure CLI** authentication (for local development)
- **Environment variables** (`AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`)
- **Visual Studio** authentication

## Error Handling

If the Copilot Studio agent fails to initialize:
- The orchestrator logs a warning but continues execution
- The pipeline runs without the Copilot Studio step
- Other agents in the pipeline are unaffected

## Troubleshooting

### Common Issues

1. **Bot URL Invalid**
   - Verify the Copilot Studio bot is published and accessible
   - Check the URL format matches your bot's endpoint

2. **Authentication Failures**
   - Ensure Azure credentials are properly configured
   - Verify tenant ID is correct
   - Check bot permissions in Azure

3. **Agent Creation Fails**
   - Review logs for specific error messages
   - Verify AI Foundry endpoint is accessible
   - Check network connectivity to Copilot Studio

### Debugging Steps

1. **Enable detailed logging**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Foundry.Agents.Agents.CopilotStudio": "Debug"
       }
     }
   }
   ```

2. **Test configuration**:
   ```bash
   # Check if bot URL is accessible
   curl -I https://your-copilot-studio-bot-url.com
   
   # Verify Azure authentication
   az account show
   ```

3. **Review agent logs**:
   - Check Application Insights for detailed error information
   - Look for Copilot Studio specific error messages

## Examples

See `Examples/CopilotStudioExample.cs` for complete usage examples including:
- Basic agent creation
- Energy context processing
- Custom agent configuration
- Error handling patterns

## Best Practices

1. **Conversation Management**
   - Use consistent conversation IDs to maintain context
   - Pass relevant energy data in structured format
   - Handle conversation timeouts gracefully

2. **Performance Considerations**
   - Cache agent instances when possible
   - Monitor response times and implement timeouts
   - Consider fallback options for bot unavailability

3. **Security**
   - Use Managed Identity in production environments
   - Regularly rotate service principal credentials
   - Monitor access logs and authentication events

4. **Testing**
   - Test with sample energy data to verify integration
   - Validate conversation flows end-to-end
   - Monitor bot performance and response quality

## Integration with Infrastructure

When deploying to Azure using the provided Bicep templates, consider:
- Configuring Managed Identity for the Container App
- Adding Copilot Studio configuration to Key Vault
- Setting up Application Insights for monitoring bot interactions
- Configuring network access between services

The Copilot Studio integration enhances the Foundry Demo by providing natural language processing capabilities that can make energy analysis results more accessible and actionable for end users.